using UnityEngine;
using UnityEngine.InputSystem;

public class KinematicController2D : MonoBehaviour
{
    [Header("Body (AABB)")]
    public Vector2 size = new Vector2(0.9f, 0.9f);
    public Vector2 bodyOffset = Vector2.zero;
    public Transform Sprite;

    public Vector2 respawnPoint;
    public bool reloadSceneOnDeath = false;

    [Header("Motion")]
    public float gravity = -60f;
    public float jumpSpeed = 22f;
    public float moveSpeedX = 12f;
    public int   maxSolverIters = 8;
    public int   maxSubsteps = 1;

    [Header("Grounding")]
    public float groundCheckPadding = 0.02f;

    [Header("Jump Assist")]
    [Tooltip("Allows jump input slightly before landing to still trigger a jump.")]
    public float jumpBufferTime = 0.10f;
    [Tooltip("Allows jump slightly after walking off a ledge.")]
    public float coyoteTime = 0.06f;

    private Vector2 velocity;
    public Vector2 Velocity { get { return velocity; } private set { velocity = value; } }
    public bool IsGrounded { get; private set; }

    float jumpBufferCounter = 0f;
    float coyoteCounter = 0f;

    void Awake()
    {
        velocity = Vector2.zero;
        IsGrounded = false;
        if (respawnPoint == Vector2.zero)
            respawnPoint = transform.position;
    }

    Rect PlayerRectAt(Vector2 pos)
    {
        Vector2 c = pos + bodyOffset;
        Vector2 half = size * 0.5f;
        return new Rect(c - half, size);
    }

    void Update()
    {
        Velocity = new Vector2(moveSpeedX, Velocity.y);
        Velocity = new Vector2(Velocity.x, Velocity.y + gravity * Time.deltaTime);

        bool jumpHeld = false;
        bool jumpPressedThisFrame = false;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            jumpHeld = Keyboard.current.spaceKey.isPressed ||
                       Keyboard.current.enterKey.isPressed;
            jumpPressedThisFrame = Keyboard.current.spaceKey.wasPressedThisFrame ||
                                   Keyboard.current.enterKey.wasPressedThisFrame;
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

        if (jumpPressedThisFrame)
            jumpBufferCounter = jumpBufferTime;
        else
            jumpBufferCounter = Mathf.Max(0f, jumpBufferCounter - Time.deltaTime);

        if (IsGrounded)
            coyoteCounter = coyoteTime;
        else
            coyoteCounter = Mathf.Max(0f, coyoteCounter - Time.deltaTime);

        bool canJumpNow = IsGrounded || coyoteCounter > 0f;
        bool wantJump = (jumpHeld && canJumpNow) || (jumpBufferCounter > 0f && canJumpNow);

        if (wantJump)
        {
            Velocity = new Vector2(Velocity.x, jumpSpeed);
            jumpBufferCounter = 0f;
            coyoteCounter = 0f;
            IsGrounded = false;
        }

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

        if (IsTouchingHazard(pos))
        {
            Kill();
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

    void Kill()
    {
        // Stop motion
        Velocity = Vector2.zero;
        IsGrounded = false;

        if (reloadSceneOnDeath)
        {
            // Simple reload (requires using UnityEngine.SceneManagement;)
            var s = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            UnityEngine.SceneManagement.SceneManager.LoadScene(s.buildIndex);
            return;
        }

        // Teleport to respawn and snap sprite rotation
        transform.position = (Vector3)respawnPoint;
        var rot = Sprite ? Sprite.rotation.eulerAngles : Vector3.zero;
        rot.z = Mathf.Round(rot.z / 90f) * 90f;
        if (Sprite) Sprite.rotation = Quaternion.Euler(rot);
    }

}
