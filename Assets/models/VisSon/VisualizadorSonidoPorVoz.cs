using System;
using UnityEngine;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

[RequireComponent(typeof(AudioSource))]
public class VisualizadorSonidoPorVoz : MonoBehaviour
{
    [Header("Deteccion")]
    public Transform jugador;
    public float distanciaActivacion = 2.0f;
    public float umbralVoz = 0.015f;
    public float suavizado = 8.0f;

    [Header("Microfono")]
    public string microfonoPreferido = "";
    public int frecuenciaMicrofono = 16000;
    public int duracionClipSegundos = 1;
    public float cooldownReintentoMicrofono = 2.0f;
    public float intervaloLogMicrofono = 5.0f;

    [Header("Contexto")]
    public bool activarEnMenuMano = false;

    [Header("Cubos")]
    public Renderer[] cubos;
    public Color[] colores = new Color[]
    {
        new Color(1.00f, 0.05f, 0.05f),
        new Color(1.00f, 0.35f, 0.00f),
        new Color(1.00f, 0.85f, 0.00f),
        new Color(0.20f, 1.00f, 0.10f),
        new Color(0.00f, 0.90f, 0.75f),
        new Color(0.00f, 0.45f, 1.00f),
        new Color(0.25f, 0.10f, 1.00f),
        new Color(0.75f, 0.10f, 1.00f),
        new Color(1.00f, 0.10f, 0.60f)
    };

    [Header("Visual")]
    public float intensidadEmision = 4.0f;
    public float sensibilidadBandas = 120.0f;
    public float intensidadBaseColor = 0.45f;
    public float umbralEncendidoPrimerCubo = 0.08f;
    public float suavizadoEncendido = 14.0f;
    public bool crearLucesPorCubo = true;
    public Light[] lucesCubos;
    public float intensidadLuzMaxima = 3.0f;
    public float rangoLuz = 0.45f;
    public float alturaLuz = 0.08f;
    public bool cambiarColorBase = true;

    private static readonly int EmissionColorProp = Shader.PropertyToID("_EmissionColor");
    private static readonly int BaseColorProp = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorProp = Shader.PropertyToID("_Color");
    private static VisualizadorSonidoPorVoz instanciaConMicrofono;
    private static bool permisoMicrofonoSolicitadoGlobal;

    private AudioSource audioSource;
    private AudioClip microfonoClip;
    private string microfonoActivo;
    private readonly float[] muestras = new float[256];
    private readonly float[] espectro = new float[512];
    private readonly float[] niveles = new float[9];
    private readonly float[] nivelesLuz = new float[9];
    private MaterialPropertyBlock block;
    private bool microfonoIniciado;
    private bool esInstanciaMenuMano;
    private float siguienteIntentoMicrofono;
    private float ultimoLogMicrofonoIniciado = -999f;

    private void Awake()
    {
        QuestCrashDiagnostics.Log("[VisSon] Awake " + name);
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = true;
        audioSource.spatialBlend = 0f;
        audioSource.volume = 0f;
        esInstanciaMenuMano = EstaEnMenuMano();
        block = new MaterialPropertyBlock();

        if (cubos == null || cubos.Length == 0)
            BuscarCubosPorNombre();

        PrepararLucesCubos();
        ApagarTodo(true);
        QuestCrashDiagnostics.Log("[VisSon] Awake end cubos=" + (cubos != null ? cubos.Length : 0) + " crearLucesPorCubo=" + crearLucesPorCubo);
    }

    private void OnEnable()
    {
        QuestCrashDiagnostics.Log("[VisSon] OnEnable " + name);
        ApagarTodo(true);
    }

    private void OnDisable()
    {
        QuestCrashDiagnostics.Log("[VisSon] OnDisable " + name + " microfonoIniciado=" + microfonoIniciado + " activo=" + microfonoActivo);
        DetenerMicrofono();
        ApagarTodo(true);
    }

    private void OnDestroy()
    {
        QuestCrashDiagnostics.Log("[VisSon] OnDestroy " + name + " microfonoIniciado=" + microfonoIniciado + " activo=" + microfonoActivo);
    }

