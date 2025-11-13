using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class HotspotButton : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Escena a cargar")]
    public string sceneName;
    // nombre de la escena que queremos abrir cuando el usuario hace clic
    // (igual que siempre: tiene que estar en Build Settings o Unity no la encuentra)

    [Header("Feedback opcional (cuando pasas el mouse)")]
    public bool highlightWhenHover = false;
    public Color hoverColor = new Color(1, 1, 1, 0.2f); 
    // color semitransparente para mostrar “estoy encima”

    private Image debugImage; 
    // image fantasma (opcional) que usamos pa pintar un fondito cuando se hace hover

    void Start()
    {
        if (highlightWhenHover)
        {
            // buscamos si ya tiene un Image, si no lo agregamos
            debugImage = gameObject.GetComponent<Image>();
            if (!debugImage)
                debugImage = gameObject.AddComponent<Image>();

            // lo ponemos invisible por defecto
            debugImage.color = new Color(0, 0, 0, 0);
            debugImage.raycastTarget = true; 
            // importante pa que pueda recibir eventos del mouse
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // cuando haces clic en el hotspot → cambiamos de escena
        if (!string.IsNullOrEmpty(sceneName))
        {
            SceneManager.LoadScene(sceneName);
        }
        else
        {
            Debug.LogWarning($"[HotspotButton] No se asignó ninguna escena en {gameObject.name}");
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        // si tienes highlight activado, cambia el color al pasar el mouse
        if (highlightWhenHover && debugImage != null)
            debugImage.color = hoverColor;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // al salir, volvemos a invisible
        if (highlightWhenHover && debugImage != null)
            debugImage.color = new Color(0, 0, 0, 0);
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        // esto dibuja el rectángulo del hotspot en la vista de escena (solo en editor)
        var rt = GetComponent<RectTransform>();
        if (rt == null) return;

        Vector3[] corners = new Vector3[4];
        rt.GetWorldCorners(corners);

        // borde naranja fuerte
        Gizmos.color = new Color(1f, 0.5f, 0f, 1f);

        Gizmos.DrawLine(corners[0], corners[1]);
        Gizmos.DrawLine(corners[1], corners[2]);
        Gizmos.DrawLine(corners[2], corners[3]);
        Gizmos.DrawLine(corners[3], corners[0]);

        // líneas hacia el centro (solo pa ver mejor el área clickable)
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.15f);
        Vector3 center = (corners[0] + corners[2]) * 0.5f;
        Gizmos.DrawLine(center, corners[0]);
        Gizmos.DrawLine(center, corners[1]);
        Gizmos.DrawLine(center, corners[2]);
        Gizmos.DrawLine(center, corners[3]);
    }
#endif
}
