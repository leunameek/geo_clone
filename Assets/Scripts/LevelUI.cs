using UnityEngine;
using TMPro;

public class LevelUI : MonoBehaviour
{
    public static LevelUI Instance { get; private set; }
    // singleton feo pero funcional, UI global del nivel
    // básicamente todos llaman LevelUI.Instance para mostrar mensajitos

    public TextMeshProUGUI messageText;
    // referencia al texto en pantalla (TMP), donde ponemos “nivel completado” o lo q quieras

    public float fadeInTime = 0.25f;
    // tiempo que dura la anim de aparecer el texto, tipo “fundido”

    void Awake()
    {
        Instance = this; // nos autoproclamamos como la instancia actual jeje

        if (messageText)
        {
            // poner alpha = 0 pa que inicie invisible
            var c = messageText.color;
            c.a = 0f; // transparencia total
            messageText.color = c;
        }
    }

    public void ShowCompleted(string text = "¡Nivel completado!")
    {
        // muestra texto de nivel completado (o lo que pases)
        if (!messageText) return;

        messageText.text = text; // cambiamos el string del TMP

        StopAllCoroutines(); // por si ya habia un fade anterior
        StartCoroutine(FadeIn()); // arrancamos transición suavita
    }

    System.Collections.IEnumerator FadeIn()
    {
        // esta corutina hace el “uuh aparece suavecito”
        float t = 0f;

        Color start = messageText.color; 
        start.a = 0f;   // arranca transparente

        Color end = messageText.color;   
        end.a = 1f;     // termina opaco

        while (t < fadeInTime)
        {
            t += Time.unscaledDeltaTime; 
            // usamos unscaledDeltaTime para que funcione incluso si el tiempo está pausado o en slow motion

            messageText.color = Color.Lerp(start, end, t / fadeInTime);
            // Lerp = interpolación lineal: (1-u)*start + u*end
            // donde u = t / fadeInTime

            yield return null; // esperamos 1 frame
        }

        messageText.color = end; // por si acaso, dejamos el color final exacto
    }
}