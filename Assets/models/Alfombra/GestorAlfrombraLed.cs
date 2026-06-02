using UnityEngine;

public class GestorAlfombraLED : MonoBehaviour
{
    [Header("Audio")]
    [Tooltip("Arrastra aquí tu archivo de sonido para el botón (.wav o .mp3)")]
    public AudioClip sonidoClick;
    private AudioSource audioSourceBoton;

    [System.Serializable]
    public class BotonMaestroLED
    {
        public Renderer rendererBoton;
        [Tooltip("Intensidad de emisión cuando la alfombra está ON")]
        public float intensidadOn = 1.5f;
        [Tooltip("Intensidad de emisión cuando la alfombra está OFF")]
        public float intensidadOff = 0.2f;
        [HideInInspector] public Color colorBaseOriginal;
    }

    [Header("Botón Maestro de Encendido")]
    public BotonMaestroLED botonMaestro;

    [Header("Efecto de Partículas (Alfombra)")]
    [Tooltip("El componente Particle System de tu alfombra LED")]
    public ParticleSystem sistemaParticulas;

    private bool estaEncendida = true;
    private MaterialPropertyBlock bloquePropiedades;

    void Awake()
    {
        bloquePropiedades = new MaterialPropertyBlock();

        // Configurar el AudioSource local para el clic del botón
        audioSourceBoton = gameObject.AddComponent<AudioSource>();
        audioSourceBoton.spatialBlend = 1f; // 3D Spatialized para Meta Quest
    }

    void Start()
    {
        // 1. Guardar el color original del material del botón maestro
        if (botonMaestro.rendererBoton != null && botonMaestro.rendererBoton.sharedMaterial != null)
        {
            if (botonMaestro.rendererBoton.sharedMaterial.HasProperty("_BaseColor"))
                botonMaestro.colorBaseOriginal = botonMaestro.rendererBoton.sharedMaterial.GetColor("_BaseColor");
            else if (botonMaestro.rendererBoton.sharedMaterial.HasProperty("_Color"))
                botonMaestro.colorBaseOriginal = botonMaestro.rendererBoton.sharedMaterial.GetColor("_Color");
            else
                botonMaestro.colorBaseOriginal = Color.white;
        }

        // Forzar que el sistema tenga el loop activo de antemano
        if (sistemaParticulas != null)
        {
            var mainModule = sistemaParticulas.main;
            mainModule.loop = true;
        }

        // 2. ESTADO INICIAL: Arranca encendido por defecto
        estaEncendida = true;

        if (sistemaParticulas != null && !sistemaParticulas.isPlaying)
        {
            sistemaParticulas.Play();
        }

        ActualizarVisualBoton();
    }

    /// <summary>
    /// Función principal vinculada al 'When Select()' de tu Unity Event Wrapper
    /// </summary>
    public void AlternarAlfombraLED()
    {
        if (estaEncendida)
        {
            ApagarAlfombra();
        }
        else
        {
            EncenderAlfombra();
        }
    }

    private bool estaEncendido()
    {
        return estaEncendida;
    }

    private void EncenderAlfombra()
    {
        estaEncendida = true;
        EjecutarFeedbackBoton();

        if (sistemaParticulas != null)
        {
            // Nacerán partículas nuevas instantáneamente y se mantiene el bucle
            sistemaParticulas.Play();
        }
    }

    private void ApagarAlfombra()
    {
        estaEncendida = false;
        EjecutarFeedbackBoton();

        if (sistemaParticulas != null)
        {
            // Detiene la creación de nuevas de golpe, pero las vivas se desvanecen solas de forma agradable
            sistemaParticulas.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private void EjecutarFeedbackBoton()
    {
        // Sonido de confirmación instantáneo
        if (audioSourceBoton != null && sonidoClick != null)
        {
            audioSourceBoton.PlayOneShot(sonidoClick);
        }

        // Actualizar el material emisivo del botón maestro
        ActualizarVisualBoton();
    }

    private void ActualizarVisualBoton()
    {
        if (botonMaestro.rendererBoton != null)
        {
            botonMaestro.rendererBoton.GetPropertyBlock(bloquePropiedades);
            float intensidadActual = estaEncendida ? botonMaestro.intensidadOn : botonMaestro.intensidadOff;
            Color colorFinal = botonMaestro.colorBaseOriginal * intensidadActual;
            colorFinal.a = 1f;
            bloquePropiedades.SetColor("_EmissionColor", colorFinal);
            botonMaestro.rendererBoton.SetPropertyBlock(bloquePropiedades);
        }
    }
}