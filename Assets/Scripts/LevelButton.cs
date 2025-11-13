using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Button))]
public class LevelButton : MonoBehaviour
{
    [Tooltip("Nombre de la escena que se va a cargar cuando se presione este botón")]
    public string sceneName;
    // nota: es el nombre de la escena tal cual aparece en el Build Settings
    // si lo escribes mal → boom no carga nada :v

    void Awake()
    {
        // acá agarramos el componente Button automáticamente
        // y le agregamos nuestro método OnClick como listener
        Button btn = GetComponent<Button>();
        btn.onClick.AddListener(OnClick);
    }

    void OnClick()
    {
        // cuando el usuario toca el botón, se llama OnClick()
        // entonces cargamos la escena que se configuró en el inspector

        if (!string.IsNullOrEmpty(sceneName))
        {
            SceneManager.LoadScene(sceneName);
            // carga la escena de forma directa (sin transiciones fancy por defecto)
        }
        else
        {
            // si olvidaste poner el nombre de la escena, te avisa con un warning
            Debug.LogWarning($"[LevelButton] No hay sceneName asignado en el objeto {gameObject.name}");
        }
    }
}