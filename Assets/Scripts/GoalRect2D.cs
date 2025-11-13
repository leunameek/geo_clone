using UnityEngine;

[ExecuteAlways] 
// esto hace que el script corra también en el editor, así puedes ver la zona de meta incluso sin darle Play
public class GoalRect2D : MonoBehaviour
{
    public Vector2 size = new Vector2(1, 1);
    // tamaño del rectángulo que marca la meta (AABB: rectángulo alineado a ejes)

    public Vector2 offset = Vector2.zero;
    // desplazamiento para mover la meta un poquito sin mover el GameObject entero

    public Rect WorldRect {
        get {
            // igual que en los otros colliders: sacamos el rectángulo en espacio mundial
            var center = (Vector2)transform.position + offset;
            var half   = size * 0.5f;
            // Rect pide esquina inferior izquierda → center - half
            return new Rect(center - half, size);
        }
    }

    void OnEnable()  
    {
        // cuando el objeto se activa, lo registramos en el mundo simple de física
        if (SimplePhysicsWorld2D.Instance) 
            SimplePhysicsWorld2D.Instance.Register(this);
    }

    void OnDisable() 
    {
        // y cuando se desactiva, lo sacamos
        if (SimplePhysicsWorld2D.Instance) 
            SimplePhysicsWorld2D.Instance.Unregister(this);
    }

    void OnDrawGizmos() 
    {
        // dibujito debug para ver dónde está la meta y su tamaño
        var r = WorldRect;

        // relleno verde semitransparente
        Gizmos.color = new Color(0f, 1f, 0.2f, 0.25f);
        Gizmos.DrawCube(r.center, r.size);

        // borde verde más fuerte
        Gizmos.color = new Color(0f, 1f, 0.2f, 1f);
        Gizmos.DrawWireCube(r.center, r.size);
    }
}