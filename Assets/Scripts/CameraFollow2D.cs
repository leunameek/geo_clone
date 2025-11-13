using UnityEngine;

public class CameraFollow2D : MonoBehaviour
{
    [Header("Objetivo")]
    public Transform target; 
    // el jugador (o lo que sea) que la cámara va siguiendo

    [Header("Offsets")]
    public float xOffset = 2f;
    public float yOffset = 1f;
    // offsets = cuanto desplazamos la cámara respecto al target
    // básicamente pos_cam = pos_target + offset (más o menos)

    [Header("Seguimiento en Y")]
    public float yThreshold = 2f;
    // este threshold es como un “margen de tolerancia”
    // la camara NO se mueve en Y mientras el target esté dentro de esta banda vertical

    public float ySmoothTime = 0.12f;
    // tiempo usado por SmoothDamp para suavizar la transición vertical

    private float yVel; 
    // variable interna usada por SmoothDamp, no la toques

    void LateUpdate()
    {
        // LateUpdate porque es mejor mover la cámara DESPUÉS de que se movió el jugador
        if (!target) return;

        Vector3 pos = transform.position;

        // --- Movimiento en X ---
        // seguimiento super simple: solo copiamos la X del target + offset
        pos.x = target.position.x + xOffset;

        // --- Movimiento inteligente en Y ---
        float targetY = target.position.y + yOffset;
        float diff = targetY - pos.y;
        // diff = qué tan lejos está la cámara del Y que debería tener

        // Si estamos dentro del margen, no movemos Y pa no marear al jugador
        if (Mathf.Abs(diff) > yThreshold)
        {
            // movemos la camara hacia targetY pero restando el threshold
            // pa que no esté pegada encima del personaje
            float desiredY = targetY - Mathf.Sign(diff) * yThreshold;

            // SmoothDamp: mueve pos.y hacia desiredY de forma suave, desacelerada
            pos.y = Mathf.SmoothDamp(pos.y, desiredY, ref yVel, ySmoothTime);
        }

        transform.position = pos;
        // actualizamos la posición final de la cámara
    }
}