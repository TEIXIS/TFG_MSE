using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct ElementoVR
{
    [Tooltip("El ID que enviar� la tablet (Ej: 'Rojo', 'Azul')")]
    public string idElemento;

    [Tooltip("El prefab peque�o (cubo) para el men� de la mano")]
    public GameObject prefabMenuMano;

    [Tooltip("El prefab grande que aparecer� f�sicamente en la sala")]
    public GameObject prefabHabitacion;
}

// --- NUEVA ESTRUCTURA: Empareja la aparici�n con el teletransporte ---
[System.Serializable]
public struct PuntoSala
{
    [Tooltip("El punto vac�o donde aparecer� el objeto grande")]
    public Transform puntoAparicion;

    [Tooltip("El punto vac�o donde se teletransportar� el jugador para mirarlo")]
    public Transform puntoTeletransporte;
}

public class SceneManager : MonoBehaviour
{
    private const string RoomWhite = "blanca";
    private const string RoomAdult = "adult";
    private const string RoomChild = "infantil";
    private const string AmbientLightLevelCommand = "CMD_AMBIENT_LIGHT_LEVEL:";

    public enum FaseApp { MenuInicial, SeleccionandoUsuario, EsperandoConexion, Tutorial, Jugando }
    private FaseApp faseActual = FaseApp.MenuInicial;

    // Memoria para saber si el usuario juega sentado o de pie
    private bool usuarioSentado = false;
    // Memoria para saber si el terapeuta deja usar el men� de manos
    private bool permisoMenuVRTerapeuta = true;
    // Memoria para saber si el terapeuta deja ver las part�culas
    private bool permisoParticulasTerapeuta = true;

    [Header("Efectos de Manos")]
    [Tooltip("Arrastra aqu� los GameObjects o Sistemas de Part�culas de las manos")]
    public GameObject particulasManoIzquierda;
    public GameObject particulasManoDerecha;

    [Header("Modo Espectador")]
    public VRHeadTracker headTracker;

    [Header("Passthrough (AR -> VR)")]
    public OVRPassthroughLayer passthroughLayer;

    [Header("Luz ambiente")]
    public AmbientLightIntensityController ambientLightController;

    [Header("Gesti�n de Red")]
    public LANDiscovery lanDiscovery;
    public Connection connectionServer;

    [Header("Gestor de Teletransporte")]
    [Tooltip("Arrastra aqu� el objeto que tiene tu script TeleportManager")]
    public TeleportManager teleportManager;

    [Header("Men� de Inicio (2 Opciones)")]
    public PalmMenuActivator menuActivator;
    public List<GameObject> initialOptions;
    public float distanciaMenuInicial = 0.35f;
    public float separacionMenuInicial = 45f;

    [Header("Men� de Usuarios (Hasta 5 Opciones)")]
    public List<GameObject> userSelectionOptions;
    [Header("Base de Datos (Nombres)")]
    private List<string> nombresDeUsuarios = new List<string>();
    private List<User> usuariosIndependientes = new List<User>();
    private readonly List<string> nombresBackupUsuarios = new List<string> { "Ana", "Carles", "Joan", "David", "Elena", "A1", "B2", "C3", "D4", "E5", "F6" };
    private bool usuariosCargados = false;
    private bool usuariosCargando = false;
    private int paginaActualUsuarios = 0;
    private const int maxNombresPorPagina = 4;
    public float distanciaMenuUsuarios = 0.5f;
    public float separacionMenuUsuarios = 22f;
    private List<string> labelsPaginaActual = new List<string>();
    private const int primeraOpcionNombreIndex = 1;
    private const int ultimaOpcionNombreIndex = 4;
    private const int indexFlechaIzquierda = 0;
    private const int indexFlechaDerecha = 5;

    [Header("Referencias de la Escena")]
    public GameObject canvasConexion;
    public GameObject salaMultisensorial;
    [Header("Tipos de Sala")]
    [Tooltip("Sala blanca. Si se deja vacio, se intentara buscar un GameObject llamado HabBlanca.")]
    public GameObject habBlanca;
    [Tooltip("Sala adulta. Si se deja vacio, se intentara buscar un GameObject llamado HabAdult.")]
    public GameObject habAdult;
    [Tooltip("Sala infantil. Si se deja vacio, se intentara buscar un GameObject llamado HabInfantil.")]
    public GameObject habInfantil;
   // public GameObject elements;
   // public GameObject jumpingpoints;

    [Header("Base de Datos de Elementos (Colores)")]
    public List<ElementoVR> baseDatosElementos;

    [Header("Puntos de Aparici�n y Teletransporte (Sala)")]
    [Tooltip("Se usan en orden fijo. Los 6 primeros son las posiciones disponibles para objetos en VR.")]
    public List<PuntoSala> puntosDeAparicion;
    [Header("Jumping Points")]
    public bool ajustarJumpingPointsCercaDelElemento = true;
    public float distanciaJumpingPointDesdeBorde = 0.55f;
    public float distanciaJumpingPointMinima = 0.45f;
    public float distanciaJumpingPointMaxima = 2.0f;
    [Header("Ajuste de objetos en sala")]
    public float margenObjetoDentroSala = 0.15f;
    public float margenTeleportDentroSala = 0.25f;
    public bool centrarBoundsVisualesEnPuntoAparicion = true;
    public float maxAjusteCentradoObjeto = 5.0f;

    private List<GameObject> objetosGeneradosEnSala = new List<GameObject>();
    private List<GameObject> opcionesMenuJugando = new List<GameObject>();
    private readonly Dictionary<string, GameObject> objetosGeneradosPorId = new Dictionary<string, GameObject>();
    private readonly Dictionary<string, PuntoSala> puntosAsignadosPorId = new Dictionary<string, PuntoSala>();
    private string tipoSalaActual = RoomWhite;

    private bool tabletRecienConectada = false;
    private bool recuperandoSesionMultidispositivo = false;
    private bool transicionVRActiva = false;
    private bool preparandoEntradaVR = false;
    private int ultimoUsuarioMultidispositivoProcesado = -1;
    private bool omitirSiguienteSyncVr = false;
    private readonly List<MonoBehaviour> interaccionesVrPausadas = new List<MonoBehaviour>();

    // --- NUEVO: Variable para recordar qu� objeto de tutorial est� activo y poder borrarlo ---
    private GameObject elementoTutorialActivo;
    private string idTutorialActivo;
    [Header("Tutorial")]
    public float tutorialDistanceFromHead = 0.25f;
    public float tutorialMinDistance = 0.7f;
    public float tutorialMaxDistance = 2.4f;
    public float tutorialBoundsPadding = 0.35f;
    public float tutorialAssumedPlayerHeight = 1.6f;
    public float tutorialMinHeadHeight = 0.8f;
    public float tutorialMaxHeadHeight = 2.2f;
    [Range(0f, 1f)] public float tutorialCenterHeightRatio = 0.5f;
    private bool registrandoDiagnosticoError;


    // Diccionario m�gico para recordar en qu� coordenada ha ca�do cada elemento
    private Dictionary<string, Transform> mapaDeTeleports = new Dictionary<string, Transform>();

    // Lista secreta para traducir �ndices del men� de mano a IDs de la tablet
    private List<string> idsOrdenadosParaTeleport = new List<string>();


    IEnumerator Start()
    {
        QuestCrashDiagnostics.Log("[SceneManager] Start coroutine begin.");

        if (baseDatosElementos == null)
            baseDatosElementos = new List<ElementoVR>();

        if (puntosDeAparicion == null)
            puntosDeAparicion = new List<PuntoSala>();

        ResolverReferenciasSalas();
        AplicarTipoSala(tipoSalaActual, false);

        if (passthroughLayer != null) passthroughLayer.hidden = false;
        SetSalaActiva(false);

        if (connectionServer != null)
        {
            connectionServer.RegisterOnClientConnectCallback(OnTabletConnected);
        }
        else
        {
            Debug.LogWarning("No has asignado el script Connection al SceneManager.");
        }

        StartCoroutine(CargarUsuariosIndependent());
        yield return new WaitForSeconds(2.0f);
        QuestCrashDiagnostics.Log("[SceneManager] Initial user load wait finished.");

        if (menuActivator != null && initialOptions != null && initialOptions.Count > 0)
        {
            faseActual = FaseApp.MenuInicial;
            menuActivator.isLockedOpen = true;
            menuActivator.distancia = distanciaMenuInicial;
            if (menuActivator.fanMenu != null) menuActivator.fanMenu.spacingAngle = separacionMenuInicial;

            menuActivator.OpenMenu(initialOptions);
            QuestCrashDiagnostics.Log("[SceneManager] Initial menu opened.");
        }

        // L�neas de prueba: simula que la tablet env�a datos
      //  List<string> coloresDePrueba = new List<string> { "blau_cel", "groc", "rosa","verd","taronja","blau_fosc" };
      //  RecibirDatosDeLaTablet(coloresDePrueba);
    }

    void ResolverReferenciasSalas()
    {
        if (habBlanca == null)
            habBlanca = GameObject.Find("HabBlanca");

        if (habAdult == null)
            habAdult = GameObject.Find("HabAdult");

        if (habInfantil == null)
            habInfantil = GameObject.Find("HabInfantil");

        if (salaMultisensorial == null)
            salaMultisensorial = habBlanca != null ? habBlanca : (habAdult != null ? habAdult : habInfantil);
    }

    void AplicarTipoSala(string tipoSala, bool mantenerActiva)
    {
        QuestCrashDiagnostics.Log("[SALA] AplicarTipoSala begin tipo=" + tipoSala + " mantenerActiva=" + mantenerActiva);
        tipoSalaActual = NormalizarTipoSala(tipoSala);

        GameObject salaAnterior = salaMultisensorial;
        salaMultisensorial = ObtenerSalaPorTipo(tipoSalaActual);
        bool debeQuedarActiva = mantenerActiva && salaAnterior != null && salaAnterior.activeSelf;

        if (habBlanca != null) habBlanca.SetActive(false);
        if (habAdult != null) habAdult.SetActive(false);
        if (habInfantil != null) habInfantil.SetActive(false);

        SetSalaActiva(debeQuedarActiva);
        if (debeQuedarActiva)
            ReparentObjetosGeneradosASalaActiva();

        Debug.Log("[SALA] Tipo de sala aplicado: " + tipoSalaActual);
        QuestCrashDiagnostics.Log("[SALA] AplicarTipoSala end tipo=" + tipoSalaActual + " activa=" + debeQuedarActiva);
    }

