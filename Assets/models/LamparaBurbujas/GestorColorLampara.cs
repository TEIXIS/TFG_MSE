
using UnityEngine;
using System.Collections.Generic;

public class GestorColorLampara : MonoBehaviour
{
    [Header("Audio")]
    public AudioClip sonidoClick;

    [System.Serializable]
    public class ConfiguracionBoton
    {
        [Tooltip("Escribe el nombre EXACTO que pasa el Poke por par�metro (ej: CyanGaseoso, AmbarLava, RosaNeon)")]
        public string nombreIdentificador;
        public Renderer rendererBoton;

        [Tooltip("Intensidad de emisi�n cuando la l�mpara est� ON y el bot�n est� SELECCIONADO")]
        public float intensidadActivo = 2.5f;
        [Tooltip("Intensidad de emisi�n cuando la l�mpara est� ON y el bot�n est� EN REPOSO")]
        public float intensidadApagado = 0.2f;
        [HideInInspector] public Color colorBaseOriginal;
    }

    [System.Serializable]
    public class BotonEncendidoMaestro
    {
        public Renderer rendererBoton;
        public float intensidadOn = 3.0f;
        public float intensidadOff = 0.3f;
        [HideInInspector] public Color colorBaseOriginal;
    }

    public enum ColorPredefinido
    {
        CyanGaseoso, RosaNeon, VerdeRadioactivo, AmbarLava, VioletaProfundo, Blanco
    }

    [Header("Configuraci�n de Nombres de Shaders")]
    public string referenciaShaderBoton = "_EmissionColor";
    public string referenciaShader = "_ColorBombolles";
    public string referenciaShaderEmission = "_EmissionColor";

    [Header("Configuraci�n de Botones")]
    public List<ConfiguracionBoton> listaBotones;

    [Header("Bot�n Maestro de Encendido")]
    public BotonEncendidoMaestro botonMaestro;
    private bool estaEncendida = true;

    [Header("Selector de Color L�mpara")]
    [Tooltip("Color inicial por defecto al arrancar el juego")]
    public ColorPredefinido colorActivo = ColorPredefinido.CyanGaseoso;

    [Header("Ajustes de la Paleta (HDR)")]
    [ColorUsage(true, true)] public Color color1_Cyan = new Color(0, 1, 1, 2f);
    [ColorUsage(true, true)] public Color color2_Rosa = new Color(1, 0, 1, 2f);
    [ColorUsage(true, true)] public Color color3_Verde = new Color(0, 1, 0, 2f);
    [ColorUsage(true, true)] public Color color4_Ambar = new Color(1, 0.5f, 0, 2f);
    [ColorUsage(true, true)] public Color color5_Violeta = new Color(0.5f, 0, 1, 2f);
    [ColorUsage(true, true)] public Color color6_Blanco = new Color(1, 1, 1, 2f);

    [Header("Elementos de la Escena (L�mpara)")]
    public GameObject tuboInterior;
    public GameObject quadInterior;
    public GameObject tapaSuperficie;

    [Header("Luz")]
    public Light luzBase;

    private MaterialPropertyBlock bloquePropiedadesLampara;
    private MaterialPropertyBlock bloquePropiedadesBotones;
    private bool esInstanciaMenuMano;

    void Start()
    {
        QuestCrashDiagnostics.Log("[GestorColorLampara] Start " + name);
        esInstanciaMenuMano = EstaEnMenuMano();
        QuestCrashDiagnostics.Log("[GestorColorLampara] Contexto menuMano=" + esInstanciaMenuMano
            + " listaBotones=" + (listaBotones != null ? listaBotones.Count : -1)
            + " botonMaestro=" + (botonMaestro != null)
            + " tubo=" + (tuboInterior != null)
            + " quad=" + (quadInterior != null)
            + " tapa=" + (tapaSuperficie != null)
            + " luz=" + (luzBase != null));

        if (esInstanciaMenuMano)
        {
            QuestCrashDiagnostics.Log("[GestorColorLampara] Start omitido en clon de menu de mano.");
            return;
        }

        QuestCrashDiagnostics.Log("[GestorColorLampara] Creando MaterialPropertyBlock.");
        bloquePropiedadesBotones = new MaterialPropertyBlock();
        bloquePropiedadesLampara = new MaterialPropertyBlock();

        // Guardar colores originales de los botones
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

        // Guardar color original del bot�n maestro
        if (botonMaestro.rendererBoton != null && botonMaestro.rendererBoton.sharedMaterial != null)
        {
            if (botonMaestro.rendererBoton.sharedMaterial.HasProperty("_BaseColor"))
                botonMaestro.colorBaseOriginal = botonMaestro.rendererBoton.sharedMaterial.GetColor("_BaseColor");
            else if (botonMaestro.rendererBoton.sharedMaterial.HasProperty("_Color"))
                botonMaestro.colorBaseOriginal = botonMaestro.rendererBoton.sharedMaterial.GetColor("_Color");
            else
                botonMaestro.colorBaseOriginal = Color.white;
        }

        // ESTADO INICIAL: Forzar encendido y color por defecto al arrancar
        estaEncendida = true;

        AplicarColorInicialSinCambiarActivos();
        ActualizarVisualBotonMaestro();
        ActualizarVisualBotonesPaleta(); // Ilumina el bot�n activo inicial autom�ticamente
    }

