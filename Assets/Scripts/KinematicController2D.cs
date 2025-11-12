using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using System.Collections;

public class KinematicController2D : MonoBehaviour
{
    [Header("Body (AABB)")]
    public Vector2 size = new Vector2(0.9f, 0.9f);
    public Vector2 bodyOffset = Vector2.zero;
    public Transform Sprite;

    [Header("Respawn / Death")]
    public Vector2 respawnPoint;
    public bool reloadSceneOnDeath = false;
    public bool useParticleExplosion = true;

    [Header("Level Complete")]
    public bool  loadNextSceneOnComplete = false;
    public string nextSceneName = "";
    public float suckDuration = 0.6f;
    public float suckSpinSpeed = 900f;  // deg/s
    public float suckScaleEnd = 0.1f;   // how small the sprite becomes


    [Header("Motion")]
    public float gravity = -60f;
    public float jumpSpeed = 22f;
    public float moveSpeedX = 12f;
    public int   maxSolverIters = 8;
    public int   maxSubsteps = 1;

    [Header("Grounding")]
    public float groundCheckPadding = 0.02f;

    [Header("Jump Assist")]
    public float jumpBufferTime = 0.10f;
    public float coyoteTime = 0.06f;

    private Vector2 velocity;
    public Vector2 Velocity { get { return velocity; } private set { velocity = value; } }
    public bool IsGrounded { get; private set; }

    float jumpBufferCounter = 0f;
    float coyoteCounter = 0f;

    // Death / hazards
    private bool isDead = false;
    private float hazardIgnoreUntil = 0f; // realtime-based grace

    // Cached visual (for “faded” bug)
    private SpriteRenderer spriteRenderer;
    private Material cachedSpriteMaterial;
    private bool isCompleting = false;
    private Color    cachedSpriteColor;

    void Awake()
    {
        velocity = Vector2.zero;
        IsGrounded = false;
        if (respawnPoint == Vector2.zero)
            respawnPoint = transform.position;

        if (Sprite)
        {
            spriteRenderer = Sprite.GetComponent<SpriteRenderer>();
            if (spriteRenderer)
            {
                cachedSpriteMaterial = spriteRenderer.sharedMaterial;
                cachedSpriteColor    = spriteRenderer.color;
            }

            // Guard: warn if Sprite is actually the same GameObject as this controller
            if (Sprite.gameObject == this.gameObject)
            {
                Debug.LogWarning("[KinematicController2D] Sprite references the root player GO. " +
                                 "We will hide renderers instead of deactivating the GameObject to keep coroutines alive.");
            }
        }
    }

    Rect PlayerRectAt(Vector2 pos)
    {
        Vector2 c = pos + bodyOffset;
        Vector2 half = size * 0.5f;
        return new Rect(c - half, size);
    }

    void Update()
    {
        if (isDead) return; // keep script active so coroutines run

        if (isCompleting) return;

        // Horizontal run + gravity
        Velocity = new Vector2(moveSpeedX, Velocity.y);
        Velocity = new Vector2(Velocity.x, Velocity.y + gravity * Time.deltaTime);

        // Input
        bool jumpHeld = false;
        bool jumpPressedThisFrame = false;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            jumpHeld = Keyboard.current.spaceKey.isPressed || Keyboard.current.enterKey.isPressed;
            jumpPressedThisFrame = Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame;
        }
        if (Mouse.current != null)
        {
            jumpHeld = jumpHeld || Mouse.current.leftButton.isPressed;
            jumpPressedThisFrame = jumpPressedThisFrame || Mouse.current.leftButton.wasPressedThisFrame;
        }
#else
        jumpHeld = Input.GetKey(KeyCode.Space) || Input.GetMouseButton(0) || Input.GetKey(KeyCode.Return);
        jumpPressedThisFrame = Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Return);