    string NormalizarTipoSala(string tipoSala)
    {
        string valor = (tipoSala ?? "").Trim().ToLowerInvariant();

        if (valor == RoomAdult || valor.Contains("adult"))
            return RoomAdult;

        if (valor == RoomChild || valor.Contains("infant"))
            return RoomChild;

        return RoomWhite;
    }

    GameObject ObtenerSalaPorTipo(string tipoSala)
    {
        if (tipoSala == RoomAdult && habAdult != null)
            return habAdult;

        if (tipoSala == RoomChild && habInfantil != null)
            return habInfantil;

        if (habBlanca != null)
            return habBlanca;

        if (salaMultisensorial != null)
            return salaMultisensorial;

        return habAdult != null ? habAdult : habInfantil;
    }

    void SetSalaActiva(bool activa)
    {
        QuestCrashDiagnostics.Log("[SALA] SetSalaActiva begin activa=" + activa + " sala=" + (salaMultisensorial != null ? salaMultisensorial.name : "null"));
        if (salaMultisensorial != null)
            salaMultisensorial.SetActive(activa);
        QuestCrashDiagnostics.Log("[SALA] SetSalaActiva end activeInHierarchy=" + (salaMultisensorial != null && salaMultisensorial.activeInHierarchy));
    }

    void ReparentObjetosGeneradosASalaActiva()
    {
        if (salaMultisensorial == null)
            return;

        foreach (GameObject obj in objetosGeneradosPorId.Values)
        {
            if (obj != null)
                obj.transform.SetParent(salaMultisensorial.transform, true);
        }
    }

    IEnumerator CargarUsuariosIndependent()
    {
        Debug.Log("[MENU] CargarUsuariosIndependent iniciado.");

        if (usuariosCargando)
        {
            Debug.Log("[MENU] Ya habia una carga de usuarios en curso; esperando.");
            while (usuariosCargando)
                yield return null;

            yield break;
        }

        usuariosCargando = true;
        usuariosCargados = false;
        List<string> usuarios = null;
        string error = null;

        yield return StartCoroutine(UserAPI.GetUsers(
            users =>
            {
                usuarios = new List<string>();
                List<User> usuariosFiltrados = new List<User>();
                int totalUsuarios = users != null ? users.Length : 0;

                if (users != null)
                {
                    foreach (var us in users)
                    {
                        if (us != null && us.independent && !string.IsNullOrWhiteSpace(us.nom))
                        {
                            usuarios.Add(us.nom.Trim());
                            usuariosFiltrados.Add(us);
                        }
                    }
                }

                usuariosIndependientes = usuariosFiltrados;
                Debug.Log($"[API] /users devolvio {totalUsuarios} usuarios; independientes validos: {usuarios.Count}.");
            },
            err =>
            {
                error = err;
            }
        ));

        if (!string.IsNullOrEmpty(error))
        {
            Debug.LogWarning("[API] No se pudieron cargar los usuarios: " + error);
            nombresDeUsuarios = new List<string>(nombresBackupUsuarios);
            usuariosIndependientes.Clear();
            usuariosCargados = true;
            Debug.LogWarning("[API] Usando nombres backup: " + string.Join(", ", nombresDeUsuarios));
        }
        else if (usuarios != null)
        {
            nombresDeUsuarios = usuarios.Count > 0 ? usuarios : new List<string>(nombresBackupUsuarios);
            usuariosCargados = true;
            if (usuarios.Count == 0)
            {
                Debug.LogWarning("[API] No se encontraron usuarios con independent=true.");
                Debug.LogWarning("[API] Usando nombres backup: " + string.Join(", ", nombresDeUsuarios));
            }
            else
            {
                Debug.Log($"[API] Cargados {usuarios.Count} usuarios independientes: {string.Join(", ", usuarios)}");
            }
        }

        usuariosCargando = false;
    }

    void OnEnable()
    {
        QuestCrashDiagnostics.Log("[SceneManager] OnEnable");
        Application.logMessageReceived += RegistrarDiagnosticoLogUnity;
        FanOption.OnInitialMenuSelectedWithLabel += AccionDelBotonConLabel;
        if (teleportManager != null)
            teleportManager.OnUsuarioTeletransportado += AvisarTabletUbicacion;
    }
    void OnDisable()
    {
        QuestCrashDiagnostics.Log("[SceneManager] OnDisable");
        Application.logMessageReceived -= RegistrarDiagnosticoLogUnity;
        FanOption.OnInitialMenuSelectedWithLabel -= AccionDelBotonConLabel;
        if (teleportManager != null)
            teleportManager.OnUsuarioTeletransportado -= AvisarTabletUbicacion;
    }

    void OnDestroy()
    {
        QuestCrashDiagnostics.Log("[SceneManager] OnDestroy " + CrearResumenEstadoVr());
    }

    void OnApplicationPause(bool pauseStatus)
    {
        QuestCrashDiagnostics.Log("[SceneManager] OnApplicationPause pause=" + pauseStatus + " " + CrearResumenEstadoVr());
    }

    void OnApplicationFocus(bool hasFocus)
    {
        QuestCrashDiagnostics.Log("[SceneManager] OnApplicationFocus focus=" + hasFocus + " " + CrearResumenEstadoVr());
    }

    void RegistrarDiagnosticoLogUnity(string condition, string stackTrace, LogType type)
    {
        if (registrandoDiagnosticoError || (type != LogType.Exception && type != LogType.Error))
            return;

        registrandoDiagnosticoError = true;
        Debug.LogWarning("[DIAG] Error Unity detectado. " + CrearResumenEstadoVr());
        registrandoDiagnosticoError = false;
    }

    string CrearResumenEstadoVr()
    {
        string resumen = $"fase={faseActual} transicion={transicionVRActiva} preparando={preparandoEntradaVR} sala={(salaMultisensorial != null ? salaMultisensorial.name : "null")} salaActiva={(salaMultisensorial != null && salaMultisensorial.activeInHierarchy)} objetos={objetosGeneradosPorId.Count}";

        foreach (var kv in objetosGeneradosPorId)
        {
            if (kv.Value == null)
            {
                resumen += $" | {kv.Key}:null";
                continue;
            }

            resumen += " | " + CrearResumenObjeto(kv.Value, kv.Key);
        }

        return resumen;
    }

    string CrearResumenObjeto(GameObject obj, string id)
    {
        if (obj == null)
            return id + ":null";

        Vector3 pos = obj.transform.position;
        Vector3 rot = obj.transform.eulerAngles;
        string resumen = $"{id}:pos=({pos.x:F2},{pos.y:F2},{pos.z:F2}) rotY={rot.y:F1} active={obj.activeInHierarchy}";

        Bounds bounds;
        if (TryGetRendererBounds(obj, out bounds))
        {
            Vector3 c = bounds.center;
            Vector3 s = bounds.size;
            resumen += $" boundsCenter=({c.x:F2},{c.y:F2},{c.z:F2}) boundsSize=({s.x:F2},{s.y:F2},{s.z:F2})";
        }
        else
        {
            resumen += " bounds=none";
        }

        return resumen;
    }

    void AvisarTabletUbicacion(int index)
    {
        if (index >= 0 && index < idsOrdenadosParaTeleport.Count)
        {
            string idDestino = idsOrdenadosParaTeleport[index];
            if (connectionServer != null && connectionServer.connected)
            {
                // Le mandamos el mensaje a la tablet
                connectionServer.Send("UBICACION:" + idDestino);
            }
        }
    }

    void AccionDelBotonConLabel(int index, string label)
    {
        if (faseActual == FaseApp.MenuInicial)
        {
            if (index == 1) // ESPERAR CONEXIÓN
            {
                faseActual = FaseApp.EsperandoConexion;

                if (canvasConexion != null)
                {
                    canvasConexion.SetActive(true);
                    Canvas canvasComp = canvasConexion.GetComponent<Canvas>();
                    if (canvasComp != null) canvasComp.enabled = true;

                    if (Camera.main != null)
                    {
                        Transform cam = Camera.main.transform;
                        Vector3 direccionPlana = cam.forward;
                        direccionPlana.y = 0;
                        direccionPlana.Normalize();

                        canvasConexion.transform.position = cam.position + (direccionPlana * 1.0f);
                        canvasConexion.transform.rotation = Quaternion.LookRotation(direccionPlana);
                    }
                    else if (menuActivator != null && menuActivator.fanMenu != null)
                    {
                        canvasConexion.transform.position = menuActivator.fanMenu.transform.position;
                        canvasConexion.transform.rotation = menuActivator.fanMenu.transform.rotation;
                    }
                }

                Debug.Log("Iniciando modo Multidispositivo. Encendiendo LAN Discovery...");
                if (lanDiscovery != null) lanDiscovery.StartBroadcast();
            }
            else if (index == 0) // SELECCIÓN DE USUARIO
            {
                faseActual = FaseApp.SeleccionandoUsuario;
                StartCoroutine(TransicionAlMenuUsuarios());
            }

            return;
        }

        if (faseActual == FaseApp.SeleccionandoUsuario)
        {
            Debug.Log($"[MENÚ] Click index={index}, label='{label}', página actual={paginaActualUsuarios}");

            if (label == "<-")
            {
                MostrarPaginaUsuarios(paginaActualUsuarios - maxNombresPorPagina);
                return;
            }

            if (label == "->")
            {
                MostrarPaginaUsuarios(paginaActualUsuarios + maxNombresPorPagina);
                return;
            }

            if (!string.IsNullOrWhiteSpace(label))
            {
                string nombreSeleccionado = label;
                Debug.Log($"[MENÚ] Usuario seleccionado: {nombreSeleccionado}");

                faseActual = FaseApp.Jugando;
                if (menuActivator != null)
                {
                    menuActivator.isLockedOpen = false;
                    menuActivator.CloseMenuPublic(0.5f);
                }

                StartCoroutine(EsperarCierreMenuYIniciarVR(nombreSeleccionado));
            }
        }
    }

