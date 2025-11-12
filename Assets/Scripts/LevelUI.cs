using UnityEngine;
using TMPro;

public class LevelUI : MonoBehaviour
{
    public static LevelUI Instance { get; private set; }
    public TextMeshProUGUI messageText;
    public float fadeInTime = 0.25f;

    void Awake()
    {
        Instance = this;
        if (messageText)
        {
            var c = messageText.color;
            c.a = 0f;
            messageText.color = c;
        }
    }

    public void ShowCompleted(string text = "¡Nivel Completado!")
    {
        if (!messageText) return;
        messageText.text = text;
        StopAllCoroutines();
        StartCoroutine(FadeIn());
    }

    System.Collections.IEnumerator FadeIn()
    {
        float t = 0f;
        Color start = messageText.color; start.a = 0f;
        Color end = messageText.color; end.a = 1f;

        while (t < fadeInTime)
        {
            t += Time.unscaledDeltaTime;
            messageText.color = Color.Lerp(start, end, t / fadeInTime);
            yield return null;
        }

        messageText.color = end;
    }
}