    private void OnEnable()
    {
        QuestCrashDiagnostics.Log("[GestorColorLampara] OnEnable " + name + " menuMano=" + EstaEnMenuMano());
    }

    private void OnDisable()
    {
        QuestCrashDiagnostics.Log("[GestorColorLampara] OnDisable " + name + " menuMano=" + esInstanciaMenuMano);
    }

    private void OnDestroy()
    {
        QuestCrashDiagnostics.Log("[GestorColorLampara] OnDestroy " + name + " menuMano=" + esInstanciaMenuMano);
    }

    private bool EstaEnMenuMano()
    {
        return GetComponentInParent<FanOption>(true) != null;
    }

    private void AplicarColorInicialSinCambiarActivos()
    {
        Color colorBaseRender = estaEncendida ? ObtenerColorActual() : Color.black;
        Color colorEmissionRender = estaEncendida ? ObtenerColorActual() : Color.black;

        if (luzBase != null)
        {
            luzBase.color = colorBaseRender;
            luzBase.enabled = estaEncendida;
        }

        if (bloquePropiedadesLampara == null) bloquePropiedadesLampara = new MaterialPropertyBlock();
        ActualizarMaterialLampara(tuboInterior != null ? tuboInterior.GetComponent<Renderer>() : null, colorBaseRender, colorEmissionRender);
        ActualizarMaterialLampara(quadInterior != null ? quadInterior.GetComponent<Renderer>() : null, colorBaseRender, colorEmissionRender);
        ActualizarMaterialLampara(tapaSuperficie != null ? tapaSuperficie.GetComponent<Renderer>() : null, colorBaseRender, colorEmissionRender);
    }

    public void AlternarEstadoLampara()
    {
        if (esInstanciaMenuMano)
            return;

        QuestCrashDiagnostics.Log("[GestorColorLampara] AlternarEstadoLampara begin encendida=" + estaEncendida);
        estaEncendida = !estaEncendida;

        if (botonMaestro != null && botonMaestro.rendererBoton != null)
        {
            AudioSource sourceMaestro = botonMaestro.rendererBoton.GetComponent<AudioSource>();
            if (sourceMaestro != null && sonidoClick != null) sourceMaestro.PlayOneShot(sonidoClick);
        }

        if (!estaEncendida)
        {
            // L�mpara OFF: Apagamos la emisi�n de todos los botones sin tocar la variable 'colorActivo'
            ApagarEmisionTotalBotones();
        }
        else
        {
            // L�mpara ON: Encendemos la paleta bas�ndonos estrictamente en el estado de 'colorActivo' memorizado
            ActualizarVisualBotonesPaleta();
        }

        ActualizarIluminacion();
        ActualizarVisualBotonMaestro();
        QuestCrashDiagnostics.Log("[GestorColorLampara] AlternarEstadoLampara end encendida=" + estaEncendida);
    }

    public void SeleccionarBoton(string nombreColor)
    {
        if (esInstanciaMenuMano)
            return;

        if (!estaEncendida) return;
        listaBotones?.Find(b => b != null && b.nombreIdentificador.ToLower().Trim() == nombreColor.ToLower().Trim())?.rendererBoton?.GetComponent<AudioSource>()?.PlayOneShot(sonidoClick);
        CambiarColorPorNombre(nombreColor);
        ActualizarVisualBotonesPaleta(); // Actualiza bas�ndose en el enum reci�n guardado
    }