    void AccionDelBoton(int index)
    {
        if (faseActual == FaseApp.MenuInicial)
        {
            if (index == 1) // ESPERAR CONEXIÓN
            {
                faseActual = FaseApp.EsperandoConexion;

                if (canvasConexion != null)
                {
                    canvasConexion.SetActive(true);
                    Canvas canvasComp = canvasConexion.GetComponent<Canvas>();
                    if (canvasComp != null) canvasComp.enabled = true;

                    if (Camera.main != null)
                    {
                        Transform cam = Camera.main.transform;
                        Vector3 direccionPlana = cam.forward;
                        direccionPlana.y = 0;
                        direccionPlana.Normalize();

                        canvasConexion.transform.position = cam.position + (direccionPlana * 1.0f);
                        canvasConexion.transform.rotation = Quaternion.LookRotation(direccionPlana);
                    }
                    else if (menuActivator != null && menuActivator.fanMenu != null)
                    {
                        canvasConexion.transform.position = menuActivator.fanMenu.transform.position;
                        canvasConexion.transform.rotation = menuActivator.fanMenu.transform.rotation;
                    }
                }

                Debug.Log("Iniciando modo Multidispositivo. Encendiendo LAN Discovery...");
                if (lanDiscovery != null) lanDiscovery.StartBroadcast();
            }
            else if (index == 0) // SELECCIÓN DE USUARIO
            {
                faseActual = FaseApp.SeleccionandoUsuario;
                StartCoroutine(TransicionAlMenuUsuarios());
            }

            return;
        }

        if (faseActual == FaseApp.SeleccionandoUsuario)
        {
            if (index < 0 || index >= labelsPaginaActual.Count)
            {
                Debug.LogWarning($"[MENÚ] Índice fuera de rango: {index}");
                return;
            }

            string label = labelsPaginaActual[index];

            Debug.Log($"[MENÚ] Click lógico index={index}, label='{label}', página={paginaActualUsuarios}");

            if (index == indexFlechaIzquierda)
            {
                MostrarPaginaUsuarios(paginaActualUsuarios - maxNombresPorPagina);
                return;
            }

            if (index == indexFlechaDerecha)
            {
                MostrarPaginaUsuarios(paginaActualUsuarios + maxNombresPorPagina);
                return;
            }

            if (!string.IsNullOrEmpty(label))
            {
                string nombreSeleccionado = label;
                Debug.Log($"[MENÚ] Usuario seleccionado: {nombreSeleccionado}");

                faseActual = FaseApp.Jugando;
                if (menuActivator != null)
                {
                    menuActivator.isLockedOpen = false;
                    menuActivator.CloseMenuPublic(0.5f);
                }

                StartCoroutine(EsperarCierreMenuYIniciarVR(nombreSeleccionado));
            }
        }
    }
    void OnTabletConnected()
    {
        Debug.Log("�Conexi�n recibida de la tablet! Transicionando a VR...");
        if (connectionServer != null && connectionServer.connected)
            connectionServer.Send("REQ_USER");

        ultimoUsuarioMultidispositivoProcesado = -1;
        tabletRecienConectada = true;
    }

    IEnumerator EsperarCierreMenuYIniciarVR(string nombreUsuario = null)
    {
        yield return new WaitForSeconds(1.4f);
        yield return StartCoroutine(IniciarExperienciaVRParaUsuarioIndependiente(nombreUsuario));
    }

    IEnumerator TransicionAlMenuUsuarios()
    {
        Debug.Log("[MENU] TransicionAlMenuUsuarios: recargando usuarios desde API.");
        yield return new WaitForSeconds(0.6f);

        yield return StartCoroutine(CargarUsuariosIndependent());

        MostrarPaginaUsuarios(0);
    }

    // ==========================================
    // FASE VR COMPLETA (CON FUNDIDO)
    // ==========================================

    void MostrarPaginaUsuarios(int indiceDeInicio)
    {
        if (menuActivator == null || userSelectionOptions == null || userSelectionOptions.Count < 7)
        {
            Debug.LogError("[MENU] No se puede mostrar usuarios: revisa menuActivator y que userSelectionOptions tenga 7 prefabs.");
            return;
        }

        int totalNombres = nombresDeUsuarios.Count;
        Debug.Log($"[MENU] MostrarPaginaUsuarios con {totalNombres} nombres.");
        if (totalNombres == 0)
        {
            Debug.LogWarning("[MENU] No hay usuarios independientes cargados para mostrar. Revisa la conexion con la API y el campo independent en la BD.");
            return;
        }

        int mostrarCuantos = maxNombresPorPagina; // Siempre queremos 4 si hay suficientes
        bool necesitaPaginacion = totalNombres > mostrarCuantos;

        // Aseguramos que el índice sea circular (si es -1 pasa al último, si supera el total vuelve a 0)
        if (totalNombres > 0)
        {
            paginaActualUsuarios = (indiceDeInicio + totalNombres) % totalNombres;
        }

        labelsPaginaActual.Clear();
        List<GameObject> opcionesActivas = new List<GameObject>();

        // 1. Flecha Izquierda (Retrocede 1 posición)
        if (necesitaPaginacion)
        {
            labelsPaginaActual.Add("<-");
            opcionesActivas.Add(userSelectionOptions[0]);
        }

        // 2. Nombres (Ciclo circular)
        // Si hay menos de 4, mostramos los que hay. Si hay 4 o más, mostramos 4.
        int limite = Mathf.Min(mostrarCuantos, totalNombres);
        
        for (int i = 0; i < limite; i++)
        {
            // La magia del módulo: (inicio + i) % total
            int realIndex = (paginaActualUsuarios + i) % totalNombres;
            
            labelsPaginaActual.Add(nombresDeUsuarios[realIndex]);
            opcionesActivas.Add(userSelectionOptions[i + 1]);
        }

        // 3. Flecha Derecha (Avanza 1 posición)
        if (necesitaPaginacion)
        {
            labelsPaginaActual.Add("->");
            opcionesActivas.Add(userSelectionOptions[5]);
        }

        // Configuración y apertura
        menuActivator.isLockedOpen = true;
        menuActivator.distancia = distanciaMenuUsuarios;
        if (menuActivator.fanMenu != null) menuActivator.fanMenu.spacingAngle = separacionMenuUsuarios;

        menuActivator.CloseMenuPublic(0f);
        menuActivator.OpenMenu(opcionesActivas, true);

        StartCoroutine(AssignarTextosMenuUsuari());
    }

    IEnumerator AssignarTextosMenuUsuari()
    {
        // Esperamos a que FanMenu termine de construir
        yield return null;
        yield return new WaitForEndOfFrame();

        if (menuActivator == null || menuActivator.fanMenu == null)
            yield break;

        List<FanOption> options = new List<FanOption>();
        foreach (Transform child in menuActivator.fanMenu.transform)
        {
            FanOption fo = child.GetComponent<FanOption>();
            if (fo != null)
                options.Add(fo);
        }

        if (options.Count != labelsPaginaActual.Count)
        {
            Debug.LogWarning($"[MENÚ] Nº de botones visuales ({options.Count}) distinto de labels lógicos ({labelsPaginaActual.Count})");
        }

        // Lo importante: usar optionIndex, no el orden visual del transform
        foreach (FanOption option in options)
        {
            option.gameObject.SetActive(true);
            int logicalIndex = option.optionIndex;

            if (logicalIndex < 0 || logicalIndex >= labelsPaginaActual.Count)
            {
                Debug.LogWarning($"[MENÚ] optionIndex inválido: {logicalIndex}");
                continue;
            }

            TMPro.TMP_Text texto = option.GetComponentInChildren<TMPro.TMP_Text>(true);
            if (texto == null)
            {
                Debug.LogError($"[MENÚ] Botón con optionIndex={logicalIndex} sin TMP_Text");
                continue;
            }

            texto.text = labelsPaginaActual[logicalIndex];
            Debug.Log($"[MENÚ] optionIndex={logicalIndex} -> '{labelsPaginaActual[logicalIndex]}'");
        }
    }

    void IniciarExperienciaVR()
    {
        try
        {
            if (transicionVRActiva)
            {
                Debug.Log("[VR] Ignorada solicitud de entrada: transicion VR ya activa.");
                return;
            }

            if (faseActual == FaseApp.Jugando && salaMultisensorial != null && salaMultisensorial.activeInHierarchy)
            {
                Debug.Log("[VR] Ignorada solicitud de entrada: la sala VR ya esta activa.");
                return;
            }

            Debug.Log("[VR] Iniciando transicion a VR.");
            transicionVRActiva = true;
            faseActual = FaseApp.Jugando;
            StartCoroutine(RutinaTransicionVR());
        }
        catch (Exception e)
        {
            transicionVRActiva = false;
            Debug.LogError("[VR] Error iniciando experiencia VR: " + e);
        }
    }

    IEnumerator IniciarExperienciaVRParaUsuarioIndependiente(string nombreUsuario)
    {
        User usuario = BuscarUsuarioIndependiente(nombreUsuario);
        if (usuario == null || usuario.id_usuari <= 0)
        {
            IniciarExperienciaVR();
            yield break;
        }

        AplicarTipoSala(GetTipoSalaUsuario(usuario), false);

        UserAPI.LastSessionResponse ultimaSesion = null;
        string error = null;

        yield return StartCoroutine(UserAPI.GetLastSession(
            usuario.id_usuari,
            session => ultimaSesion = session,
            err => error = err
        ));

        if (!string.IsNullOrEmpty(error))
        {
            Debug.LogWarning("No se pudo cargar la ultima sesion del usuario independiente: " + error);
            IniciarExperienciaVR();
            yield break;
        }

        AplicarConfiguracionUltimaSesion(ultimaSesion);

        List<string> ids = ExtraerIdsVrUltimaSesion(ultimaSesion);
        if (ids.Count > 0)
        {
            yield return StartCoroutine(PrepararEntradaVRSegura(ids));
        }
        else
        {
            IniciarExperienciaVR();
        }
    }

    IEnumerator IniciarExperienciaVRMultidispositivoDesdeUltimaSesion(int userId)
    {
        if (recuperandoSesionMultidispositivo)
            yield break;

        recuperandoSesionMultidispositivo = true;

        UserAPI.LastSessionResponse ultimaSesion = null;
        string error = null;

        yield return StartCoroutine(UserAPI.GetLastSession(
            userId,
            session => ultimaSesion = session,
            err => error = err
        ));

        if (!string.IsNullOrEmpty(error))
        {
            Debug.LogWarning("[MULTI] No se pudo cargar la ultima sesion del usuario multidispositivo: " + error);
            EnviarSeleccionPreparacionATablet(new List<string>());
            ProcesarCambioAPassthrough(FaseApp.Jugando);
            recuperandoSesionMultidispositivo = false;
            yield break;
        }

        List<string> ids = ExtraerIdsVrUltimaSesion(ultimaSesion);
        if (ultimaSesion == null || ids.Count == 0)
        {
            Debug.Log("[MULTI] Sin ultima sesion VR recuperable. Se mantiene el modo seleccion.");
            AplicarConfiguracionUltimaSesion(ultimaSesion);
            EnviarSeleccionPreparacionATablet(new List<string>());
            ProcesarCambioAPassthrough(FaseApp.Jugando);
            recuperandoSesionMultidispositivo = false;
            yield break;
        }

        Debug.Log($"[MULTI] Ultima sesion recuperada para usuario {userId}. Elementos cargados en seleccion: {string.Join(",", ids)}");
        AplicarConfiguracionUltimaSesion(ultimaSesion);
        omitirSiguienteSyncVr = true;
        RecibirDatosDeLaTablet(ids);
        EnviarSeleccionPreparacionATablet(ids);
        ProcesarCambioAPassthrough(FaseApp.Jugando);

        recuperandoSesionMultidispositivo = false;
    }

