using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using System.Collections;

public class KinematicController2D : MonoBehaviour
{
    [Header("Cuerpo (AABB)")]
    public Vector2 size = new Vector2(0.9f, 0.9f); 
    // tamaño del cajón de colisión del jugador, es un AABB (axis aligned bounding box),
    // osea un rectangulo que no rota, siempre alineado a los ejes X/Y
    // matematicamente es solo un centro + un tamaño (width, height)

    public Vector2 bodyOffset = Vector2.zero;
    // offset del cajón respecto al pivot del objeto
    // pos_caja = pos_jugador + bodyOffset

    public Transform Sprite;
    // esto es solo lo visual, el gráfico. la colisión se hace con el AABB, no con el sprite
    // por eso podemos rotar el sprite sin dañar la “física”


    [Header("Respawn / Muerte")]
    public Vector2 respawnPoint;
    // punto donde reaparece el player cuando muere (si no recargamos escena)
    public bool reloadSceneOnDeath = false;
    public bool useParticleExplosion = true;

    [Header("Nivel completado")]
    public float suckDuration = 0.6f;
    public float suckSpinSpeed = 900f;  // deg/s
    public float suckScaleEnd = 0.1f;   // que tan pequeño queda el sprite al final


    [Header("Movimiento")]
    public float gravity = -60f;
    // gravedad en unidades por segundo^2 (negativa porque en Unity hacia abajo es -Y)
    public float jumpSpeed = 22f;
    // velocidad inicial del salto (vy = jumpSpeed cuando saltas)
    public float moveSpeedX = 12f;
    // velocidad constante hacia la derecha, como un auto-runner
    public int   maxSolverIters = 8;
    // cuantas veces intentamos “desenredar” colisiones en un eje
    public int   maxSubsteps = 1;
    // cuantas subiteraciones de movimiento hacemos por frame (sub-stepping)

    [Header("Detección suelo")]
    public float groundCheckPadding = 0.02f;
    // como un margencito para chequear el piso debajo de los pies

    [Header("Ayudas de salto")]
    public float jumpBufferTime = 0.10f;
    // tiempo que guardamos el “intento de salto” antes de tocar suelo (jump buffer)
    public float coyoteTime = 0.06f;
    // tiempo que puedes saltar después de haber dejado el borde (coyote)

    private Vector2 velocity;
    // vector velocidad (vx, vy). Cada frame hacemos x += vx * dt, y += vy * dt
    public Vector2 Velocity { get { return velocity; } private set { velocity = value; } }
    public bool IsGrounded { get; private set; }
    // si pisamos el piso o no (bool simple, no magia)

    float jumpBufferCounter = 0f;
    float coyoteCounter = 0f;

    // Muerte / peligros
    private bool isDead = false;
    private float hazardIgnoreUntil = 0f; // tiempo en realtime hasta el cual ignoramos hazards

    // cache visual (para bug de sprite “desvanecido”)
    private SpriteRenderer spriteRenderer;
    private Material cachedSpriteMaterial;
    private bool isCompleting = false;
    private Color cachedSpriteColor;

    void Awake()
    {
        velocity = Vector2.zero;
        IsGrounded = false;
        if (respawnPoint == Vector2.zero)
            respawnPoint = transform.position; // si no pones respawn en el inspector, usa donde empezó

        if (Sprite)
        {
            spriteRenderer = Sprite.GetComponent<SpriteRenderer>();
            if (spriteRenderer)
            {
                cachedSpriteMaterial = spriteRenderer.sharedMaterial;
                cachedSpriteColor    = spriteRenderer.color;
            }

            // advertencia si usas el mismo GO como sprite y controlador
            if (Sprite.gameObject == this.gameObject)
            {
                Debug.LogWarning("[KinematicController2D] El Sprite referencia el mismo objeto del jugador. " +
                                 "Se van a ocultar los renderers en vez de desactivar el GameObject para mantener corutinas vivas.");
            }
        }
    }

    Rect PlayerRectAt(Vector2 pos)
    {
        // Matematicamente:
        // centro_caja = pos + bodyOffset
        // half = size * 0.5
        // esquina_min = centro - half
        // Rect usa (pos_min, size)
        Vector2 c = pos + bodyOffset;
        Vector2 half = size * 0.5f;
        return new Rect(c - half, size);
    }

