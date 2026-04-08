using UnityEngine;

// Obligamos a Unity a añadir un CanvasGroup al objeto para poder controlar la transparencia
[RequireComponent(typeof(CanvasGroup))]
public class PulsingCanvas : MonoBehaviour
{
    [Header("Configuración del Parpadeo Suave")]
    [Tooltip("Velocidad a la que respira el Canvas (menor = más relajante)")]
    public float speed = 2.5f;

    [Tooltip("Transparencia mínima (0 = invisible, 1 = totalmente opaco)")]
    [Range(0f, 1f)] public float minAlpha = 0.15f;

    [Tooltip("Transparencia máxima (0 = invisible, 1 = totalmente opaco)")]
    [Range(0f, 1f)] public float maxAlpha = 1.0f;

    private CanvasGroup canvasGroup;

    void Awake()
    {
        // Obtenemos el componente que controla la transparencia de todo el Canvas y sus textos/imágenes
        canvasGroup = GetComponent<CanvasGroup>();
    }

    void Update()
    {
        // Usamos una onda senoidal (Mathf.Sin) para crear un vaivén suave infinito
        // Mathf.Sin genera valores entre -1 y 1. Lo convertimos para que vaya de 0 a 1.
        float wave = (Mathf.Sin(Time.time * speed) + 1f) / 2f;

        // Cambiamos la transparencia suavemente entre el mínimo y el máximo que hayas elegido
        canvasGroup.alpha = Mathf.Lerp(minAlpha, maxAlpha, wave);
    }
}