    User BuscarUsuarioIndependiente(string nombreUsuario)
    {
        if (string.IsNullOrEmpty(nombreUsuario))
            return null;

        foreach (User usuario in usuariosIndependientes)
        {
            if (usuario != null && usuario.nom == nombreUsuario)
                return usuario;
        }

        return null;
    }

    string GetTipoSalaUsuario(User usuario)
    {
        if (usuario == null)
            return RoomWhite;

        if (!string.IsNullOrEmpty(usuario.tipus_sala))
            return usuario.tipus_sala;

        return usuario.entorn_adult ? RoomAdult : RoomChild;
    }

    void AplicarConfiguracionUltimaSesion(UserAPI.LastSessionResponse session)
    {
        if (session == null)
            return;

        string postura = !string.IsNullOrEmpty(session.postura_actual)
            ? session.postura_actual
            : (!string.IsNullOrEmpty(session.postura_final) ? session.postura_final : session.postura_inicial);

        usuarioSentado = postura == "SENTADO";
        permisoMenuVRTerapeuta = session.menu_mans_actiu;
        permisoParticulasTerapeuta = session.particules_mans_actives;

        if (particulasManoIzquierda != null) particulasManoIzquierda.SetActive(permisoParticulasTerapeuta);
        if (particulasManoDerecha != null) particulasManoDerecha.SetActive(permisoParticulasTerapeuta);
    }

    List<string> ExtraerIdsVrUltimaSesion(UserAPI.LastSessionResponse session)
    {
        List<string> ids = new List<string>();
        if (session == null || session.vr_elements == null)
            return ids;

        List<UserAPI.LastSessionElement> elementos = new List<UserAPI.LastSessionElement>(session.vr_elements);
        elementos.Sort((a, b) => a.numero_posicio.CompareTo(b.numero_posicio));

        foreach (var elemento in elementos)
        {
            if (elemento != null && !string.IsNullOrEmpty(elemento.id_element) && !ids.Contains(elemento.id_element))
            {
                ids.Add(elemento.id_element);
                if (ids.Count >= 6)
                    break;
            }
        }

        return ids;
    }

    IEnumerator RutinaTransicionVR()
    {
        QuestCrashDiagnostics.Log("[VR] RutinaTransicionVR begin. " + CrearResumenEstadoVr());
        Debug.Log("[VR] Transicion: fade a negro.");
        if (teleportManager != null)
        {
            QuestCrashDiagnostics.Log("[VR] FadeToBlack begin.");
            yield return StartCoroutine(teleportManager.FadeToBlack(1.5f));
            QuestCrashDiagnostics.Log("[VR] FadeToBlack end.");
        }

        bool errorTransicion = false;
        try
        {
            Debug.Log("[VR] Transicion: activando sala virtual.");
            if (passthroughLayer != null)
            {
                QuestCrashDiagnostics.Log("[VR] passthrough hidden=true begin.");
                Debug.Log("[VR] Paso: ocultando passthrough.");
                passthroughLayer.hidden = true;
                Debug.Log("[VR] Paso OK: passthrough oculto.");
                QuestCrashDiagnostics.Log("[VR] passthrough hidden=true end.");
            }

            QuestCrashDiagnostics.Log("[VR] PausarInteraccionesObjetosSala begin.");
            PausarInteraccionesObjetosSala();
            QuestCrashDiagnostics.Log("[VR] PausarInteraccionesObjetosSala end.");

            Debug.Log("[VR] Paso: SetSalaActiva(true) inicio.");
            SetSalaActiva(true);
            Debug.Log("[VR] Paso OK: SetSalaActiva(true) fin.");

            if (menuActivator != null)
            {
                menuActivator.isLockedOpen = false;
                // AHORA DEPENDE DE LO QUE HAYA DICHO LA TABLET:
                menuActivator.canOpenWithPalms = permisoMenuVRTerapeuta;
                menuActivator.distancia = distanciaMenuUsuarios;
                if (menuActivator.fanMenu != null) menuActivator.fanMenu.spacingAngle = separacionMenuUsuarios;
            }
        }
        catch (Exception e)
        {
            errorTransicion = true;
            transicionVRActiva = false;
            ReanudarInteraccionesObjetosSala();
            QuestCrashDiagnostics.LogError("[VR] Error durante activacion de sala: " + e);
            Debug.LogError("[VR] Error durante activacion de sala: " + e);
            Debug.LogWarning("[VR-DIAG] " + CrearResumenEstadoVr());
        }

        if (errorTransicion)
            yield break;

        Debug.Log("[VR] Transicion: fade a claro.");
        if (teleportManager != null)
        {
            Debug.Log("[VR] Paso: FadeToClear inicio.");
            QuestCrashDiagnostics.Log("[VR] FadeToClear begin.");
            yield return StartCoroutine(teleportManager.FadeToClear(1.5f));
            Debug.Log("[VR] Paso OK: FadeToClear fin.");
            QuestCrashDiagnostics.Log("[VR] FadeToClear end.");
        }

        try
        {
            QuestCrashDiagnostics.Log("[VR] ReanudarInteraccionesObjetosSala begin.");
            ReanudarInteraccionesObjetosSala();
            QuestCrashDiagnostics.Log("[VR] ReanudarInteraccionesObjetosSala end.");
            QuestCrashDiagnostics.Log("[VR] AplicarAjustesPostEntradaVr begin.");
            AplicarAjustesPostEntradaVr();
            QuestCrashDiagnostics.Log("[VR] AplicarAjustesPostEntradaVr end.");
            Debug.Log("[VR-DIAG] Estado tras entrada VR: " + CrearResumenEstadoVr());
            QuestCrashDiagnostics.Log("[VR-DIAG] Estado tras entrada VR: " + CrearResumenEstadoVr());
        }
        catch (Exception e)
        {
            QuestCrashDiagnostics.LogError("[VR] Error durante post-ajustes VR: " + e);
            Debug.LogError("[VR] Error durante post-ajustes VR: " + e);
            Debug.LogWarning("[VR-DIAG] " + CrearResumenEstadoVr());
        }

        transicionVRActiva = false;
        Debug.Log("[VR] Transicion a VR completada.");
        QuestCrashDiagnostics.Log("[VR] RutinaTransicionVR end.");
    }

    // ==========================================
    // EMERGENCIA SOS (CON FUNDIDO R�PIDO)
    // ==========================================

    public void ActivarModoSOS()
    {
        // Lanzamos la corrutina de emergencia
        StartCoroutine(RutinaSOS());
    }

    IEnumerator RutinaSOS()
    {
        Debug.LogWarning("�SE�AL SOS RECIBIDA! Iniciando fundido de emergencia.");

        // 1. Pantalla a negro r�pida (0.4 segundos). Cortamos est�mulos casi al instante.
        if (teleportManager != null)
            yield return StartCoroutine(teleportManager.FadeToBlack(0.4f));

        // 2. En la oscuridad, apagamos la sala virtual y activamos las c�maras reales
        SetSalaActiva(false);
        if (passthroughLayer != null) passthroughLayer.hidden = false;

        // Bloqueamos el men� por seguridad
        if (menuActivator != null) menuActivator.canOpenWithPalms = false;

        // 3. Volvemos a la luz del mundo real a una velocidad suave (1.0 segundos)
        if (teleportManager != null)
            yield return StartCoroutine(teleportManager.FadeToClear(1.0f));

        Debug.Log("Rescate completado. Usuario en Passthrough.");
    }

