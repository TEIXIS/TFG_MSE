using UnityEngine;
using System.Collections.Generic;

public class GestorProyeccion : MonoBehaviour
{
    [Header("Audio")]
    [Tooltip("Arrastra aquí tu archivo de sonido (.wav o .mp3)")]
    public AudioClip sonidoClick;

    [System.Serializable]
    public class ConfiguracionBoton
    {
        [Tooltip("Escribe el nombre identificador (ej: CyanGaseoso, RosaNeon, VerdeRadioactivo...)")]
        public string nombreIdentificador;

        [Header("Visuales del Botón")]
        public Renderer rendererBoton;     // El modelo 3D del botón

        [Tooltip("Intensidad de emisión cuando la proyección está ON y el botón está SELECCIONADO")]
        public float intensidadActivo = 2.5f;

        [Tooltip("Intensidad de emisión cuando la proyección está ON y el botón está EN REPOSO")]
        public float intensidadApagado = 0.2f;

        [Header("Paleta del Shader (HDR)")]
        [ColorUsage(true, true)] public Color colorFondo = Color.black;
        [ColorUsage(true, true)] public Color colorBurbuja = Color.white;

        // Variables ocultas para optimización (cero latencia)
        [HideInInspector] public Color colorBaseOriginal;
        [HideInInspector] public AudioSource audioFuente;
    }

    [System.Serializable]
    public class BotonEncendidoMaestro
    {
        public Renderer rendererBoton;
        public float intensidadOn = 3.0f;
        public float intensidadOff = 0.3f;
        [HideInInspector] public Color colorBaseOriginal;
    }

    [Header("Configuración de Botones y Paletas")]
    [Tooltip("El nombre de la propiedad de emisión en tu material de los botones")]
    public string referenciaShaderBoton = "_EmissionColor";
    public List<ConfiguracionBoton> listaBotones;

    [Header("Botón Maestro de Encendido")]
    public BotonEncendidoMaestro botonMaestro;
    private bool estaEncendida = true;

    [Header("Color Memorizado Inicial")]
    [Tooltip("Escribe el identificador que arrancará por defecto (ej: CyanGaseoso)")]
    public string colorActivoInicial = "CyanGaseoso";
    private string colorActivoMemoria;

    [Header("Elementos de la Escena (Proyección)")]
    [Tooltip("El plano en la pared que tiene el Shader Graph")]
    public Renderer proyeccionRenderer;

    [Tooltip("La esfera u objeto extra que también usa el Shader Graph")]
    public Renderer esferaRenderer;

    // Nombres exactos de las propiedades en el Shader Graph de proyección
    private readonly string referenciaFondo = "_ColorFondo";
    private readonly string referenciaBurbuja = "_ColorBurbuja";

    private MaterialPropertyBlock bloquePropiedadesProyeccion;
    private MaterialPropertyBlock bloquePropiedadesBotones;