#endif

        // Jump buffers
        if (jumpPressedThisFrame) jumpBufferCounter = jumpBufferTime;
        else jumpBufferCounter = Mathf.Max(0f, jumpBufferCounter - Time.deltaTime);

        if (IsGrounded) coyoteCounter = coyoteTime;
        else coyoteCounter = Mathf.Max(0f, coyoteCounter - Time.deltaTime);

        bool canJumpNow = IsGrounded || coyoteCounter > 0f;
        bool wantJump = (jumpHeld && canJumpNow) || (jumpBufferCounter > 0f && canJumpNow);

        if (wantJump)
        {
            Velocity = new Vector2(Velocity.x, jumpSpeed);
            jumpBufferCounter = 0f;
            coyoteCounter = 0f;
            IsGrounded = false;
        }

        // Sprite spinning / snapping
        if (Sprite)
        {
            if (IsGrounded)
            {
                Vector3 Rotation = Sprite.rotation.eulerAngles;
                Rotation.z = Mathf.Round(Rotation.z / 90f) * 90f;
                Sprite.rotation = Quaternion.Euler(Rotation);
            }
            else
            {
                Sprite.Rotate(Vector3.back * 1f);
            }
        }

        // Substep move and collide
        int steps = Mathf.Max(1, maxSubsteps);
        float dt = Time.deltaTime / steps;
        if (dt <= 0f) return;
        Vector2 pos = transform.position;

        for (int i = 0; i < steps; i++)
        {
            Vector2 delta = Velocity * dt;
            MoveAxis(ref pos, new Vector2(delta.x, 0f), axis: 0);
            MoveAxis(ref pos, new Vector2(0f, delta.y), axis: 1);
            IsGrounded = CheckGrounded(pos);
            if (IsGrounded && Velocity.y < 0f) Velocity = new Vector2(Velocity.x, 0f);
        }

        transform.position = pos;

        // Hazards (with grace after respawn)
        if (IsTouchingHazard(pos))
        {
            Kill();
            return;
        }

                // Level goal
        if (IsTouchingGoal(pos, out var goalCenter))
        {
            StartLevelComplete(goalCenter);
            return;
        }
    }

    void MoveAxis(ref Vector2 pos, Vector2 delta, int axis)
    {
        if (delta[axis] == 0f)
        {
            pos += delta;
            return;
        }

        var world = SimplePhysicsWorld2D.Instance;
        if (world == null)
        {
            pos += delta;
            return;
        }

        Vector2 newPos = pos + delta;
        Rect playerRect = PlayerRectAt(newPos);

        if (world.Colliders.Count == 0)
        {
            pos = newPos;
            return;
        }

        for (int iter = 0; iter < maxSolverIters; iter++)
        {
            bool anyHit = false;

            foreach (var c in world.Colliders)
            {
                if (c == null) continue;

                Rect colliderRect = c.WorldRect;
                if (!Overlaps(playerRect, colliderRect)) continue;

                anyHit = true;
                float push = MinSeparationAlongAxis(playerRect, colliderRect, axis);
                float maxPush = delta[axis] * 2f;
                push = Mathf.Clamp(push, -maxPush, maxPush);
                Vector2 separation = axis == 0 ? new Vector2(push, 0f) : new Vector2(0f, push);

                newPos += separation;
                playerRect.position += separation;

                if (axis == 0) Velocity = new Vector2(0f, Velocity.y);
                else           Velocity = new Vector2(Velocity.x, 0f);
            }

            if (!anyHit) break;
        }

        pos = newPos;
    }

    bool Overlaps(Rect a, Rect b)
    {
        return a.xMin < b.xMax && a.xMax > b.xMin &&
               a.yMin < b.yMax && a.yMax > b.yMin;
    }

    float MinSeparationAlongAxis(Rect a, Rect b, int axis)
    {
        if (axis == 0)
        {
            float leftOverlap = b.xMax - a.xMin;
            float rightOverlap = a.xMax - b.xMin;
            return (leftOverlap < rightOverlap) ? -leftOverlap : rightOverlap;
        }
        else
        {
            float downOverlap = b.yMax - a.yMin;
            float upOverlap = a.yMax - b.yMin;
            return (downOverlap < upOverlap) ? -downOverlap : upOverlap;
        }
    }

    bool CheckGrounded(Vector2 pos)
    {
        var world = SimplePhysicsWorld2D.Instance;
        if (world == null || world.Colliders.Count == 0) return false;

        Rect feet = PlayerRectAt(pos);
        feet.yMin -= groundCheckPadding;
        feet.yMax = feet.yMin + groundCheckPadding * 2f;

        foreach (var c in world.Colliders)
        {
            if (c == null) continue;
            Rect colliderRect = c.WorldRect;
            bool xOverlap = feet.xMin < colliderRect.xMax && feet.xMax > colliderRect.xMin;
            bool feetBelowTop = feet.yMin <= colliderRect.yMax + 0.001f;
            bool playerAbove = pos.y >= colliderRect.yMax - 0.001f;

            if (xOverlap && feetBelowTop && playerAbove)
                return true;
        }
        return false;
    }

    void OnDrawGizmos()
    {
        var r = PlayerRectAt((Vector2)transform.position);
        Gizmos.color = IsGrounded ? Color.green : Color.red;
        Gizmos.DrawWireCube(r.center, r.size);
    }

    bool IsTouchingHazard(Vector2 pos)
    {
        if (Time.unscaledTime < hazardIgnoreUntil) return false;

        var world = SimplePhysicsWorld2D.Instance;
        if (world == null || world.Hazards.Count == 0) return false;

        Rect playerRect = PlayerRectAt(pos);
        foreach (var h in world.Hazards)
        {
            if (h == null) continue;
            if (Overlaps(playerRect, h.WorldRect))
                return true;
        }
        return false;
    }

        bool IsTouchingGoal(Vector2 pos, out Vector2 goalCenter)
    {
        goalCenter = Vector2.zero;

        var world = SimplePhysicsWorld2D.Instance;
        if (world == null || world.Goals.Count == 0) return false;

        Rect playerRect = PlayerRectAt(pos);
        foreach (var g in world.Goals)
        {
            if (g == null) continue;
            var r = g.WorldRect;
            if (Overlaps(playerRect, r))
            {
                goalCenter = r.center;
                return true;
            }
        }
        return false;
    }

    void Kill()
    {
        if (isDead) return;
        isDead = true;

        // Defensive: keep GO active; we control visibility via renderers
        SetSpriteVisible(true);

        Velocity = Vector2.zero;
        IsGrounded = false;

        // Watchdog in case anything stalls before reload/respawn
        StartCoroutine(DeathWatchdogRealtime(1.2f));

        if (useParticleExplosion)
        {
            StartCoroutine(ExplosionAndThen());
            return;
        }

        if (reloadSceneOnDeath) ReloadScene();
        else RespawnAndReset();
    }

    IEnumerator ExplosionAndThen()
    {
        Debug.Log("[Death] ExplosionAndThen started");

        // SAFER: hide renderers (do NOT SetActive(false) on the GO)
        SetSpriteVisible(false);
        CreateAndPlayExplosion();

        // Use realtime so it completes even if timeScale == 0
        yield return new WaitForSecondsRealtime(0.6f);

        Debug.Log("[Death] Explosion wait done");

        try
        {
            if (reloadSceneOnDeath) ReloadScene();
            else RespawnAndReset();
        }
        catch (System.Exception e)
        {
            Debug.LogError("[Death] ExplosionAndThen exception: " + e.Message);
            // Fallback
            if (reloadSceneOnDeath) ReloadScene();
            else RespawnAndReset();
        }
    }

    IEnumerator DeathWatchdogRealtime(float timeoutSeconds)
    {
        float start = Time.realtimeSinceStartup;
        while (isDead && (Time.realtimeSinceStartup - start) < timeoutSeconds)
            yield return null;

        if (isDead)
        {
            Debug.LogWarning("[Death] Watchdog fired -> forcing recovery");
            if (reloadSceneOnDeath) ReloadScene();
            else RespawnAndReset();
        }
    }

    void ReloadScene()
    {
        Debug.Log("[Death] ReloadScene()");
        try
        {
            var s = SceneManager.GetActiveScene();
            SceneManager.LoadScene(s.buildIndex);
        }
        catch (System.Exception e)
        {
            Debug.LogError("[Death] Error reloading scene: " + e.Message + " -> Falling back to respawn.");
            RespawnAndReset();
        }
    }

    void RespawnAndReset()
    {
        Debug.Log("[Death] RespawnAndReset()");
        transform.position = (Vector3)respawnPoint;

        // Re-show renderers (not SetActive on GO)
        SetSpriteVisible(true);

        if (spriteRenderer)
        {
            // Restore alpha to 1 and previous material (fixes “faded” look)
            var col = spriteRenderer.color; col.a = 1f; spriteRenderer.color = col;

            if (cachedSpriteMaterial != null)
                spriteRenderer.sharedMaterial = cachedSpriteMaterial;
        }

        if (Sprite)
        {
            var rot = Sprite.rotation.eulerAngles;
            rot.z = Mathf.Round(rot.z / 90f) * 90f;
            Sprite.rotation = Quaternion.Euler(rot);
        }

        Velocity = Vector2.zero;
        IsGrounded = false;
        jumpBufferCounter = 0f;
        coyoteCounter = 0f;

        // Avoid instant re-death if spawn is near a hazard
        hazardIgnoreUntil = Time.unscaledTime + 0.25f;

        isDead = false;
    }

    // ---- Renderer visibility helper (prevents killing coroutines) ----
    void SetSpriteVisible(bool visible)
    {
        if (!Sprite) return;
        var rends = Sprite.GetComponentsInChildren<Renderer>(true);
        foreach (var r in rends) r.enabled = visible;
    }

    void CreateAndPlayExplosion()
    {
        GameObject explosionObj = new GameObject("PlayerExplosion");
        var ps = explosionObj.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 0.6f;
        main.loop = false;
        main.startLifetime = 0.5f;
        main.startSpeed = 8f;
        main.startSize = 0.3f;
        main.startColor = Color.yellow;
        main.gravityModifier = 0.3f;
        main.maxParticles = 60;
        main.playOnAwake = false;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.0f, 40, 60, 1, 0.01f) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.2f;

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(Color.yellow, 0.0f),
                new GradientColorKey(Color.red, 0.5f),
                new GradientColorKey(new Color(1f, 0f, 0f, 0f), 1.0f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(1.0f, 0.0f),
                new GradientAlphaKey(0.0f, 1.0f)
            }
        );
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

        var renderer = explosionObj.GetComponent<ParticleSystemRenderer>();
        Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (sh == null) sh = Shader.Find("Particles/Standard Unlit");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        if (sh != null)
        {
            var mat = new Material(sh);
            renderer.material = mat;
            renderer.material.color = Color.yellow;
        }

        explosionObj.transform.position = transform.position;
        ps.Play();
        Destroy(explosionObj, 1.0f);
    }

        void StartLevelComplete(Vector2 goalCenter)
    {
        if (isCompleting || isDead) return;
        isCompleting = true;

        // Freeze physics control
        Velocity = Vector2.zero;

        // UI message
        if (LevelUI.Instance) LevelUI.Instance.ShowCompleted("¡Nivel Completado!");

        // Suck slightly inside the wall so it feels like an opening
        Vector2 suckTarget = goalCenter + new Vector2(0.2f, 0f);

        // Use realtime so it works even if timeScale changes
        StartCoroutine(SuckIntoWallAndFinish(suckTarget));
    }

    System.Collections.IEnumerator SuckIntoWallAndFinish(Vector2 target)
    {
        // Optional: ignore hazards while completing
        hazardIgnoreUntil = Time.unscaledTime + suckDuration + 0.5f;

        float t = 0f;
        Vector3 startPos = transform.position;
        Vector3 startScale = Sprite ? Sprite.localScale : Vector3.one;
        Vector3 endScale = startScale * suckScaleEnd;

        while (t < suckDuration)
        {
            float dt = Time.unscaledDeltaTime;
            t += dt;

            // Ease-in curve
            float u = t / suckDuration;
            u = u * u * (3f - 2f * u); // smoothstep

            // Lerp position
            transform.position = Vector3.Lerp(startPos, target, u);

            // Spin + shrink sprite
            if (Sprite)
            {
                Sprite.Rotate(Vector3.forward, -suckSpinSpeed * dt, Space.Self);
                Sprite.localScale = Vector3.Lerp(startScale, endScale, u);
            }

            yield return null;
        }

        // Snap to final
        transform.position = target;
        if (Sprite)
        {
            Sprite.localScale = endScale;
            var rot = Sprite.rotation.eulerAngles; rot.z = Mathf.Round(rot.z / 90f) * 90f;
            Sprite.rotation = Quaternion.Euler(rot);
        }

        // Finish: either next scene or wait a moment then reload/respawn
        yield return new WaitForSecondsRealtime(0.3f);

        if (loadNextSceneOnComplete && !string.IsNullOrEmpty(nextSceneName))
        {
            try { SceneManager.LoadScene(nextSceneName); }
            catch { /* fallback */ }
        }
        else
        {
            // If you want to stop here (level editor/testing), just keep the state.
            // Or reload scene to replay:
            // SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }

}