    void Update()
    {
        // 1. L�gica de cuando la tablet se conecta por primera vez
        if (tabletRecienConectada && faseActual == FaseApp.EsperandoConexion)
        {
            tabletRecienConectada = false;

            if (canvasConexion != null) canvasConexion.SetActive(false);
            if (lanDiscovery != null) lanDiscovery.StopBroadcast();

            ProcesarCambioAPassthrough(FaseApp.Jugando);
        }

        // 2. NUEVO: L�GICA PARA ESCUCHAR A LA TABLET CONSTANTEMENTE
        if (connectionServer != null && connectionServer.connected)
        {
            // TryDequeue saca los mensajes de la cola de tu script Connection
            while (connectionServer.messageQueue.TryDequeue(out string mensajeRecibido))
            {
                // --- COMANDOS ESPECIALES ---
                if (mensajeRecibido == "CMD_SOS")
                {
                    ActivarModoSOS();
                    continue;
                }
                else if (mensajeRecibido == "CMD_TUTORIAL")
                {
                    // Delegamos la decisi�n a nuestra nueva funci�n inteligente
                    ProcesarCambioAPassthrough(FaseApp.Tutorial);
                    continue;
                }
                else if (mensajeRecibido == "CMD_PREPARACION")
                {
                    // Delegamos la decisi�n (usamos FaseApp.Jugando pero con la sala apagada)
                    ProcesarCambioAPassthrough(FaseApp.Jugando);
                    continue;
                }
                else if (mensajeRecibido.StartsWith("USER:"))
                {
                    string rawUserId = mensajeRecibido.Split(':')[1];
                    if (int.TryParse(rawUserId, out int userId) && userId > 0)
                    {
                        if (userId == ultimoUsuarioMultidispositivoProcesado)
                            continue;

                        ultimoUsuarioMultidispositivoProcesado = userId;
                        Debug.Log("[MULTI] Usuario recibido desde tablet: " + userId);
                        StartCoroutine(IniciarExperienciaVRMultidispositivoDesdeUltimaSesion(userId));
                    }
                    else
                    {
                        Debug.LogWarning("[MULTI] USER recibido con id invalido: " + mensajeRecibido);
                    }

                    continue;
                }
                else if (mensajeRecibido.StartsWith("ROOM:"))
                {
                    string tipoSala = mensajeRecibido.Substring("ROOM:".Length);
                    AplicarTipoSala(tipoSala, faseActual == FaseApp.Jugando);
                    continue;
                }
                else if (mensajeRecibido.StartsWith(AmbientLightLevelCommand))
                {
                    Debug.Log("[LUZ] Recibido nivel de luz desde tablet: " + mensajeRecibido);
                    AplicarNivelLuzAmbiente(mensajeRecibido.Substring(AmbientLightLevelCommand.Length));
                    continue;
                }

                // --- COMANDOS CON DATOS ---
                if (mensajeRecibido.StartsWith("TUTORIAL:"))
                {
                    string idColor = mensajeRecibido.Split(':')[1];
                    MostrarElementoEnTutorial(idColor);
                }
                else if (mensajeRecibido.StartsWith("VR:"))
                {
                    try
                    {
                        string colores = mensajeRecibido.Substring("VR:".Length);
                        Debug.Log("[VR] Solicitud de entrada recibida: " + colores);
                        List<string> listaParaElEscenario = new List<string>(colores.Split(','));

                        StartCoroutine(PrepararEntradaVRSegura(listaParaElEscenario));
                    }
                    catch (Exception e)
                    {
                        Debug.LogError("[VR] Error preparando entrada a VR: " + e);
                    }
                }
                else if (mensajeRecibido.StartsWith("TELEPORT:"))
                {
                    string idColor = mensajeRecibido.Split(':')[1];

                    if (mapaDeTeleports.ContainsKey(idColor))
                    {
                        Transform destino = mapaDeTeleports[idColor];
                        if (teleportManager != null)
                        {
                            teleportManager.ForzarTeletransporte(destino, () =>
                            {
                                if (connectionServer != null && connectionServer.connected)
                                {
                                    connectionServer.Send("UBICACION:" + idColor);
                                }
                            });
                        }
                    }
                }
                else if (mensajeRecibido == "CMD_TRACKING:ON")
                {
                    if (headTracker != null) headTracker.transmitiendo = true;
                    continue;
                }
                else if (mensajeRecibido == "CMD_TRACKING:OFF")
                {
                    if (headTracker != null) headTracker.transmitiendo = false;
                    continue;
                }
                else if (mensajeRecibido == "POSTURA:SENTADO")
                {
                    usuarioSentado = true;
                    Debug.Log("Modo Sentado: Restringiendo aparici�n a 180� frontales.");
                    continue;
                }
                else if (mensajeRecibido == "POSTURA:DE_PIE")
                {
                    usuarioSentado = false;
                    Debug.Log("Modo De Pie: Aparici�n 360� libre.");
                    continue;
                }
                else if (mensajeRecibido == "CMD_MENU_VR:ON")
                {
                    permisoMenuVRTerapeuta = true;
                    if (faseActual == FaseApp.Jugando && menuActivator != null) menuActivator.canOpenWithPalms = true;
                    continue;
                }
                else if (mensajeRecibido == "CMD_MENU_VR:OFF")
                {
                    permisoMenuVRTerapeuta = false;
                    if (menuActivator != null) menuActivator.canOpenWithPalms = false;
                    continue;
                }
                else if (mensajeRecibido == "CMD_PARTICULAS:ON")
                {
                    permisoParticulasTerapeuta = true;
                    // Encendemos los objetos de part�culas en vivo
                    if (particulasManoIzquierda != null) particulasManoIzquierda.SetActive(true);
                    if (particulasManoDerecha != null) particulasManoDerecha.SetActive(true);
                    continue;
                }
                else if (mensajeRecibido == "CMD_PARTICULAS:OFF")
                {
                    permisoParticulasTerapeuta = false;
                    // Apagamos los objetos de part�culas en vivo
                    if (particulasManoIzquierda != null) particulasManoIzquierda.SetActive(false);
                    if (particulasManoDerecha != null) particulasManoDerecha.SetActive(false);
                    continue;
                }
            }
        }
    }
  
    // --- NUEVO: Funci�n que decide si hace falta fundido o no ---
    private void AplicarNivelLuzAmbiente(string rawLevel)
    {
        if (!int.TryParse(rawLevel, out int level))
        {
            Debug.LogWarning("[LUZ] Nivel de luz ambiente invalido: " + rawLevel);
            return;
        }

        if (ambientLightController == null)
            ambientLightController = FindFirstObjectByType<AmbientLightIntensityController>();

        if (ambientLightController == null)
        {
            Debug.LogWarning("[LUZ] No hay AmbientLightIntensityController asignado en la escena.");
            return;
        }

        ambientLightController.SetLevel(level);
        Debug.Log("[LUZ] Nivel de luz aplicado en VR: " + level);

        if (connectionServer != null && connectionServer.connected)
            connectionServer.Send(AmbientLightLevelCommand + ambientLightController.currentLevel);
    }

    private void ProcesarCambioAPassthrough(FaseApp nuevaFase)
    {
        if (faseActual == FaseApp.Jugando && salaMultisensorial != null && salaMultisensorial.activeSelf)
        {
            // CASO A: Venimos del mundo VR (inmersi�n total). S� hacemos el fundido lento a negro.
            StartCoroutine(RutinaSalidaLentaDeVR(nuevaFase));
        }
        else
        {
            // CASO B: Ya est�bamos en Passthrough (ej: pasando de Tutorial a Preparaci�n).
            // NO hay fundido a negro de pantalla. Solo borramos el objeto 3D del tutorial al instante.
            faseActual = nuevaFase;

            DestruirElementoTutorialActivo();

            // Nos aseguramos de que el entorno VR est� apagado y las c�maras encendidas
            SetSalaActiva(false);
            if (passthroughLayer != null) passthroughLayer.hidden = false;
            if (menuActivator != null) menuActivator.canOpenWithPalms = false;
        }
    }

    // --- NUEVO: Rutina de salida MUCHO m�s lenta y agradable ---
    IEnumerator RutinaSalidaLentaDeVR(FaseApp nuevaFase)
    {
        // 1. Pantalla a negro lentamente (1.0 segundos para no asustar)
        if (teleportManager != null)
            yield return StartCoroutine(teleportManager.FadeToBlack(1.0f));

        // 2. EN LA OSCURIDAD: Hacemos el cambiazo
        faseActual = nuevaFase;
        DestruirElementoTutorialActivo();

        SetSalaActiva(false);
        DestruirObjetosSalaGenerados();
        if (passthroughLayer != null) passthroughLayer.hidden = false;
        if (menuActivator != null) menuActivator.canOpenWithPalms = false;

        // 3. Volvemos a la luz del mundo real a la misma velocidad c�moda que te gust� (1.5 segundos)
        if (teleportManager != null)
            yield return StartCoroutine(teleportManager.FadeToClear(1.5f));

        Debug.Log($"Salida suave de VR completada. Passthrough n�tido.");
    }

