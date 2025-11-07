using UnityEngine;

[ExecuteAlways]
public class StaticRectCollider2D : MonoBehaviour
{
    public Vector2 size = new Vector2(1, 1);
    public Vector2 offset = Vector2.zero;

    public Rect WorldRect {
        get {
            var center = (Vector2)transform.position + offset;
            var half = size * 0.5f;
            return new Rect(center - half, size);
        }
    }

    void OnEnable()   { if (SimplePhysicsWorld2D.Instance) SimplePhysicsWorld2D.Instance.Register(this); }
    void OnDisable()  { if (SimplePhysicsWorld2D.Instance) SimplePhysicsWorld2D.Instance.Unregister(this); }

    void OnDrawGizmos() {
        var r = WorldRect;
        Gizmos.color = new Color(0f, 0.6f, 1f, 0.3f);
        Gizmos.DrawCube(r.center, r.size);
        Gizmos.color = new Color(0f, 0.6f, 1f, 1f);
        Gizmos.DrawWireCube(r.center, r.size);
    }
}