    private void Update()
    {
        if (!activarEnMenuMano && (esInstanciaMenuMano || EstaEnMenuMano()))
        {
            esInstanciaMenuMano = true;
            ApagarTodo(false);
            return;
        }

        Transform referenciaJugador = ObtenerJugador();
        bool cerca = referenciaJugador != null && Vector3.Distance(referenciaJugador.position, transform.position) <= distanciaActivacion;

        if (!cerca)
        {
            DetenerMicrofono();
            ApagarTodo(false);
            return;
        }

        if (!microfonoIniciado && !IniciarMicrofono())
        {
            ApagarTodo(false);
            return;
        }

        float voz = CalcularVolumenMicrofono();
        if (voz < umbralVoz)
        {
            ApagarTodo(false);
            return;
        }

        audioSource.GetSpectrumData(espectro, 0, FFTWindow.BlackmanHarris);
        ActualizarCubos(voz);
    }

    private Transform ObtenerJugador()
    {
        if (jugador != null)
            return jugador;

        Camera cam = Camera.main;
        return cam != null ? cam.transform : null;
    }

    private bool IniciarMicrofono()
    {
        if (Time.time < siguienteIntentoMicrofono)
            return false;

        if (!ReservarMicrofonoParaEstaInstancia())
        {
            siguienteIntentoMicrofono = Time.time + Mathf.Max(0.25f, cooldownReintentoMicrofono);
            return false;
        }

#if UNITY_ANDROID
        if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
        {
            if (!permisoMicrofonoSolicitadoGlobal)
            {
                Permission.RequestUserPermission(Permission.Microphone);
                permisoMicrofonoSolicitadoGlobal = true;
                Debug.Log("[VisSon] Solicitando permiso de microfono.");
                QuestCrashDiagnostics.Log("[VisSon] RequestUserPermission Microphone.");
            }

            LiberarReservaMicrofono();
            siguienteIntentoMicrofono = Time.time + Mathf.Max(0.25f, cooldownReintentoMicrofono);
            return false;
        }
#endif

        if (Microphone.devices == null || Microphone.devices.Length == 0)
        {
            Debug.LogWarning("[VisSon] No hay microfono disponible.");
            LiberarReservaMicrofono();
            siguienteIntentoMicrofono = Time.time + Mathf.Max(0.25f, cooldownReintentoMicrofono);
            return false;
        }

        try
        {
            microfonoActivo = !string.IsNullOrEmpty(microfonoPreferido) ? microfonoPreferido : Microphone.devices[0];
            QuestCrashDiagnostics.Log("[VisSon] Microphone.Start begin device=" + microfonoActivo + " freq=" + frecuenciaMicrofono);
            microfonoClip = Microphone.Start(microfonoActivo, true, Mathf.Max(1, duracionClipSegundos), frecuenciaMicrofono);
            if (microfonoClip == null)
            {
                Debug.LogWarning("[VisSon] No se pudo iniciar el microfono: " + microfonoActivo);
                LiberarReservaMicrofono();
                siguienteIntentoMicrofono = Time.time + Mathf.Max(0.25f, cooldownReintentoMicrofono);
                return false;
            }

            audioSource.clip = microfonoClip;
            audioSource.Play();
            QuestCrashDiagnostics.Log("[VisSon] Microphone.Start end device=" + microfonoActivo);
        }
        catch (Exception ex)
        {
            QuestCrashDiagnostics.LogWarning("[VisSon] Error iniciando microfono: " + ex);
            Debug.LogWarning("[VisSon] Error iniciando microfono: " + ex.Message);
            microfonoClip = null;
            microfonoActivo = null;
            LiberarReservaMicrofono();
            siguienteIntentoMicrofono = Time.time + Mathf.Max(0.25f, cooldownReintentoMicrofono);
            return false;
        }

        microfonoIniciado = true;
        if (Time.time - ultimoLogMicrofonoIniciado >= Mathf.Max(0.5f, intervaloLogMicrofono))
        {
            Debug.Log("[VisSon] Microfono iniciado: " + microfonoActivo);
            ultimoLogMicrofonoIniciado = Time.time;
        }
        return true;
    }

