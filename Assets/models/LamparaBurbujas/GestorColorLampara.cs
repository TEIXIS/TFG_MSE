using UnityEngine;
using System.Collections.Generic;

// Quitamos [ExecuteAlways] para que la lectura de colores originales se haga solo al darle al Play, evitando latencias en el editor.
public class GestorColorLampara : MonoBehaviour
{
    [Header("Audio")]
    public AudioClip sonidoClick;   // Arrastra aquí tu archivo de sonido (.wav o .mp3)

    [System.Serializable]
    public class ConfiguracionBoton
    {
        public string nombreIdentificador; // ej: "verde"
        public Renderer rendererBoton;     // El objeto del botón (que debe tener su único material asignado en la escena)

        [Tooltip("Intensidad de emisión cuando el botón está activo")]
        public float intensidadActivo = 2.5f;
        [Tooltip("Intensidad de emisión cuando el botón está en reposo (apagado)")]
        public float intensidadApagado = 0f; // <-- ¡NUEVA VARIABLE!
        // Variables ocultas para optimización (el script las rellena solas)
        [HideInInspector] public Color colorBaseOriginal;
    }

    public enum ColorPredefinido
    {
        CyanGaseoso, RosaNeon, VerdeRadioactivo, AmbarLava, VioletaProfundo, Blanco
    }

    [Header("Configuración de Botones")]
    [Tooltip("El nombre de la propiedad de emisión en tu material (Suele ser _EmissionColor o _BaseColor)")]
    public string referenciaShaderBoton = "_EmissionColor";
    public List<ConfiguracionBoton> listaBotones;

    [Header("Selector de Color Lámpara")]
    public ColorPredefinido colorActivo = ColorPredefinido.CyanGaseoso;

    [Header("Ajustes de la Paleta (HDR)")]
    [ColorUsage(true, true)] public Color color1_Cyan = new Color(0, 1, 1, 2f);
    [ColorUsage(true, true)] public Color color2_Rosa = new Color(1, 0, 1, 2f);
    [ColorUsage(true, true)] public Color color3_Verde = new Color(0, 1, 0, 2f);
    [ColorUsage(true, true)] public Color color4_Ambar = new Color(1, 0.5f, 0, 2f);
    [ColorUsage(true, true)] public Color color5_Violeta = new Color(0.5f, 0, 1, 2f);
    [ColorUsage(true, true)] public Color color6_Blanco = new Color(1, 1, 1, 2f);

    [Header("Elementos de la Escena")]
    public Renderer tuboInterior;
    public Renderer quadInterior;
    public Renderer tapaSuperficie;
    public Light luzBase;

    private string referenciaShader = "_ColorBombolles";

    // Usaremos dos bloques distintos para no mezclar la lámpara con los botones
    private MaterialPropertyBlock bloquePropiedadesLampara;
    private MaterialPropertyBlock bloquePropiedadesBotones;

    void Start()
    {
        bloquePropiedadesBotones = new MaterialPropertyBlock();
        bloquePropiedadesLampara = new MaterialPropertyBlock();

        // 1. EXTRAER EL COLOR ORIGINAL DE CADA BOTÓN (Para saber de qué color deben brillar)
        foreach (var boton in listaBotones)
        {
            if (boton.rendererBoton != null && boton.rendererBoton.sharedMaterial != null)
            {
                if (boton.rendererBoton.sharedMaterial.HasProperty("_BaseColor"))
                    boton.colorBaseOriginal = boton.rendererBoton.sharedMaterial.GetColor("_BaseColor");
                else if (boton.rendererBoton.sharedMaterial.HasProperty("_Color"))
                    boton.colorBaseOriginal = boton.rendererBoton.sharedMaterial.GetColor("_Color");
                else
                    boton.colorBaseOriginal = Color.white;
            }
        }

        ActualizarIluminacion();
    }

