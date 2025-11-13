using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelMusicManager : MonoBehaviour
{
    public static LevelMusicManager Instance { get; private set; }
    // singleton basicote para manejar la música del nivel.
    // básicamente solo existe UNO por escena, y el Player le avisa cuando muere,
    // respawnea, o termina el nivel.

    [Header("Ajustes de música")]
    [Tooltip("AudioSource que va a reproducir la música del nivel.")]
    public AudioSource musicSource;

    [Tooltip("Canción para este nivel (por ejemplo, algo en Assets/Audio).")]
    public AudioClip levelMusic;

    [Tooltip("Si está activo, la música empieza sola al cargar la escena.")]
    public bool playOnStart = true;

    void Awake()
    {
        // sistema tipo “si ya hay uno, destruyo este para que solo quede un manager”
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // si no se asignó el AudioSource en el inspector, lo buscamos en el mismo GO
        if (!musicSource)
            musicSource = GetComponent<AudioSource>();

        // si hay música asignada en este script, la copiamos al AudioSource
        if (musicSource && levelMusic && musicSource.clip != levelMusic)
            musicSource.clip = levelMusic;
    }

    void Start()
    {
        // si está marcado “playOnStart”, pues arrancamos de una
        if (playOnStart)
            PlayFromStart();
    }

    // ---- API pública para que el Player llame ----

    /// <summary>Reproducir la canción desde cero (reinicio total).</summary>
    public void PlayFromStart()
    {
        if (!musicSource) return;

        // nos aseguramos que el clip sea el que toca
        if (levelMusic && musicSource.clip != levelMusic)
            musicSource.clip = levelMusic;

        musicSource.time = 0f; // empezar desde el segundo 0
        musicSource.volume = 1f; // volumen full (puedes cambiarlo si quieres fades)
        musicSource.Play();
    }

    /// <summary>Detener la música.</summary>
    public void StopMusic()
    {
        if (!musicSource) return;
        musicSource.Stop();
    }

    /// <summary>El jugador se murió -> parar la música.</summary>
    public void OnPlayerDied()
    {
        StopMusic();
    }

    /// <summary>El jugador respawneó sin recargar la escena -> volver a empezar la música.</summary>
    public void OnPlayerRespawned()
    {
        PlayFromStart();
    }

    /// <summary>El nivel fue completado -> parar música (para transición a victoria o lo q sea).</summary>
    public void OnLevelCompleted()
    {
        StopMusic();
    }
}
