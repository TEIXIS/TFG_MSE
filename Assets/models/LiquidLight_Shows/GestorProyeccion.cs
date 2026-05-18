using UnityEngine;
using System.Collections.Generic;

// Quitamos [ExecuteAlways] para hacer la lectura del color inicial de forma segura al darle al Play
public class GestorProyeccion : MonoBehaviour
{
    [Header("Audio")]
    [Tooltip("Arrastra aquí tu archivo de sonido (.wav o .mp3)")]
    public AudioClip sonidoClick;

    [System.Serializable]
    public class ConfiguracionBoton
    {
        public string nombreIdentificador; // ej: "cyan", "rosa", "ambar"

        [Header("Visuales del Botón")]
        public Renderer rendererBoton;     // El modelo 3D del botón

        [Tooltip("Intensidad de emisión cuando el botón está activo")]
        public float intensidadActivo = 2.5f;

        [Tooltip("Intensidad de emisión cuando el botón está en reposo (apagado)")]
        public float intensidadApagado = 0f;

        [Header("Paleta del Shader (HDR)")]
        [ColorUsage(true, true)] public Color colorFondo = Color.black;
        [ColorUsage(true, true)] public Color colorBurbuja = Color.white;

        // Variables ocultas para optimización (cero latencia)
        [HideInInspector] public Color colorBaseOriginal;
        [HideInInspector] public AudioSource audioFuente;
    }

    [Header("Configuración de Botones y Paletas")]
    [Tooltip("El nombre de la propiedad de emisión en tu material de los botones (Suele ser _EmissionColor o _BaseColor)")]
    public string referenciaShaderBoton = "_EmissionColor";
    public List<ConfiguracionBoton> listaBotones;

    [Header("Elementos de la Escena")]
    [Tooltip("El plano en la pared que tiene el Shader Graph")]
    public Renderer proyeccionRenderer;

    [Tooltip("La esfera u objeto extra que también usa el Shader Graph")]
    public Renderer esferaRenderer;

    // IMPORTANTE: Nombres exactos del Shader Graph de proyección
    private readonly string referenciaFondo = "_ColorFondo";
    private readonly string referenciaBurbuja = "_ColorBurbuja";

    // Usamos dos bloques distintos para no mezclar configuraciones
    private MaterialPropertyBlock bloquePropiedadesProyeccion;
    private MaterialPropertyBlock bloquePropiedadesBotones;

    void Start()
    {
        bloquePropiedadesProyeccion = new MaterialPropertyBlock();
        bloquePropiedadesBotones = new MaterialPropertyBlock();

        // 1. PREPARACIÓN OPTIMIZADA: Leer colores originales y audios de los botones
        foreach (var boton in listaBotones)
        {
            if (boton.rendererBoton != null)
            {
                // Guardamos el AudioSource de antemano para evitar latencia al hacer clic
                boton.audioFuente = boton.rendererBoton.GetComponent<AudioSource>();

                // Extraemos el color base del material único del botón
                if (boton.rendererBoton.sharedMaterial != null)
                {
                    if (boton.rendererBoton.sharedMaterial.HasProperty("_BaseColor"))
                        boton.colorBaseOriginal = boton.rendererBoton.sharedMaterial.GetColor("_BaseColor");
                    else if (boton.rendererBoton.sharedMaterial.HasProperty("_Color"))
                        boton.colorBaseOriginal = boton.rendererBoton.sharedMaterial.GetColor("_Color");
                    else
                        boton.colorBaseOriginal = Color.white;
                }
            }
        }

        // Activar el primer botón por defecto al iniciar
        if (listaBotones.Count > 0)
        {
            SeleccionarBoton(listaBotones[0].nombreIdentificador);
        }
    }

    /// <summary>
    /// Cambia la paleta y actualiza los botones pasando un string. Ideal para Unity Events en VR.
    /// </summary>
    public void SeleccionarBoton(string nombreColor)
    {
        string nombreLimpio = nombreColor.ToLower().Trim();
        bool colorEncontrado = false;

        foreach (var boton in listaBotones)
        {
            if (boton.nombreIdentificador.ToLower().Trim() == nombreLimpio)
            {
                colorEncontrado = true;

                // 1. SONIDO ESPACIAL (Sin latencia)
                if (boton.audioFuente != null && sonidoClick != null)
                {
                    boton.audioFuente.PlayOneShot(sonidoClick);
                }

                // 2. ENCENDER BOTÓN (Multiplicar color original por intensidad de activo)
                if (boton.rendererBoton != null)
                {
                    boton.rendererBoton.GetPropertyBlock(bloquePropiedadesBotones);
                    Color colorBrillante = boton.colorBaseOriginal * boton.intensidadActivo;
                    colorBrillante.a = 1f; // Aseguramos opacidad total
                    bloquePropiedadesBotones.SetColor(referenciaShaderBoton, colorBrillante);
                    boton.rendererBoton.SetPropertyBlock(bloquePropiedadesBotones);
                }

                // 3. ACTUALIZAR SHADERS DE PROYECCIÓN (Plano y Esfera)
                ActualizarShaders(boton.colorFondo, boton.colorBurbuja);
            }
            else
            {
                // APAGAR BOTÓN (Multiplicar color original por intensidad de reposo)
                if (boton.rendererBoton != null)
                {
                    boton.rendererBoton.GetPropertyBlock(bloquePropiedadesBotones);
                    Color colorReposo = boton.colorBaseOriginal * boton.intensidadApagado;
                    colorReposo.a = 1f;
                    bloquePropiedadesBotones.SetColor(referenciaShaderBoton, colorReposo);
                    boton.rendererBoton.SetPropertyBlock(bloquePropiedadesBotones);
                }
            }
        }

        if (!colorEncontrado)
        {
            Debug.LogWarning($"No se ha configurado ningún botón con el identificador: '{nombreColor}'");
        }
    }

    private void ActualizarShaders(Color colorFondo, Color colorBurbuja)
    {
        if (bloquePropiedadesProyeccion == null) bloquePropiedadesProyeccion = new MaterialPropertyBlock();

        // Actualizar el Shader Graph de la pared (Plano)
        if (proyeccionRenderer != null)
        {
            proyeccionRenderer.GetPropertyBlock(bloquePropiedadesProyeccion);
            bloquePropiedadesProyeccion.SetColor(referenciaFondo, colorFondo);
            bloquePropiedadesProyeccion.SetColor(referenciaBurbuja, colorBurbuja);
            proyeccionRenderer.SetPropertyBlock(bloquePropiedadesProyeccion);
        }

        // Actualizar el Shader Graph de la Esfera
        if (esferaRenderer != null)
        {
            esferaRenderer.GetPropertyBlock(bloquePropiedadesProyeccion);
            bloquePropiedadesProyeccion.SetColor(referenciaFondo, colorFondo);
            bloquePropiedadesProyeccion.SetColor(referenciaBurbuja, colorBurbuja);
            esferaRenderer.SetPropertyBlock(bloquePropiedadesProyeccion);
        }
    }
}