    void Update()
    {
        if (isDead) return; // no actualizamos lógica de jugador si está muerto, pero corutinas siguen

        if (isCompleting) return; // si estamos en anim de nivel completado, congelamos control

        // --- FÍSICA: actualizamos velocidad (v = v + a * dt) ---
        // en X: runner constante, no aceleramos, solo fijamos vx
        Velocity = new Vector2(moveSpeedX, Velocity.y);

        // en Y: Euler explícito simple: vy = vy + g * dt
        Velocity = new Vector2(Velocity.x, Velocity.y + gravity * Time.deltaTime);

        // --- INPUT salto ---
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

        // --- jump buffer: si presionas salto, guardamos ese intento un ratito ---
        if (jumpPressedThisFrame) 
            jumpBufferCounter = jumpBufferTime; // resetea
        else 
            jumpBufferCounter = Mathf.Max(0f, jumpBufferCounter - Time.deltaTime);
        // contador baja lineal con el tiempo hasta 0

        // --- coyote time: guardamos que estuviste en el piso hace poquito ---
        if (IsGrounded) 
            coyoteCounter = coyoteTime;
        else 
            coyoteCounter = Mathf.Max(0f, coyoteCounter - Time.deltaTime);

        bool canJumpNow = IsGrounded || coyoteCounter > 0f;
        // logica de “te dejo saltar si estuviste tocando el piso hace un poquito”

        bool wantJump = (jumpHeld && canJumpNow) || (jumpBufferCounter > 0f && canJumpNow);
        // si mantienes salto y se puede, o si el buffer de salto está activo, entonces saltas

        if (wantJump)
        {
            // aquí es donde metemos la parte “balística”:
            // en vez de aplicar una fuerza, simplemente seteamos vy = jumpSpeed
            Velocity = new Vector2(Velocity.x, jumpSpeed);
            jumpBufferCounter = 0f;
            coyoteCounter = 0f;
            IsGrounded = false;
        }

        // --- lógica del sprite (rotar y encajar) ---
        if (Sprite)
        {
            if (IsGrounded)
            {
                // redondeamos rotación a múltiplos de 90° para que se vea como un cubo que cayó exacto
                Vector3 Rotation = Sprite.rotation.eulerAngles;
                Rotation.z = Mathf.Round(Rotation.z / 90f) * 90f; // round(z/90)*90
                Sprite.rotation = Quaternion.Euler(Rotation);
            }
            else
            {
                // en el aire, rotamos un poquito cada frame
                Sprite.Rotate(Vector3.back * 1f); // 1 grado aprox por frame, depende fps
            }
        }

        // --- Sub-stepping: dividimos el movimiento grande en varios pequeños ---
        int steps = Mathf.Max(1, maxSubsteps);
        float dt = Time.deltaTime / steps;
        if (dt <= 0f) return;
        Vector2 pos = transform.position;

        for (int i = 0; i < steps; i++)
        {
            // desplazamiento = velocidad * dt
            Vector2 delta = Velocity * dt;

            // movemos primero en X y resolvemos colisión en X
            MoveAxis(ref pos, new Vector2(delta.x, 0f), axis: 0);
            // luego en Y (para evitar resolver en diagonal que es más enredado)
            MoveAxis(ref pos, new Vector2(0f, delta.y), axis: 1);

            // chequeamos si al final de este substep pisamos el suelo
            IsGrounded = CheckGrounded(pos);

            // si estamos cayendo (vy < 0) y tocamos suelo, frenamos vy
            if (IsGrounded && Velocity.y < 0f) 
                Velocity = new Vector2(Velocity.x, 0f);
        }

        transform.position = pos;

        // --- hazards (peligros que matan al tocar) ---
        if (IsTouchingHazard(pos))
        {
            Kill();
            return;
        }

        // --- meta / objetivo del nivel ---
        if (IsTouchingGoal(pos, out var goalCenter))
        {
            StartLevelComplete(goalCenter);
            return;
        }
    }

