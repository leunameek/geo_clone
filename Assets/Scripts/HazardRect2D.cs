using UnityEngine;

[ExecuteAlways]
public class HazardRect2D : MonoBehaviour
{
    public Vector2 size = new Vector2(1, 1);
    // tamaño del “cajón” del peligro, ancho (x) y alto (y)
    // en realidad es un AABB, o sea un rectangulo alineado con los ejes

    public Vector2 offset = Vector2.zero;
    // esto desplaza el centro del hazard respecto a la posicion del objeto
    // centro_real = transform.position + offset (en 2D)

    public Rect WorldRect {
        get {
            // aca sacamos el RECTAUNGLO en coordenadas mundiales
            // 1) centro = posicion + offset
            var center = (Vector2)transform.position + offset;

            // 2) half = size * 0.5  => mitad del ancho y mitad del alto
            var half = size * 0.5f;

            // 3) Rect en Unity se construye con la esquina minima (bottom-left) y el tamaño
            //    esquinaInferiorIzq = center - half
            //    entonces queda un rectangulito centrado en “center”
            return new Rect(center - half, size);
        }
    }

    void OnEnable()  
    { 
        // cuando se activa el objeto en la escena, lo registramos en el "mundo" de física
        // esto permite que el player lo vea como hazard al chequear colisiones
        if (SimplePhysicsWorld2D.Instance) 
            SimplePhysicsWorld2D.Instance.Register(this); 
    }

    void OnDisable() 
    { 
        // cuando se desactiva / destruye, lo quitamos de la lista
        if (SimplePhysicsWorld2D.Instance) 
            SimplePhysicsWorld2D.Instance.Unregister(this); 
    }

    void OnDrawGizmos() {
        // dibujamos el hazard en la vista de escena para debug
        var r = WorldRect;

        // primero un cubo rojo semi-transparente para ver el área rellena
        Gizmos.color = new Color(1f, 0f, 0f, 0.25f);
        Gizmos.DrawCube(r.center, r.size);

        // luego el borde del cuadro en rojo opaco, para ver bien los limites
        Gizmos.color = new Color(1f, 0f, 0f, 1f);
        Gizmos.DrawWireCube(r.center, r.size);
    }
}