    private void DetenerMicrofono()
    {
        if (!microfonoIniciado)
            return;

        QuestCrashDiagnostics.Log("[VisSon] DetenerMicrofono begin device=" + microfonoActivo + " owner=" + (instanciaConMicrofono == this));

        if (instanciaConMicrofono != this)
        {
            microfonoClip = null;
            microfonoActivo = null;
            microfonoIniciado = false;
            return;
        }

        try
        {
            if (audioSource != null)
                audioSource.Stop();

            if (!string.IsNullOrEmpty(microfonoActivo))
                Microphone.End(microfonoActivo);
        }
        catch (Exception ex)
        {
            QuestCrashDiagnostics.LogWarning("[VisSon] Error deteniendo microfono: " + ex);
            Debug.LogWarning("[VisSon] Error deteniendo microfono: " + ex.Message);
        }

        microfonoClip = null;
        microfonoActivo = null;
        microfonoIniciado = false;
        siguienteIntentoMicrofono = Time.time + Mathf.Max(0.25f, cooldownReintentoMicrofono);
        LiberarReservaMicrofono();
        QuestCrashDiagnostics.Log("[VisSon] DetenerMicrofono end.");
    }

    private float CalcularVolumenMicrofono()
    {
        if (instanciaConMicrofono != this || microfonoClip == null || string.IsNullOrEmpty(microfonoActivo))
            return 0f;

        try
        {
            int posicion = Microphone.GetPosition(microfonoActivo) - muestras.Length;
            if (posicion < 0)
                return 0f;

            microfonoClip.GetData(muestras, posicion);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[VisSon] Error leyendo microfono: " + ex.Message);
            DetenerMicrofono();
            return 0f;
        }

        float suma = 0f;
        for (int i = 0; i < muestras.Length; i++)
            suma += muestras[i] * muestras[i];

        return Mathf.Sqrt(suma / muestras.Length);
    }

    private bool ReservarMicrofonoParaEstaInstancia()
    {
        if (instanciaConMicrofono == null || instanciaConMicrofono == this)
        {
            instanciaConMicrofono = this;
            return true;
        }

        return false;
    }

    private void LiberarReservaMicrofono()
    {
        if (instanciaConMicrofono == this)
            instanciaConMicrofono = null;
    }

    private bool EstaEnMenuMano()
    {
        return GetComponentInParent<FanOption>(true) != null;
    }

    private void ActualizarCubos(float voz)
    {
        int total = Mathf.Min(9, cubos != null ? cubos.Length : 0);
        if (total <= 0)
            return;

        float intensidadGlobal = Mathf.Clamp01(Mathf.Max(0f, voz - umbralVoz) * sensibilidadBandas * 0.3f);
        int cubosEncendidos = Mathf.Clamp(Mathf.CeilToInt(intensidadGlobal * total), 0, total);
        if (intensidadGlobal < umbralEncendidoPrimerCubo)
            cubosEncendidos = 0;

        for (int i = 0; i < total; i++)
        {
            Renderer cubo = cubos[i];
            if (cubo == null)
                continue;

            float banda = CalcularBanda(i, total);
            float energiaCubo = Mathf.Clamp01((banda * sensibilidadBandas) + intensidadGlobal);
            float objetivo = i < cubosEncendidos ? Mathf.Max(0.65f, energiaCubo) : 0f;
            niveles[i] = Mathf.Lerp(niveles[i], objetivo, Time.deltaTime * suavizadoEncendido);

            Color color = colores != null && colores.Length > i ? colores[i] : Color.white;
            AplicarColor(cubo, color, niveles[i]);
        }
    }

    private void ApagarLuces()
    {
        if (lucesCubos == null)
            return;

        for (int i = 0; i < lucesCubos.Length; i++)
        {
            Light luz = lucesCubos[i];
            if (luz == null)
                continue;

            luz.intensity = 0f;
            luz.enabled = false;
        }
    }

    private float CalcularBanda(int indice, int total)
    {
        int inicio = Mathf.FloorToInt(Mathf.Pow((float)indice / total, 2f) * espectro.Length);
        int fin = Mathf.FloorToInt(Mathf.Pow((float)(indice + 1) / total, 2f) * espectro.Length);
        fin = Mathf.Clamp(fin, inicio + 1, espectro.Length);

        float suma = 0f;
        for (int i = inicio; i < fin; i++)
            suma += espectro[i];

        return suma / (fin - inicio);
    }