    void MoveAxis(ref Vector2 pos, Vector2 delta, int axis)
    {
        // mueve en un solo eje (0 = X, 1 = Y) y corrige colisiones empujando lo mínimo
        // idea matematica:
        //   pos_nueva = pos + delta
        //   si playerRect solapa con algun colliderRect:
        //       calculamos push_min para separar justo lo suficiente en ese eje

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

                // push = empuje mínimo calculado según dirección y solape
                float push = MinSeparationAlongAxis(playerRect, colliderRect, axis);

                // capeo el push para evitar saltos locos si delta es pequeño
                float maxPush = delta[axis] * 2f;
                push = Mathf.Clamp(push, -maxPush, maxPush);

                Vector2 separation = axis == 0 ? new Vector2(push, 0f) : new Vector2(0f, push);

                newPos += separation;
                playerRect.position += separation;

                // si chocamos en X => vx = 0, si es en Y => vy = 0
                if (axis == 0) Velocity = new Vector2(0f, Velocity.y);
                else           Velocity = new Vector2(Velocity.x, 0f);
            }

            if (!anyHit) break; // ya no hay choques, salimos del bucle iterador
        }

        pos = newPos;
    }

    bool Overlaps(Rect a, Rect b)
    {
        // condición tipica de solape entre dos AABB:
        // en X: a_min < b_max && a_max > b_min
        // en Y: a_min < b_max && a_max > b_min
        return a.xMin < b.xMax && a.xMax > b.xMin &&
               a.yMin < b.yMax && a.yMax > b.yMin;
    }

    float MinSeparationAlongAxis(Rect a, Rect b, int axis)
    {
        // Aquí calculamos el empuje mínimo para separar A de B SOLO en un eje
        // idea: medimos cuanto se “meten” por la izquierda y por la derecha
        // y usamos el más pequeño (para que el movimiento sea mínimo)

        if (axis == 0)
        {
            // eje X
            float leftOverlap  = b.xMax - a.xMin; // empuje para mover A hacia -X
            float rightOverlap = a.xMax - b.xMin; // empuje para mover A hacia +X
            return (leftOverlap < rightOverlap) ? -leftOverlap : rightOverlap;
        }
        else
        {
            // eje Y
            float downOverlap = b.yMax - a.yMin; // empuje hacia abajo
            float upOverlap   = a.yMax - b.yMin; // empuje hacia arriba
            return (downOverlap < upOverlap) ? -downOverlap : upOverlap;
        }
    }

    bool CheckGrounded(Vector2 pos)
    {
        var world = SimplePhysicsWorld2D.Instance;
        if (world == null || world.Colliders.Count == 0) return false;

        // sacamos el rectángulo de los pies:
        Rect feet = PlayerRectAt(pos);
        feet.yMin -= groundCheckPadding;
        feet.yMax = feet.yMin + groundCheckPadding * 2f; // franja finita debajo del jugador

        foreach (var c in world.Colliders)
        {
            if (c == null) continue;
            Rect colliderRect = c.WorldRect;

            bool xOverlap = feet.xMin < colliderRect.xMax && feet.xMax > colliderRect.xMin;
            bool feetBelowTop = feet.yMin <= colliderRect.yMax + 0.001f;
            bool playerAbove = pos.y >= colliderRect.yMax - 0.001f;

            // estamos “parados” cuando los pies se solapan en X con el collider,
            // los pies están justo por debajo del top y el centro del jugador está arriba
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
        // esto es solo debug visual en la escena, dibuja el AABB del jugador
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

        if (LevelMusicManager.Instance)
            LevelMusicManager.Instance.OnPlayerDied();

        // mantenemos el GO activo, controlamos visibilidad por renderers
        SetSpriteVisible(true);

        Velocity = Vector2.zero;
        IsGrounded = false;

        // watchdog por si algo falla antes del respawn/reload
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
        Debug.Log("[Muerte] Inicio ExplosionAndThen");

        // ocultamos renderers (NO hacemos SetActive(false) al GameObject)
        SetSpriteVisible(false);
        CreateAndPlayExplosion();

        // usamos tiempo real para que funcione incluso si timeScale = 0
        yield return new WaitForSecondsRealtime(0.6f);

        Debug.Log("[Muerte] Espera de explosion terminada");

        try
        {
            if (reloadSceneOnDeath) ReloadScene();
            else RespawnAndReset();
        }
        catch (System.Exception e)
        {
            Debug.LogError("[Muerte] Excepción en ExplosionAndThen: " + e.Message);
            // fallback por si algo raro pasa
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
            Debug.LogWarning("[Muerte] Watchdog disparado -> forzando recuperación");
            if (reloadSceneOnDeath) ReloadScene();
            else RespawnAndReset();
        }
    }

    void ReloadScene()
    {
        Debug.Log("[Muerte] RecargarEscena()");
        try
        {
            var s = SceneManager.GetActiveScene();
            SceneManager.LoadScene(s.buildIndex);
        }
        catch (System.Exception e)
        {
            Debug.LogError("[Muerte] Error recargando escena: " + e.Message + " -> se hace respawn en vez de recargar.");
            RespawnAndReset();
        }
    }

    void RespawnAndReset()
    {
        Debug.Log("[Muerte] RespawnYReset()");
        transform.position = (Vector3)respawnPoint;

        // volvemos a mostrar los renderers
        SetSpriteVisible(true);

        if (spriteRenderer)
        {
            // restaurar alpha y material original
            var col = spriteRenderer.color; col.a = 1f; spriteRenderer.color = col;

            if (cachedSpriteMaterial != null)
                spriteRenderer.sharedMaterial = cachedSpriteMaterial;
        }

        if (Sprite)
        {
            // enderezar rotación a múltiplos de 90 para que no reaparezca torcido
            var rot = Sprite.rotation.eulerAngles;
            rot.z = Mathf.Round(rot.z / 90f) * 90f;
            Sprite.rotation = Quaternion.Euler(rot);
        }

        Velocity = Vector2.zero;
        IsGrounded = false;
        jumpBufferCounter = 0f;
        coyoteCounter = 0f;

        // pequeño “escudo” para no morir instantmente si hay un hazard al lado del respawn
        hazardIgnoreUntil = Time.unscaledTime + 0.25f;

        if (LevelMusicManager.Instance && !reloadSceneOnDeath)
            LevelMusicManager.Instance.OnPlayerRespawned();

        isDead = false;
    }

    // ---- helper para visibilidad de renderers (no matamos corutinas) ----
    void SetSpriteVisible(bool visible)
    {
        if (!Sprite) return;
        var rends = Sprite.GetComponentsInChildren<Renderer>(true);
        foreach (var r in rends) r.enabled = visible;
    }

    void CreateAndPlayExplosion()
    {
        GameObject explosionObj = new GameObject("ExplosionJugador");
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

        // congelamos la “física”
        Velocity = Vector2.zero;

        if (LevelMusicManager.Instance)
            LevelMusicManager.Instance.OnLevelCompleted();

        // Mensaje de UI
        if (LevelUI.Instance) LevelUI.Instance.ShowCompleted("¡Nivel completado!");

        // metemos un poco al jugador dentro de la meta para que parezca como si se lo tragara
        Vector2 suckTarget = goalCenter + new Vector2(0.2f, 0f);

        // usamos tiempo real por si cambian el timeScale
        StartCoroutine(SuckIntoWallAndFinish(suckTarget));
    }

    System.Collections.IEnumerator SuckIntoWallAndFinish(Vector2 target)
    {
        // ignorar hazards mientras hacemos la animación de final
        hazardIgnoreUntil = Time.unscaledTime + suckDuration + 0.5f;

        float t = 0f;
        Vector3 startPos = transform.position;
        Vector3 startScale = Sprite ? Sprite.localScale : Vector3.one;
        Vector3 endScale = startScale * suckScaleEnd;

        while (t < suckDuration)
        {
            float dt = Time.unscaledDeltaTime;
            t += dt;

            // u = t/T (tiempo normalizado)
            float u = t / suckDuration;

            // aplicamos curva smoothstep: u = u² (3 - 2u)
            u = u * u * (3f - 2f * u);

            // Lerp de posición entre inicio y target
            transform.position = Vector3.Lerp(startPos, target, u);

            // rotar + encoger sprite
            if (Sprite)
            {
                Sprite.Rotate(Vector3.forward, -suckSpinSpeed * dt, Space.Self);
                Sprite.localScale = Vector3.Lerp(startScale, endScale, u);
            }

            yield return null;
        }

        // ajuste final
        transform.position = target;
        if (Sprite)
        {
            Sprite.localScale = endScale;
            var rot = Sprite.rotation.eulerAngles; 
            rot.z = Mathf.Round(rot.z / 90f) * 90f;
            Sprite.rotation = Quaternion.Euler(rot);
        }

        // pequeño delay por si luego quieres cargar otra escena o mostrar algo más
        yield return new WaitForSecondsRealtime(0.3f);
    }

}