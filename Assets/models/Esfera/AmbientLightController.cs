

using UnityEngine;

public class AmbientLightController : MonoBehaviour
{
    [Header("Configuración del Cubo (Luz Ambiental)")]
    [SerializeField] private Renderer cuboRenderer;
    [Tooltip("Velocidad del cambio de color. Valores bajos = más relajante.")]
    [SerializeField] private float velocidadCiclo = 0.05f;
    [Tooltip("Intensidad del brillo (HDR) para el cubo ambiental.")]
    [SerializeField] private float intensidadCubo = 2.0f;

    [Header("Configuración del Botón Maestro")]
    [SerializeField] private Renderer botonRenderer;
    [Tooltip("Color base que tendrá el botón maestro.")]
    [SerializeField] private Color colorBaseBoton = Color.white;
    [SerializeField] private float emisionBotonEncendido = 1.5f;
    [SerializeField] private float emisionBotonApagado = 0.2f;

    private Material materialCubo;
    private Material materialBoton;
    private float hueActual = 0.5f; // 0.5 es el color Cyan en el espectro HSV
    private bool estaEncendido = true;

    void Start()
    {
        // Inicialización del Cubo Ambiental
        if (cuboRenderer == null) cuboRenderer = GetComponent<Renderer>();
        if (cuboRenderer != null)
        {
            materialCubo = cuboRenderer.material;
            materialCubo.EnableKeyword("_EMISSION");
        }
        else
        {
            Debug.LogWarning("No se asignó el Renderer del Cubo.");
        }

        // Inicialización del Botón Maestro
        if (botonRenderer != null)
        {
            materialBoton = botonRenderer.material;
            materialBoton.EnableKeyword("_EMISSION");
        }
        else
        {
            Debug.LogWarning("No se asignó el Renderer del Botón Maestro en el Inspector.");
        }

        // Aplicamos los estados iniciales (Empezando en Cyan y Botón Encendido)
        ActualizarColorCubo();
        ActualizarEmisionBoton();
    }

    void Update()
    {
        // Si la luz está apagada, el cubo no cambia de color
        if (!estaEncendido) return;

        // Avanzar suavemente a través de la gama de colores
        hueActual += velocidadCiclo * Time.deltaTime;
        if (hueActual > 1.0f)
        {
            hueActual -= 1.0f;
        }

        ActualizarColorCubo();
    }

    private void ActualizarColorCubo()
    {
        if (materialCubo == null) return;

        // Convertimos el espectro HSV a RGB y multiplicamos por la intensidad HDR
        Color colorBase = Color.HSVToRGB(hueActual, 1f, 1f);
        materialCubo.SetColor("_EmissionColor", colorBase * intensidadCubo);
    }

    private void ActualizarEmisionBoton()
    {
        if (materialBoton == null) return;

        // Determinamos la intensidad HDR según el estado actual
        float intensidadTarget = estaEncendido ? emisionBotonEncendido : emisionBotonApagado;
        materialBoton.SetColor("_EmissionColor", colorBaseBoton * intensidadTarget);
    }

    /// <summary>
    /// Método Maestro para encender/apagar. Conserva el progreso del color del cubo.
    /// </summary>
    public void AlternarEstadoLuz()
    {
        estaEncendido = !estaEncendido;

        if (!estaEncendido)
        {
            // Apagamos por completo la emisión del cubo ambiental
            if (materialCubo != null) materialCubo.SetColor("_EmissionColor", Color.black);
        }
        else
        {
            // Al encender, restauramos instantáneamente el color del cubo donde se quedó
            ActualizarColorCubo();
        }

        // Actualizamos la emisión propia del botón (1.5 o 0.2)
        ActualizarEmisionBoton();
    }
}