using UnityEngine;

[ExecuteAlways] 
// esto hace que el script corra incluso en modo editor (no solo jugando),
// asi puedes ver las cajas de colisión todo el tiempo
public class StaticRectCollider2D : MonoBehaviour
{
    public Vector2 size = new Vector2(1, 1);
    // tamaño del bloque de colisión (AABB)
//  matematicamente: size.x = ancho, size.y = alto

    public Vector2 offset = Vector2.zero;
    // pequeño desplazamiento desde el pivot del objeto
    // sirve para centrar el collider si el sprite no está centrado o si quieres ajustarlo

    public Rect WorldRect {
        get {
            // la "posición real" del collider es (posición del objeto + offset)
            var center = (Vector2)transform.position + offset;

            // half = la mitad del ancho y la mitad del alto
            // porque los rectángulos se suelen definir desde el centro
            var half = size * 0.5f;

            // Rect espera esquina_Inferior_Izquierda y tamaño
            // esquina = centro - half
            // tamaño = size (obvio)
            return new Rect(center - half, size);
        }
    }

    void OnEnable()  
    {
        // cuando este GameObject se activa, lo registramos en el "mundo" de física simple
        if (SimplePhysicsWorld2D.Instance) 
            SimplePhysicsWorld2D.Instance.Register(this);
    }

    void OnDisable()  
    {
        // cuando se desactiva, lo sacamos del registro
        if (SimplePhysicsWorld2D.Instance) 
            SimplePhysicsWorld2D.Instance.Unregister(this);
    }

    void OnDrawGizmos() 
    {
        // esto solo dibuja el rectángulo en la escena para debug
        var r = WorldRect;

        // Azul clarito transparente
        Gizmos.color = new Color(0f, 0.6f, 1f, 0.3f);
        Gizmos.DrawCube(r.center, r.size);

        // Y los bordes más opacos para que se note bien el contorno
        Gizmos.color = new Color(0f, 0.6f, 1f, 1f);
        Gizmos.DrawWireCube(r.center, r.size);
    }
}
