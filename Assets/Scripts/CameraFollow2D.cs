using UnityEngine;

public class CameraFollow2D : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Header("Offsets")]
    public float xOffset = 2f;
    public float yOffset = 1f;

    [Header("Y Follow")]
    public float yThreshold = 2f;
    public float ySmoothTime = 0.12f;

    private float yVel;

    void LateUpdate()
    {
        if (!target) return;

        Vector3 pos = transform.position;

        pos.x = target.position.x + xOffset;

        float targetY = target.position.y + yOffset;
        float diff = targetY - pos.y;

        if (Mathf.Abs(diff) > yThreshold)
        {
            float desiredY = targetY - Mathf.Sign(diff) * yThreshold;
            pos.y = Mathf.SmoothDamp(pos.y, desiredY, ref yVel, ySmoothTime);
        }

        transform.position = pos;
    }
}