    private void ApagarTodo(bool inmediato)
    {
        if (cubos == null)
            return;

        int total = Mathf.Min(9, cubos.Length);
        for (int i = 0; i < total; i++)
        {
            if (inmediato)
            {
                niveles[i] = 0f;
                nivelesLuz[i] = 0f;
            }
            else
            {
                niveles[i] = Mathf.Lerp(niveles[i], 0f, Time.deltaTime * suavizado);
            }

            Renderer cubo = cubos[i];
            if (cubo == null)
                continue;

            Color color = colores != null && colores.Length > i ? colores[i] : Color.white;
            AplicarColor(cubo, color, niveles[i]);
        }

        if (inmediato)
            ApagarLuces();
    }

    private void AplicarColor(Renderer rendererObjetivo, Color color, float intensidad)
    {
        rendererObjetivo.GetPropertyBlock(block);

        float intensidadNormalizada = Mathf.Clamp01(intensidad);
        Color colorBase = color * Mathf.Clamp01(intensidadBaseColor);
        Color emision = color * (intensidadNormalizada * intensidadEmision);

        if (cambiarColorBase)
        {
            block.SetColor(BaseColorProp, colorBase);
            block.SetColor(ColorProp, colorBase);
        }

        block.SetColor(EmissionColorProp, emision);
        rendererObjetivo.SetPropertyBlock(block);

        int indice = Array.IndexOf(cubos, rendererObjetivo);
        AplicarLuzCubo(indice, color, intensidadNormalizada);
    }

    private void PrepararLucesCubos()
    {
        if (!crearLucesPorCubo || cubos == null)
            return;

        int total = Mathf.Min(9, cubos.Length);
        if (lucesCubos == null || lucesCubos.Length < total)
            lucesCubos = new Light[total];

        for (int i = 0; i < total; i++)
        {
            Renderer cubo = cubos[i];
            if (cubo == null)
                continue;

            Color color = colores != null && colores.Length > i ? colores[i] : Color.white;
            Light luz = lucesCubos[i];
            if (luz == null)
                luz = cubo.GetComponentInChildren<Light>(true);

            if (luz == null)
            {
                GameObject luzObj = new GameObject("VisSon_Luz_" + (i + 1).ToString("00"));
                luzObj.transform.SetParent(cubo.transform, false);
                luzObj.transform.localPosition = Vector3.up * alturaLuz;
                luz = luzObj.AddComponent<Light>();
            }

            luz.type = LightType.Point;
            luz.color = color;
            luz.range = Mathf.Max(0.05f, rangoLuz);
            luz.intensity = 0f;
            luz.shadows = LightShadows.None;
            luz.renderMode = LightRenderMode.ForcePixel;
            luz.enabled = false;
            lucesCubos[i] = luz;
        }
    }

    private void AplicarLuzCubo(int indice, Color color, float intensidad)
    {
        if (!crearLucesPorCubo || lucesCubos == null || indice < 0 || indice >= lucesCubos.Length)
            return;

        Light luz = lucesCubos[indice];
        if (luz == null)
            return;

        if (indice >= nivelesLuz.Length)
            return;

        nivelesLuz[indice] = Mathf.Lerp(nivelesLuz[indice], intensidad, Time.deltaTime * suavizadoEncendido);
        luz.color = color;
        luz.range = Mathf.Max(0.05f, rangoLuz);
        luz.intensity = nivelesLuz[indice] * intensidadLuzMaxima;
        luz.enabled = luz.intensity > 0.02f;
    }

    private void BuscarCubosPorNombre()
    {
        cubos = new Renderer[9];

        for (int i = 0; i < cubos.Length; i++)
        {
            string nombre = "Cube." + (i + 1).ToString("000");
            Transform cube = transform.Find(nombre);
            if (cube == null)
                cube = BuscarHijoRecursivo(transform, nombre);

            if (cube != null)
                cubos[i] = cube.GetComponentInChildren<Renderer>(true);
        }
    }

    private Transform BuscarHijoRecursivo(Transform root, string nombre)
    {
        foreach (Transform child in root)
        {
            if (child.name == nombre)
                return child;

            Transform encontrado = BuscarHijoRecursivo(child, nombre);
            if (encontrado != null)
                return encontrado;
        }

        return null;
    }
}