    // ==========================================
    // NUEVO: FASE TUTORIAL
    // ==========================================
    void MostrarElementoEnTutorial(string idTablet)
    {
        if (elementoTutorialActivo != null && idTutorialActivo == idTablet)
            return;
        DestruirElementoTutorialActivo();
        idTutorialActivo = idTablet;

        ElementoVR elemento = baseDatosElementos.Find(e => e.idElemento == idTablet);

        if (elemento.prefabHabitacion != null && Camera.main != null)
        {
            Transform cam = Camera.main.transform;
            Vector3 direccionPlana = cam.forward;
            direccionPlana.y = 0;
            if (direccionPlana.sqrMagnitude < 0.001f)
                direccionPlana = cam.transform.forward;
            direccionPlana.Normalize();

            Vector3 posicionCabezaSegura = ObtenerPosicionCabezaSegura(cam);
            float headY = posicionCabezaSegura.y;
            float floorY = 0f;
            float targetCenterY = Mathf.Lerp(floorY, headY, Mathf.Clamp01(tutorialCenterHeightRatio));
            float distanciaInicial = Mathf.Max(0.1f, tutorialDistanceFromHead, tutorialMinDistance);
            Vector3 posicionAparicion = posicionCabezaSegura + (direccionPlana * distanciaInicial);
            posicionAparicion.y = targetCenterY;

            elementoTutorialActivo = Instantiate(elemento.prefabHabitacion, posicionAparicion, Quaternion.LookRotation(-direccionPlana, Vector3.up));
            PrepararPreviewTutorial(elementoTutorialActivo);
            AplicarColocacionTutorial(elementoTutorialActivo, posicionCabezaSegura, direccionPlana, floorY, targetCenterY, idTablet);

            Debug.Log($"[TUTORIAL] Mostrando {idTablet}. {CrearResumenObjeto(elementoTutorialActivo, idTablet)}");

            if (connectionServer != null && connectionServer.connected)
            {
                Transform transformTutorial = elementoTutorialActivo.transform;
                Vector3 posicionReal = transformTutorial.position;

                string px = posicionReal.x.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
                string py = posicionReal.y.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
                string pz = posicionReal.z.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
                string ry = transformTutorial.eulerAngles.y.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);

                connectionServer.Send($"SYNC_TUT:{idTablet}|{px},{py},{pz}|{ry}");
            }
        }
    }

    void AlinearCentroVisualTutorial(GameObject objeto, float targetCenterY)
    {
        if (objeto == null)
            return;

        Bounds bounds;
        if (!TryGetRendererBounds(objeto, out bounds))
            return;

        Vector3 pos = objeto.transform.position;
        pos.y += targetCenterY - bounds.center.y;
        objeto.transform.position = pos;
    }

    Vector3 ObtenerPosicionCabezaSegura(Transform cam)
    {
        Vector3 posicion = cam != null ? cam.position : Vector3.zero;
        float minHead = Mathf.Max(0.1f, tutorialMinHeadHeight);
        float maxHead = Mathf.Max(minHead, tutorialMaxHeadHeight);

        if (float.IsNaN(posicion.y) || float.IsInfinity(posicion.y) || posicion.y < minHead || posicion.y > maxHead)
        {
            Debug.LogWarning($"[TUTORIAL] Altura de camara no valida ({posicion.y:F2}); usando altura asumida {tutorialAssumedPlayerHeight:F2}.");
            posicion.y = Mathf.Clamp(tutorialAssumedPlayerHeight, minHead, maxHead);
        }

        return posicion;
    }

    void PrepararPreviewTutorial(GameObject objeto)
    {
        if (objeto == null)
            return;

        MonoBehaviour[] behaviours = objeto.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour != null)
                behaviour.enabled = false;
        }

        AudioSource[] audioSources = objeto.GetComponentsInChildren<AudioSource>(true);
        foreach (AudioSource audioSource in audioSources)
        {
            if (audioSource == null)
                continue;

            audioSource.Stop();
            audioSource.enabled = false;
        }

        Collider[] colliders = objeto.GetComponentsInChildren<Collider>(true);
        foreach (Collider collider in colliders)
        {
            if (collider != null)
                collider.enabled = false;
        }
    }

    void AplicarColocacionTutorial(GameObject objeto, Vector3 posicionCabeza, Vector3 direccionPlana, float floorY, float targetCenterY, string idTablet)
    {
        if (objeto == null)
            return;

        Bounds bounds;
        float distancia = Mathf.Max(0.1f, tutorialDistanceFromHead, tutorialMinDistance);
        if (TryGetRendererBounds(objeto, out bounds))
        {
            float radioHorizontal = Mathf.Max(bounds.extents.x, bounds.extents.z);
            distancia = Mathf.Clamp(radioHorizontal + Mathf.Max(0f, tutorialBoundsPadding), tutorialMinDistance, tutorialMaxDistance);
        }

        Vector3 centroObjetivo = posicionCabeza + direccionPlana * distancia;
        centroObjetivo.y = targetCenterY;
        objeto.transform.rotation = Quaternion.LookRotation(-direccionPlana, Vector3.up);

        if (TryGetRendererBounds(objeto, out bounds))
            objeto.transform.position += centroObjetivo - bounds.center;
        else
            objeto.transform.position = centroObjetivo;

        if (TryGetRendererBounds(objeto, out bounds) && bounds.min.y < floorY)
            objeto.transform.position += Vector3.up * (floorY - bounds.min.y);

        Debug.Log($"[TUTORIAL] {idTablet}: distancia={distancia:F2} floorY={floorY:F2} targetCenterY={targetCenterY:F2}");
    }

    bool TryGetRendererBounds(GameObject root, out Bounds bounds)
    {
        bool hasBounds = false;
        bounds = new Bounds(root != null ? root.transform.position : Vector3.zero, Vector3.zero);
        if (root == null)
            return false;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || renderer is ParticleSystemRenderer || renderer is LineRenderer || renderer is TrailRenderer)
                continue;

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds;
    }

    bool TryGetRendererBoundsExcluyendoObjetosGenerados(GameObject root, out Bounds bounds)
    {
        bool hasBounds = false;
        bounds = new Bounds(root != null ? root.transform.position : Vector3.zero, Vector3.zero);
        if (root == null)
            return false;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || renderer is ParticleSystemRenderer || renderer is LineRenderer || renderer is TrailRenderer)
                continue;

            if (EsParteDeObjetoGenerado(renderer.transform))
                continue;

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds;
    }

    bool EsParteDeObjetoGenerado(Transform transformObjetivo)
    {
        if (transformObjetivo == null)
            return false;

        foreach (GameObject obj in objetosGeneradosEnSala)
        {
            if (obj == null)
                continue;

            Transform actual = transformObjetivo;
            while (actual != null)
            {
                if (actual == obj.transform)
                    return true;

                actual = actual.parent;
            }
        }

        return false;
    }

    void DestruirElementoTutorialActivo()
    {
        if (elementoTutorialActivo == null)
            return;

        DestruirInstanciaPrefabSegura(elementoTutorialActivo);
        elementoTutorialActivo = null;
        idTutorialActivo = null;
    }

    // ==========================================
    // FASE VR COMPLETA
    // ==========================================
    IEnumerator PrepararEntradaVRSegura(List<string> elementosSeleccionadosTablet)
    {
        QuestCrashDiagnostics.Log("[VR] PrepararEntradaVRSegura begin count=" + (elementosSeleccionadosTablet != null ? elementosSeleccionadosTablet.Count : 0)
            + " preparando=" + preparandoEntradaVR + " transicion=" + transicionVRActiva);

        if (preparandoEntradaVR || transicionVRActiva)
        {
            Debug.Log("[VR] Ignorada preparacion: ya hay una entrada VR en curso.");
            QuestCrashDiagnostics.LogWarning("[VR] PrepararEntradaVRSegura ignored: already busy.");
            yield break;
        }

        preparandoEntradaVR = true;

        QuestCrashDiagnostics.Log("[VR] PrepararEntradaVRSegura destroying tutorial begin.");
        DestruirElementoTutorialActivo();
        QuestCrashDiagnostics.Log("[VR] PrepararEntradaVRSegura destroying tutorial end.");
        yield return null;

        QuestCrashDiagnostics.Log("[VR] PrepararEntradaVRSegura RecibirDatosDeLaTablet begin.");
        RecibirDatosDeLaTablet(elementosSeleccionadosTablet);
        QuestCrashDiagnostics.Log("[VR] PrepararEntradaVRSegura RecibirDatosDeLaTablet end.");
        yield return null;

        preparandoEntradaVR = false;
        QuestCrashDiagnostics.Log("[VR] PrepararEntradaVRSegura IniciarExperienciaVR begin.");
        IniciarExperienciaVR();
        QuestCrashDiagnostics.Log("[VR] PrepararEntradaVRSegura end.");
    }

    public void RecibirDatosDeLaTablet(List<string> elementosSeleccionadosTablet)
    {
        RecibirDatosDeLaTabletConPosicionesFijas(elementosSeleccionadosTablet);
        return;
    }

    void RecibirDatosDeLaTabletConPosicionesFijas(List<string> elementosSeleccionadosTablet)
    {
        try
        {
            QuestCrashDiagnostics.Log("[VR] RecibirDatosDeLaTabletConPosicionesFijas begin.");
            if (elementosSeleccionadosTablet == null)
                elementosSeleccionadosTablet = new List<string>();

            List<string> idsSeleccionados = NormalizarListaElementos(elementosSeleccionadosTablet);
            Debug.Log("[VR] Preparando elementos: " + string.Join(",", idsSeleccionados));

            DestruirElementoTutorialActivo();

            EliminarObjetosNoSeleccionados(idsSeleccionados);
            CrearObjetosNuevosEnPuntosFijos(idsSeleccionados);
            ReconstruirEstadoVr(idsSeleccionados);
            Debug.Log("[VR-DIAG] Estado tras preparar elementos: " + CrearResumenEstadoVr());
            QuestCrashDiagnostics.Log("[VR-DIAG] Estado tras preparar elementos: " + CrearResumenEstadoVr());
        }
        catch (Exception e)
        {
            QuestCrashDiagnostics.LogError("[VR] Error reconstruyendo estado VR: " + e);
            Debug.LogError("[VR] Error reconstruyendo estado VR: " + e);
            Debug.LogWarning("[VR-DIAG] Estado tras error reconstruyendo VR: " + CrearResumenEstadoVr());
        }
    }

    List<string> NormalizarListaElementos(List<string> elementosSeleccionadosTablet)
    {
        List<string> ids = new List<string>();

        foreach (string id in elementosSeleccionadosTablet)
        {
            if (string.IsNullOrWhiteSpace(id))
                continue;

            string idNormalizado = id.Trim();
            if (ids.Contains(idNormalizado))
                continue;

            ids.Add(idNormalizado);

            if (ids.Count >= 6)
                break;
        }

        return ids;
    }

    void EliminarObjetosNoSeleccionados(List<string> idsSeleccionados)
    {
        List<string> idsAEliminar = new List<string>();

        foreach (var kv in objetosGeneradosPorId)
        {
            if (!idsSeleccionados.Contains(kv.Key))
                idsAEliminar.Add(kv.Key);
        }

        foreach (string id in idsAEliminar)
        {
            if (objetosGeneradosPorId.TryGetValue(id, out GameObject obj) && obj != null)
            {
                objetosGeneradosEnSala.Remove(obj);
                DestruirInstanciaPrefabSegura(obj);
            }

            objetosGeneradosPorId.Remove(id);
            puntosAsignadosPorId.Remove(id);
            mapaDeTeleports.Remove(id);
        }
    }

    void DestruirObjetosSalaGenerados()
    {
        foreach (GameObject obj in objetosGeneradosEnSala)
        {
            if (obj != null)
                DestruirInstanciaPrefabSegura(obj);
        }

        objetosGeneradosEnSala.Clear();
        objetosGeneradosPorId.Clear();
        puntosAsignadosPorId.Clear();
        mapaDeTeleports.Clear();
        idsOrdenadosParaTeleport.Clear();
        opcionesMenuJugando.Clear();

        if (teleportManager != null)
            teleportManager.teleportDestinations = null;

        if (menuActivator != null)
            menuActivator.opcionesDePartida = opcionesMenuJugando;
    }

    void DestruirInstanciaPrefabSegura(GameObject instancia)
    {
        if (instancia == null)
            return;

        QuestCrashDiagnostics.Log("[VR] DestruirInstanciaPrefabSegura begin name=" + instancia.name);

        AudioSource[] audioSources = instancia.GetComponentsInChildren<AudioSource>(true);
        foreach (AudioSource audioSource in audioSources)
        {
            if (audioSource != null)
                audioSource.Stop();
        }

        Collider[] colliders = instancia.GetComponentsInChildren<Collider>(true);
        foreach (Collider collider in colliders)
        {
            if (collider != null)
                collider.enabled = false;
        }

        MonoBehaviour[] behaviours = instancia.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour != null)
                behaviour.enabled = false;
        }

        instancia.SetActive(false);
        Destroy(instancia);
        QuestCrashDiagnostics.Log("[VR] DestruirInstanciaPrefabSegura end name=" + instancia.name);
    }

    void PausarInteraccionesObjetosSala()
    {
        interaccionesVrPausadas.Clear();

        foreach (GameObject obj in objetosGeneradosEnSala)
        {
            if (obj == null)
                continue;

            MonoBehaviour[] behaviours = obj.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour == null || !behaviour.enabled)
                    continue;

                System.Type tipo = behaviour.GetType();
                string nombreTipo = tipo != null ? tipo.FullName : "";
                if (string.IsNullOrEmpty(nombreTipo) || !nombreTipo.StartsWith("Oculus.Interaction"))
                    continue;

                behaviour.enabled = false;
                interaccionesVrPausadas.Add(behaviour);
            }
        }

        if (interaccionesVrPausadas.Count > 0)
            Debug.Log($"[VR] Interacciones Oculus pausadas durante entrada: {interaccionesVrPausadas.Count}");
    }

    void ReanudarInteraccionesObjetosSala()
    {
        if (interaccionesVrPausadas.Count == 0)
            return;

        int reanudadas = 0;
        foreach (MonoBehaviour behaviour in interaccionesVrPausadas)
        {
            if (behaviour == null)
                continue;

            behaviour.enabled = true;
            reanudadas++;
        }

        interaccionesVrPausadas.Clear();
        Debug.Log($"[VR] Interacciones Oculus reanudadas tras entrada: {reanudadas}");
    }

    void CrearObjetosNuevosEnPuntosFijos(List<string> idsSeleccionados)
    {
        if (baseDatosElementos == null || baseDatosElementos.Count == 0)
        {
            Debug.LogWarning("[VR] No hay baseDatosElementos configurada.");
            return;
        }

        if (puntosDeAparicion == null || puntosDeAparicion.Count == 0)
        {
            Debug.LogWarning("[VR] No hay puntosDeAparicion configurados.");
            return;
        }

        foreach (string idTablet in idsSeleccionados)
        {
            try
            {
                if (objetosGeneradosPorId.ContainsKey(idTablet))
                    continue;

                ElementoVR elemento = baseDatosElementos.Find(e => e.idElemento == idTablet);
                if (elemento.prefabHabitacion == null)
                {
                    Debug.LogWarning("[VR] Elemento no encontrado o sin prefabHabitacion: " + idTablet);
                    continue;
                }

                if (!TryGetPuntoLibreParaElemento(idTablet, out PuntoSala puntoElegido))
                {
                    Debug.LogWarning("No quedan puntos libres para colocar el elemento: " + idTablet);
                    continue;
                }

                Transform parentTransform = salaMultisensorial != null ? salaMultisensorial.transform : null;
                Debug.Log("[VR] Instanciando elemento '" + idTablet + "' con prefab '" + elemento.prefabHabitacion.name + "' en punto " + puntoElegido.puntoAparicion.position);
                QuestCrashDiagnostics.Log("[VR] Instantiate begin id=" + idTablet + " prefab=" + elemento.prefabHabitacion.name
                    + " parent=" + (parentTransform != null ? parentTransform.name : "null"));
                GameObject nuevoObjetoSala = Instantiate(elemento.prefabHabitacion, puntoElegido.puntoAparicion.position, puntoElegido.puntoAparicion.rotation, parentTransform);
                QuestCrashDiagnostics.Log("[VR] Instantiate end id=" + idTablet + " instance=" + nuevoObjetoSala.name);

                objetosGeneradosEnSala.Add(nuevoObjetoSala);
                objetosGeneradosPorId[idTablet] = nuevoObjetoSala;
                puntosAsignadosPorId[idTablet] = puntoElegido;
                mapaDeTeleports[idTablet] = puntoElegido.puntoTeletransporte;

                AplicarColocacionFijaEnPunto(nuevoObjetoSala, puntoElegido, idTablet);
                Debug.Log("[VR-DIAG] Elemento colocado: " + CrearResumenObjeto(nuevoObjetoSala, idTablet));
                QuestCrashDiagnostics.Log("[VR-DIAG] Elemento colocado: " + CrearResumenObjeto(nuevoObjetoSala, idTablet));
            }
            catch (Exception e)
            {
                QuestCrashDiagnostics.LogError("[VR] Error instanciando elemento '" + idTablet + "': " + e);
                Debug.LogError("[VR] Error instanciando elemento '" + idTablet + "': " + e);
                Debug.LogWarning("[VR-DIAG] Estado tras error instanciando '" + idTablet + "': " + CrearResumenEstadoVr());
            }
        }
    }

    void AlinearMinimoVisualConSuelo(GameObject objetoSala, float sueloY, string idTablet)
    {
        if (objetoSala == null)
            return;

        Bounds bounds;
        if (!TryGetRendererBounds(objetoSala, out bounds))
            return;

        float ajusteY = sueloY - bounds.min.y;
        if (Mathf.Abs(ajusteY) < 0.001f)
            return;

        if (Mathf.Abs(ajusteY) > 2f)
        {
            Debug.LogWarning($"[VR] {idTablet}: alineacion al suelo omitida por ajusteY demasiado grande ({ajusteY:F3}).");
            return;
        }

        Vector3 posicion = objetoSala.transform.position;
        posicion.y += ajusteY;
        objetoSala.transform.position = posicion;

        Debug.Log($"[VR] {idTablet}: base visual alineada al suelo. minY={bounds.min.y:F3} sueloY={sueloY:F3} ajusteY={ajusteY:F3}");
    }

    void OrientarObjetoLejosDeParedMasCercana(GameObject objetoSala, Bounds boundsSala, string idTablet)
    {
        if (objetoSala == null)
            return;

        Bounds boundsObjeto;
        if (!TryGetRendererBounds(objetoSala, out boundsObjeto))
            return;

        Vector3 centro = boundsObjeto.center;
        float distanciaMinX = Mathf.Abs(centro.x - boundsSala.min.x);
        float distanciaMaxX = Mathf.Abs(boundsSala.max.x - centro.x);
        float distanciaMinZ = Mathf.Abs(centro.z - boundsSala.min.z);
        float distanciaMaxZ = Mathf.Abs(boundsSala.max.z - centro.z);

        Vector3 direccionInterior = Vector3.right;
        float distanciaMasCercana = distanciaMinX;

        if (distanciaMaxX < distanciaMasCercana)
        {
            distanciaMasCercana = distanciaMaxX;
            direccionInterior = Vector3.left;
        }

        if (distanciaMinZ < distanciaMasCercana)
        {
            distanciaMasCercana = distanciaMinZ;
            direccionInterior = Vector3.forward;
        }

        if (distanciaMaxZ < distanciaMasCercana)
            direccionInterior = Vector3.back;

        if (direccionInterior.sqrMagnitude < 0.001f)
            return;

        objetoSala.transform.rotation = Quaternion.LookRotation(direccionInterior, Vector3.up);
        Debug.Log($"[VR] {idTablet}: orientado lejos de la pared mas cercana. forward={direccionInterior}");
    }

    void AjustarBoundsObjetoDentroSala(GameObject objetoSala, Bounds boundsSala, float sueloY, string idTablet)
    {
        if (objetoSala == null)
            return;

        Bounds boundsObjeto;
        if (!TryGetRendererBounds(objetoSala, out boundsObjeto))
            return;

        float margen = Mathf.Max(0f, margenObjetoDentroSala);
        float minX = boundsSala.min.x + margen;
        float maxX = boundsSala.max.x - margen;
        float minZ = boundsSala.min.z + margen;
        float maxZ = boundsSala.max.z - margen;

        if (minX >= maxX || minZ >= maxZ)
        {
            Debug.LogWarning($"[VR] {idTablet}: no se pudo ajustar dentro de sala por bounds de sala invalidos.");
            return;
        }

        Vector3 ajuste = Vector3.zero;
        float anchoObjeto = boundsObjeto.size.x;
        float profundidadObjeto = boundsObjeto.size.z;

        if (anchoObjeto <= maxX - minX)
        {
            if (boundsObjeto.min.x < minX)
                ajuste.x += minX - boundsObjeto.min.x;
            if (boundsObjeto.max.x + ajuste.x > maxX)
                ajuste.x += maxX - (boundsObjeto.max.x + ajuste.x);
        }
        else
        {
            ajuste.x += boundsSala.center.x - boundsObjeto.center.x;
            Debug.LogWarning($"[VR] {idTablet}: ancho mayor que margen interior de sala; centrando en X.");
        }

        if (profundidadObjeto <= maxZ - minZ)
        {
            if (boundsObjeto.min.z < minZ)
                ajuste.z += minZ - boundsObjeto.min.z;
            if (boundsObjeto.max.z + ajuste.z > maxZ)
                ajuste.z += maxZ - (boundsObjeto.max.z + ajuste.z);
        }
        else
        {
            ajuste.z += boundsSala.center.z - boundsObjeto.center.z;
            Debug.LogWarning($"[VR] {idTablet}: profundidad mayor que margen interior de sala; centrando en Z.");
        }

        float maxAjuste = Mathf.Max(0.1f, maxAjusteCentradoObjeto);
        if (Mathf.Abs(ajuste.x) > maxAjuste || Mathf.Abs(ajuste.z) > maxAjuste)
        {
            Debug.LogWarning($"[VR] {idTablet}: ajuste XZ omitido por ser demasiado grande ({ajuste.x:F3}, {ajuste.z:F3}).");
            return;
        }

        if (Mathf.Abs(ajuste.x) > 0.001f || Mathf.Abs(ajuste.z) > 0.001f)
        {
            objetoSala.transform.position += ajuste;
            Debug.Log($"[VR] {idTablet}: bounds ajustados dentro de sala. ajusteXZ=({ajuste.x:F3}, {ajuste.z:F3})");
        }

        AlinearMinimoVisualConSuelo(objetoSala, sueloY, idTablet);
    }

    void AplicarColocacionFijaEnPunto(GameObject objetoSala, PuntoSala punto, string idTablet)
    {
        if (objetoSala == null || punto.puntoAparicion == null)
            return;

        Bounds boundsSala = default;
        bool hayBoundsSala = salaMultisensorial != null && TryGetRendererBoundsExcluyendoObjetosGenerados(salaMultisensorial, out boundsSala);
        float sueloY = punto.puntoAparicion.position.y;

        objetoSala.transform.rotation = punto.puntoAparicion.rotation;

        if (centrarBoundsVisualesEnPuntoAparicion)
            CentrarBoundsVisualesEnPunto(objetoSala, punto.puntoAparicion.position, idTablet);

        AlinearMinimoVisualConSuelo(objetoSala, sueloY, idTablet);

        if (hayBoundsSala)
        {
            OrientarObjetoLejosDeParedMasCercana(objetoSala, boundsSala, idTablet);

            if (centrarBoundsVisualesEnPuntoAparicion)
                CentrarBoundsVisualesEnPunto(objetoSala, punto.puntoAparicion.position, idTablet);

            AjustarBoundsObjetoDentroSala(objetoSala, boundsSala, sueloY, idTablet);
        }

        AjustarJumpingPointCercaDelElemento(objetoSala, punto.puntoTeletransporte, idTablet, hayBoundsSala, boundsSala);
    }

    void CentrarBoundsVisualesEnPunto(GameObject objetoSala, Vector3 puntoObjetivo, string idTablet)
    {
        if (objetoSala == null)
            return;

        Bounds bounds;
        if (!TryGetRendererBounds(objetoSala, out bounds))
            return;

        Vector3 ajuste = puntoObjetivo - bounds.center;
        ajuste.y = 0f;

        float maxAjuste = Mathf.Max(0.1f, maxAjusteCentradoObjeto);
        if (Mathf.Abs(ajuste.x) > maxAjuste || Mathf.Abs(ajuste.z) > maxAjuste)
        {
            Debug.LogWarning($"[VR] {idTablet}: centrado visual omitido por ajuste XZ demasiado grande ({ajuste.x:F3}, {ajuste.z:F3}).");
            return;
        }

        if (Mathf.Abs(ajuste.x) < 0.001f && Mathf.Abs(ajuste.z) < 0.001f)
            return;

        objetoSala.transform.position += ajuste;
        Debug.Log($"[VR] {idTablet}: bounds visuales centrados en punto fijo. ajusteXZ=({ajuste.x:F3}, {ajuste.z:F3})");
    }

    bool TryGetSiguientePuntoLibre(out PuntoSala puntoLibre)
    {
        if (puntosDeAparicion == null)
        {
            puntoLibre = default;
            return false;
        }

        int limite = Mathf.Min(6, puntosDeAparicion.Count);

        for (int i = 0; i < limite; i++)
        {
            PuntoSala punto = puntosDeAparicion[i];
            if (punto.puntoAparicion == null)
                continue;

            if (!PuntoSalaYaAsignado(punto))
            {
                puntoLibre = punto;
                return true;
            }
        }

        puntoLibre = default;
        return false;
    }

    bool TryGetPuntoLibreParaElemento(string idTablet, out PuntoSala puntoLibre)
    {
        if (string.Equals(idTablet, "negre", StringComparison.OrdinalIgnoreCase) && TryGetPuntoLibreInteriorParaObjetoGrande(out puntoLibre))
            return true;

        return TryGetSiguientePuntoLibre(out puntoLibre);
    }

    bool TryGetPuntoLibreInteriorParaObjetoGrande(out PuntoSala puntoLibre)
    {
        if (puntosDeAparicion == null)
        {
            puntoLibre = default;
            return false;
        }

        int limite = Mathf.Min(6, puntosDeAparicion.Count);
        Bounds boundsSala = default;
        bool hayBoundsSala = salaMultisensorial != null && TryGetRendererBoundsExcluyendoObjetosGenerados(salaMultisensorial, out boundsSala);
        Vector3 centroSala = hayBoundsSala ? boundsSala.center : (salaMultisensorial != null ? salaMultisensorial.transform.position : Vector3.zero);
        float margenInterior = 0.75f;

        bool encontrado = false;
        PuntoSala mejorPunto = default;
        float mejorDistancia = float.MaxValue;

        for (int i = 0; i < limite; i++)
        {
            PuntoSala punto = puntosDeAparicion[i];
            if (punto.puntoAparicion == null || PuntoSalaYaAsignado(punto))
                continue;

            Vector3 posicion = punto.puntoAparicion.position;
            if (hayBoundsSala && !PuntoDentroBoundsXZConMargen(posicion, boundsSala, margenInterior))
                continue;

            Vector3 delta = posicion - centroSala;
            delta.y = 0f;
            float distancia = delta.sqrMagnitude;
            if (!encontrado || distancia < mejorDistancia)
            {
                encontrado = true;
                mejorDistancia = distancia;
                mejorPunto = punto;
            }
        }

        if (encontrado)
        {
            puntoLibre = mejorPunto;
            Debug.Log("[VR] negre: punto interior elegido para objeto grande en " + mejorPunto.puntoAparicion.position);
            return true;
        }

        if (hayBoundsSala)
        {
            for (int i = 0; i < limite; i++)
            {
                PuntoSala punto = puntosDeAparicion[i];
                if (punto.puntoAparicion == null || PuntoSalaYaAsignado(punto))
                    continue;

                Vector3 delta = punto.puntoAparicion.position - centroSala;
                delta.y = 0f;
                float distancia = delta.sqrMagnitude;
                if (!encontrado || distancia < mejorDistancia)
                {
                    encontrado = true;
                    mejorDistancia = distancia;
                    mejorPunto = punto;
                }
            }

            if (encontrado)
            {
                puntoLibre = mejorPunto;
                Debug.LogWarning("[VR] negre: no habia punto con margen interior; usando punto libre mas centrado en " + mejorPunto.puntoAparicion.position);
                return true;
            }
        }

        puntoLibre = default;
        return false;
    }

    bool PuntoSalaYaAsignado(PuntoSala punto)
    {
        foreach (var asignado in puntosAsignadosPorId.Values)
        {
            if (asignado.puntoAparicion == punto.puntoAparicion)
                return true;
        }

        return false;
    }

    bool PuntoDentroBoundsXZConMargen(Vector3 posicion, Bounds bounds, float margen)
    {
        return posicion.x >= bounds.min.x + margen &&
               posicion.x <= bounds.max.x - margen &&
               posicion.z >= bounds.min.z + margen &&
               posicion.z <= bounds.max.z - margen;
    }

    void ReconstruirEstadoVr(List<string> idsSeleccionados)
    {
        string paqueteSync = "SYNC:";
        opcionesMenuJugando.Clear();
        idsOrdenadosParaTeleport.Clear();
        mapaDeTeleports.Clear();
        List<Transform> destinosTeleportActivos = new List<Transform>();

        foreach (string idTablet in idsSeleccionados)
        {
            if (!objetosGeneradosPorId.TryGetValue(idTablet, out GameObject objetoSala) || objetoSala == null)
                continue;

            ElementoVR elemento = baseDatosElementos.Find(e => e.idElemento == idTablet);
            if (elemento.prefabMenuMano != null)
                opcionesMenuJugando.Add(elemento.prefabMenuMano);

            if (puntosAsignadosPorId.TryGetValue(idTablet, out PuntoSala punto))
            {
                if (punto.puntoTeletransporte != null)
                {
                    destinosTeleportActivos.Add(punto.puntoTeletransporte);
                    mapaDeTeleports[idTablet] = punto.puntoTeletransporte;
                }
                else
                {
                    Debug.LogWarning("[VR] Punto de teletransporte no asignado para: " + idTablet);
                }

                idsOrdenadosParaTeleport.Add(idTablet);
            }

            paqueteSync += CrearEntradaSync(idTablet, objetoSala.transform) + ";";
        }

        if (paqueteSync.EndsWith(";"))
            paqueteSync = paqueteSync.Substring(0, paqueteSync.Length - 1);

        if (connectionServer != null && connectionServer.connected && !omitirSiguienteSyncVr)
            connectionServer.Send(paqueteSync);

        omitirSiguienteSyncVr = false;

        if (menuActivator != null)
            menuActivator.opcionesDePartida = opcionesMenuJugando;

        if (teleportManager != null)
            teleportManager.teleportDestinations = destinosTeleportActivos.ToArray();
    }

    void AplicarAjustesPostEntradaVr()
    {
        List<Transform> destinosTeleportActivos = new List<Transform>();

        foreach (string idTablet in idsOrdenadosParaTeleport)
        {
            if (!objetosGeneradosPorId.TryGetValue(idTablet, out GameObject objetoSala) || objetoSala == null)
                continue;

            if (!puntosAsignadosPorId.TryGetValue(idTablet, out PuntoSala punto))
                continue;

            AplicarColocacionFijaEnPunto(objetoSala, punto, idTablet);

            if (punto.puntoTeletransporte != null)
            {
                destinosTeleportActivos.Add(punto.puntoTeletransporte);
                mapaDeTeleports[idTablet] = punto.puntoTeletransporte;
            }
        }

        if (teleportManager != null)
            teleportManager.teleportDestinations = destinosTeleportActivos.ToArray();
    }

    void AjustarJumpingPointCercaDelElemento(GameObject objetoSala, Transform jumpingPoint, string idTablet, bool hayBoundsSala, Bounds boundsSala)
    {
        if (!ajustarJumpingPointsCercaDelElemento || objetoSala == null || jumpingPoint == null)
            return;

        Bounds bounds;
        if (!TryGetRendererBounds(objetoSala, out bounds))
            return;

        Vector3 centro = bounds.center;
        Vector3 direccion = jumpingPoint.position - centro;
        direccion.y = 0f;

        if (direccion.sqrMagnitude < 0.001f)
        {
            direccion = -objetoSala.transform.forward;
            direccion.y = 0f;
        }

        if (direccion.sqrMagnitude < 0.001f)
            direccion = Vector3.back;

        direccion.Normalize();

        float radioHorizontal = Mathf.Max(bounds.extents.x, bounds.extents.z);
        float distancia = Mathf.Clamp(
            radioHorizontal + Mathf.Max(0f, distanciaJumpingPointDesdeBorde),
            Mathf.Max(0.1f, distanciaJumpingPointMinima),
            Mathf.Max(distanciaJumpingPointMinima, distanciaJumpingPointMaxima));

        Vector3 posicion = centro + direccion * distancia;
        posicion.y = jumpingPoint.position.y;
        if (hayBoundsSala)
            posicion = AjustarPuntoDentroSalaXZ(posicion, boundsSala, margenTeleportDentroSala);

        jumpingPoint.position = posicion;

        Vector3 mirada = centro - jumpingPoint.position;
        mirada.y = 0f;
        if (mirada.sqrMagnitude > 0.001f)
            jumpingPoint.rotation = Quaternion.LookRotation(mirada.normalized, Vector3.up);

        Debug.Log($"[VR] {idTablet}: jumping point ajustado cerca del elemento. pos={jumpingPoint.position} distancia={distancia:F2}");
    }

    Vector3 AjustarPuntoDentroSalaXZ(Vector3 posicion, Bounds boundsSala, float margen)
    {
        float margenSeguro = Mathf.Max(0f, margen);
        float minX = boundsSala.min.x + margenSeguro;
        float maxX = boundsSala.max.x - margenSeguro;
        float minZ = boundsSala.min.z + margenSeguro;
        float maxZ = boundsSala.max.z - margenSeguro;

        if (minX < maxX)
            posicion.x = Mathf.Clamp(posicion.x, minX, maxX);

        if (minZ < maxZ)
            posicion.z = Mathf.Clamp(posicion.z, minZ, maxZ);

        return posicion;
    }

    string CrearEntradaSync(string idTablet, Transform transformReal)
    {
        Vector3 posicionReal = transformReal.position;

        string px = posicionReal.x.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
        string py = posicionReal.y.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
        string pz = posicionReal.z.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
        string ry = transformReal.eulerAngles.y.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);

        return $"{idTablet}|{px},{py},{pz}|{ry}";
    }

    void EnviarSeleccionPreparacionATablet(List<string> idsSeleccionados)
    {
        if (connectionServer == null || !connectionServer.connected || idsSeleccionados == null)
            return;

        string paquetePrep = "PREP:";
        foreach (string idTablet in idsSeleccionados)
        {
            if (!objetosGeneradosPorId.TryGetValue(idTablet, out GameObject objetoSala) || objetoSala == null)
                continue;

            paquetePrep += CrearEntradaSync(idTablet, objetoSala.transform) + ";";
        }

        if (paquetePrep.EndsWith(";"))
            paquetePrep = paquetePrep.Substring(0, paquetePrep.Length - 1);

        connectionServer.Send(paquetePrep);
    }

   
}