    void Start()
    {
        bloquePropiedadesProyeccion = new MaterialPropertyBlock();
        bloquePropiedadesBotones = new MaterialPropertyBlock();

        // 1. Guardar colores originales y cachés de audio de los botones de paleta
        foreach (var boton in listaBotones)
        {
            if (boton.rendererBoton != null)
            {
                boton.audioFuente = boton.rendererBoton.GetComponent<AudioSource>();

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

        // 2. Guardar color original del botón maestro
        if (botonMaestro.rendererBoton != null && botonMaestro.rendererBoton.sharedMaterial != null)
        {
            if (botonMaestro.rendererBoton.sharedMaterial.HasProperty("_BaseColor"))
                botonMaestro.colorBaseOriginal = botonMaestro.rendererBoton.sharedMaterial.GetColor("_BaseColor");
            else if (botonMaestro.rendererBoton.sharedMaterial.HasProperty("_Color"))
                botonMaestro.colorBaseOriginal = botonMaestro.rendererBoton.sharedMaterial.GetColor("_Color");
            else
                botonMaestro.colorBaseOriginal = Color.white;
        }

        // 3. SOLUCIÓN AL ARRANQUE: Forzar encendido y sincronizar datos limpiamente
        estaEncendida = true;
        colorActivoMemoria = colorActivoInicial.ToLower().Trim();

        // Forzamos la actualización visual de todos los componentes desde el frame 1
        ActualizarVisualBotonMaestro();
        ActualizarVisualBotonesPaleta();
        ActualizarIluminacionProyeccion();
    }

    public void AlternarEstadoLampara()
    {
        estaEncendida = !estaEncendida;

        // Audio espacial en el botón maestro
        if (botonMaestro.rendererBoton != null)
        {
            AudioSource sourceMaestro = botonMaestro.rendererBoton.GetComponent<AudioSource>();
            if (sourceMaestro != null && sonidoClick != null) sourceMaestro.PlayOneShot(sonidoClick);
        }

        if (!estaEncendida)
        {
            ApagarEmisionTotalBotones();
        }
        else
        {
            ActualizarVisualBotonesPaleta();
        }

        ActualizarVisualBotonMaestro();
        ActualizarIluminacionProyeccion();
    }

    public void SeleccionarBoton(string nombreColor)
    {
        if (!estaEncendida) return;

        string nombreLimpio = nombreColor.ToLower().Trim();

        // Reproducción de audio buscando de forma segura sin importar mayúsculas/minúsculas
        listaBotones.Find(b => b.nombreIdentificador.ToLower().Trim() == nombreLimpio)?.audioFuente?.PlayOneShot(sonidoClick);

        // Guardamos en memoria el nuevo color activo
        colorActivoMemoria = nombreLimpio;

        // Sincronizamos los materiales emisivos y actualizamos las mallas del shader
        ActualizarVisualBotonesPaleta();
        ActualizarIluminacionProyeccion();
    }

    private void ActualizarVisualBotonesPaleta()
    {
        foreach (var boton in listaBotones)
        {
            if (boton.rendererBoton != null)
            {
                boton.rendererBoton.GetPropertyBlock(bloquePropiedadesBotones);
                string botonIdLimpio = boton.nombreIdentificador.ToLower().Trim();

                if (botonIdLimpio == colorActivoMemoria)
                {
                    // Botón Activo: Brillo Máximo (HDR)
                    Color colorBrillante = boton.colorBaseOriginal * boton.intensidadActivo;
                    colorBrillante.a = 1f;
                    bloquePropiedadesBotones.SetColor(referenciaShaderBoton, colorBrillante);
                }
                else
                {
                    // Botones en Reposo: Brillo sutil
                    Color colorReposo = boton.colorBaseOriginal * boton.intensidadApagado;
                    colorReposo.a = 1f;
                    bloquePropiedadesBotones.SetColor(referenciaShaderBoton, colorReposo);
                }

                boton.rendererBoton.SetPropertyBlock(bloquePropiedadesBotones);
            }
        }
    }

    private void ApagarEmisionTotalBotones()
    {
        foreach (var boton in listaBotones)
        {
            if (boton.rendererBoton != null)
            {
                boton.rendererBoton.GetPropertyBlock(bloquePropiedadesBotones);
                bloquePropiedadesBotones.SetColor(referenciaShaderBoton, Color.black);
                boton.rendererBoton.SetPropertyBlock(bloquePropiedadesBotones);
            }
        }
    }

    private void ActualizarVisualBotonMaestro()
    {
        if (botonMaestro.rendererBoton != null)
        {
            botonMaestro.rendererBoton.GetPropertyBlock(bloquePropiedadesBotones);
            float intensidadActual = estaEncendida ? botonMaestro.intensidadOn : botonMaestro.intensidadOff;
            Color colorFinal = botonMaestro.colorBaseOriginal * intensidadActual;
            colorFinal.a = 1f;
            bloquePropiedadesBotones.SetColor(referenciaShaderBoton, colorFinal);
            botonMaestro.rendererBoton.SetPropertyBlock(bloquePropiedadesBotones);
        }
    }

    private void ActualizarIluminacionProyeccion()
    {
        if (bloquePropiedadesProyeccion == null) bloquePropiedadesProyeccion = new MaterialPropertyBlock();

        // -----------------------------------------------------------
        // ¡NUEVO! Encender o apagar los Renderers según el estado
        // -----------------------------------------------------------
        if (proyeccionRenderer != null) proyeccionRenderer.enabled = estaEncendida;
        if (esferaRenderer != null) esferaRenderer.enabled = estaEncendida;

        // Si está apagado, no calculamos ni aplicamos colores nuevos, simplemente terminamos la función
        if (!estaEncendida) return;

        Color fondoRender = Color.black;
        Color burbujaRender = Color.black;

        // Buscamos ignorando mayúsculas/minúsculas
        ConfiguracionBoton botonActivo = listaBotones.Find(b => b.nombreIdentificador.ToLower().Trim() == colorActivoMemoria);

        // Plan de respaldo si hay un error de escritura en el campo inicial
        if (botonActivo == null && listaBotones.Count > 0)
        {
            botonActivo = listaBotones[0];
            colorActivoMemoria = botonActivo.nombreIdentificador.ToLower().Trim();
        }

        if (botonActivo != null)
        {
            fondoRender = botonActivo.colorFondo;
            burbujaRender = botonActivo.colorBurbuja;
        }

        // Aplicamos al plano de la pared
        if (proyeccionRenderer != null)
        {
            proyeccionRenderer.GetPropertyBlock(bloquePropiedadesProyeccion);
            bloquePropiedadesProyeccion.SetColor(referenciaFondo, fondoRender);
            bloquePropiedadesProyeccion.SetColor(referenciaBurbuja, burbujaRender);
            proyeccionRenderer.SetPropertyBlock(bloquePropiedadesProyeccion);
        }

        // Aplicamos a la esfera auxiliar
        if (esferaRenderer != null)
        {
            esferaRenderer.GetPropertyBlock(bloquePropiedadesProyeccion);
            bloquePropiedadesProyeccion.SetColor(referenciaFondo, fondoRender);
            bloquePropiedadesProyeccion.SetColor(referenciaBurbuja, burbujaRender);
            esferaRenderer.SetPropertyBlock(bloquePropiedadesProyeccion);
        }
    }
}

/*using UnityEngine;
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
}*/