    public void SeleccionarBoton(string nombreColor)
    {
        string nombreLimpio = nombreColor.ToLower().Trim();

        foreach (var boton in listaBotones)
        {
            if (boton.nombreIdentificador.ToLower().Trim() == nombreLimpio)
            {
                // 1. SONIDO ESPACIAL
                if (boton.rendererBoton != null)
                {
                    AudioSource sourceDelBoton = boton.rendererBoton.GetComponent<AudioSource>();
                    if (sourceDelBoton != null && sonidoClick != null)
                    {
                        sourceDelBoton.PlayOneShot(sonidoClick);
                    }

                    // 2. ENCENDER BOTÓN (Multiplicar color base por intensidad)
                    boton.rendererBoton.GetPropertyBlock(bloquePropiedadesBotones);
                    Color colorBrillante = boton.colorBaseOriginal * boton.intensidadActivo;
                    bloquePropiedadesBotones.SetColor(referenciaShaderBoton, colorBrillante);
                    boton.rendererBoton.SetPropertyBlock(bloquePropiedadesBotones);
                }
            }
            else
            {
                // 3. APAGAR BOTÓN (Intensidad 0 = Negro absoluto)
                if (boton.rendererBoton != null)
                {
                    boton.rendererBoton.GetPropertyBlock(bloquePropiedadesBotones);

                    // Multiplicamos su propio color por la intensidad de reposo
                    Color colorReposo = boton.colorBaseOriginal * boton.intensidadApagado;
                    colorReposo.a = 1f;

                    bloquePropiedadesBotones.SetColor(referenciaShaderBoton, colorReposo);
                    boton.rendererBoton.SetPropertyBlock(bloquePropiedadesBotones);
                }
            }
        }

        // Finalmente, actualizamos la lámpara
        CambiarColorPorNombre(nombreColor);
    }

    public void CambiarColorPorNombre(string nombreColor)
    {
        switch (nombreColor.ToLower().Trim())
        {
            case "cyan": case "cyangaseoso": colorActivo = ColorPredefinido.CyanGaseoso; break;
            case "rosa": case "rosaneon": colorActivo = ColorPredefinido.RosaNeon; break;
            case "verde": case "verderadioactivo": colorActivo = ColorPredefinido.VerdeRadioactivo; break;
            case "ambar": case "ambarlava": colorActivo = ColorPredefinido.AmbarLava; break;
            case "violeta": case "violetaprofundo": colorActivo = ColorPredefinido.VioletaProfundo; break;
            case "blanco": colorActivo = ColorPredefinido.Blanco; break;
            default: Debug.LogWarning($"El color '{nombreColor}' no existe"); break;
        }
        ActualizarIluminacion();
    }

    public void CambiarColorDesdeUI(ColorPredefinido nuevoColor)
    {
        colorActivo = nuevoColor;
        ActualizarIluminacion();
    }

    private void ActualizarIluminacion()
    {
        Color colorSeleccionado = ObtenerColorActual();
        if (luzBase != null) luzBase.color = colorSeleccionado;

        if (bloquePropiedadesLampara == null) bloquePropiedadesLampara = new MaterialPropertyBlock();

        ActualizarMaterialLampara(tuboInterior, colorSeleccionado);
        ActualizarMaterialLampara(quadInterior, colorSeleccionado);
        ActualizarMaterialLampara(tapaSuperficie, colorSeleccionado);
    }

    private void ActualizarMaterialLampara(Renderer renderer, Color col)
    {
        if (renderer != null)
        {
            renderer.GetPropertyBlock(bloquePropiedadesLampara);
            bloquePropiedadesLampara.SetColor(referenciaShader, col);
            renderer.SetPropertyBlock(bloquePropiedadesLampara);
        }
    }

    private Color ObtenerColorActual()
    {
        switch (colorActivo)
        {
            case ColorPredefinido.CyanGaseoso: return color1_Cyan;
            case ColorPredefinido.RosaNeon: return color2_Rosa;
            case ColorPredefinido.VerdeRadioactivo: return color3_Verde;
            case ColorPredefinido.AmbarLava: return color4_Ambar;
            case ColorPredefinido.VioletaProfundo: return color5_Violeta;
            case ColorPredefinido.Blanco: return color6_Blanco;
            default: return color1_Cyan;
        }
    }
}