    private void ActualizarVisualBotonesPaleta()
    {
        // Convertimos el Enum activo actual a string para comparar directamente con los identificadores del inspector
        string nombreEnumActivo = colorActivo.ToString().ToLower().Trim();

        foreach (var boton in listaBotones)
        {
            if (boton.rendererBoton != null)
            {
                boton.rendererBoton.GetPropertyBlock(bloquePropiedadesBotones);

                string botonIdLimpio = boton.nombreIdentificador.ToLower().Trim();

                if (botonIdLimpio == nombreEnumActivo)
                {
                    // ESTADO 1: Bot�n que coincide con el Enum activo (Brillo M�ximo)
                    Color colorBrillante = boton.colorBaseOriginal * boton.intensidadActivo;
                    bloquePropiedadesBotones.SetColor(referenciaShaderBoton, colorBrillante);
                }
                else
                {
                    // ESTADO 2: Botones en reposo (Brillo Suave)
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
                // ESTADO 3: Cero emisi�n absoluta
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
            bloquePropiedadesBotones.SetColor(referenciaShaderBoton, colorFinal);
            botonMaestro.rendererBoton.SetPropertyBlock(bloquePropiedadesBotones);
        }
    }

    public void CambiarColorPorNombre(string nombreColor)
    {
        if (!estaEncendida) return;

        // Compara exactamente el string que env�a tu Poke con los nombres del Enum
        switch (nombreColor.Trim())
        {
            case "CyanGaseoso": colorActivo = ColorPredefinido.CyanGaseoso; break;
            case "RosaNeon": colorActivo = ColorPredefinido.RosaNeon; break;
            case "VerdeRadioactivo": colorActivo = ColorPredefinido.VerdeRadioactivo; break;
            case "AmbarLava": colorActivo = ColorPredefinido.AmbarLava; break;
            case "VioletaProfundo": colorActivo = ColorPredefinido.VioletaProfundo; break;
            case "Blanco": colorActivo = ColorPredefinido.Blanco; break;
            default: Debug.LogWarning($"El color '{nombreColor}' no coincide con ning�n caso exacto."); break;
        }
        ActualizarIluminacion();
    }

    private void ActualizarIluminacion()
    {
        QuestCrashDiagnostics.Log("[GestorColorLampara] ActualizarIluminacion begin encendida=" + estaEncendida + " color=" + colorActivo);
        Color colorBaseRender = estaEncendida ? ObtenerColorActual() : Color.black;
        Color colorEmissionRender = estaEncendida ? ObtenerColorActual() : Color.black;

        if (luzBase != null)
        {
            luzBase.color = colorBaseRender;
            luzBase.enabled = estaEncendida;
        }

        SetActiveIfNeeded(tuboInterior, estaEncendida);
        SetActiveIfNeeded(quadInterior, estaEncendida);
        SetActiveIfNeeded(tapaSuperficie, estaEncendida);

        if (estaEncendida)
        {
            if (bloquePropiedadesLampara == null) bloquePropiedadesLampara = new MaterialPropertyBlock();

            ActualizarMaterialLampara(tuboInterior?.GetComponent<Renderer>(), colorBaseRender, colorEmissionRender);
            ActualizarMaterialLampara(quadInterior?.GetComponent<Renderer>(), colorBaseRender, colorEmissionRender);
            ActualizarMaterialLampara(tapaSuperficie?.GetComponent<Renderer>(), colorBaseRender, colorEmissionRender);
        }
        QuestCrashDiagnostics.Log("[GestorColorLampara] ActualizarIluminacion end encendida=" + estaEncendida);
    }

    private void SetActiveIfNeeded(GameObject target, bool active)
  {
      if (target == null || target.activeSelf == active)
          return;

      QuestCrashDiagnostics.Log("[GestorColorLampara] SetActive " + target.name + " -> " + active);
      target.SetActive(active);
  }

    private void ActualizarMaterialLampara(Renderer renderer, Color baseCol, Color emiCol)
    {
        if (renderer != null && renderer.gameObject.activeSelf)
        {
            renderer.GetPropertyBlock(bloquePropiedadesLampara);
            bloquePropiedadesLampara.SetColor(referenciaShader, baseCol);
            bloquePropiedadesLampara.SetColor(referenciaShaderEmission, emiCol);
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

