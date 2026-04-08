using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct ElementoVR
{
    [Tooltip("El ID que enviará la tablet (Ej: 'Rojo', 'Azul')")]
    public string idElemento;

    [Tooltip("El prefab pequeño (cubo) para el menú de la mano")]
    public GameObject prefabMenuMano;

    [Tooltip("El prefab grande que aparecerá físicamente en la sala")]
    public GameObject prefabHabitacion;
}

// --- NUEVA ESTRUCTURA: Empareja la aparición con el teletransporte ---
[System.Serializable]
public struct PuntoSala
{
    [Tooltip("El punto vacío donde aparecerá el objeto grande")]
    public Transform puntoAparicion;

    [Tooltip("El punto vacío donde se teletransportará el jugador para mirarlo")]
    public Transform puntoTeletransporte;
}

public class SceneManager : MonoBehaviour
{
    public enum FaseApp { MenuInicial, SeleccionandoUsuario, EsperandoConexion, Tutorial, Jugando }
    private FaseApp faseActual = FaseApp.MenuInicial;

    // Memoria para saber si el usuario juega sentado o de pie
    private bool usuarioSentado = false;
    // Memoria para saber si el terapeuta deja usar el menú de manos
    private bool permisoMenuVRTerapeuta = true;
    // Memoria para saber si el terapeuta deja ver las partículas
    private bool permisoParticulasTerapeuta = true;

    [Header("Efectos de Manos")]
    [Tooltip("Arrastra aquí los GameObjects o Sistemas de Partículas de las manos")]
    public GameObject particulasManoIzquierda;
    public GameObject particulasManoDerecha;

    [Header("Modo Espectador")]
    public VRHeadTracker headTracker;

    [Header("Passthrough (AR -> VR)")]
    public OVRPassthroughLayer passthroughLayer;

    [Header("Gestión de Red")]
    public LANDiscovery lanDiscovery;
    public Connection connectionServer;

    [Header("Gestor de Teletransporte")]
    [Tooltip("Arrastra aquí el objeto que tiene tu script TeleportManager")]
    public TeleportManager teleportManager;

    [Header("Menú de Inicio (2 Opciones)")]
    public PalmMenuActivator menuActivator;
    public List<GameObject> initialOptions;
    public float distanciaMenuInicial = 0.35f;
    public float separacionMenuInicial = 45f;

    [Header("Menú de Usuarios (Hasta 6 Opciones)")]
    public List<GameObject> userSelectionOptions;
    [Header("Base de Datos (Nombres)")]
    public List<string> nombresDeUsuarios = new List<string> { "Ana", "Carles", "Joan", "David", "Elena" };
    public float distanciaMenuUsuarios = 0.5f;
    public float separacionMenuUsuarios = 30f;

    [Header("Referencias de la Escena")]
    public GameObject canvasConexion;
    public GameObject salaMultisensorial;
   // public GameObject elements;
   // public GameObject jumpingpoints;

    [Header("Base de Datos de Elementos (Colores)")]
    public List<ElementoVR> baseDatosElementos;

    [Header("Puntos de Aparición y Teletransporte (Sala)")]
    public List<PuntoSala> puntosDeAparicion;

    private List<GameObject> objetosGeneradosEnSala = new List<GameObject>();
    private List<GameObject> opcionesMenuJugando = new List<GameObject>();

    private bool tabletRecienConectada = false;

    // --- NUEVO: Variable para recordar qué objeto de tutorial está activo y poder borrarlo ---
    private GameObject elementoTutorialActivo;


    // Diccionario mágico para recordar en qué coordenada ha caído cada elemento
    private Dictionary<string, Transform> mapaDeTeleports = new Dictionary<string, Transform>();

    // Lista secreta para traducir índices del menú de mano a IDs de la tablet
    private List<string> idsOrdenadosParaTeleport = new List<string>();


    IEnumerator Start()
    {
        if (passthroughLayer != null) passthroughLayer.hidden = false;
        if (salaMultisensorial != null) salaMultisensorial.SetActive(false);

        if (connectionServer != null)
        {
            connectionServer.RegisterOnClientConnectCallback(OnTabletConnected);
        }
        else
        {
            Debug.LogWarning("No has asignado el script Connection al SceneManager.");
        }

        yield return new WaitForSeconds(2.0f);

        if (menuActivator != null && initialOptions != null && initialOptions.Count > 0)
        {
            faseActual = FaseApp.MenuInicial;
            menuActivator.isLockedOpen = true;
            menuActivator.distancia = distanciaMenuInicial;
            if (menuActivator.fanMenu != null) menuActivator.fanMenu.spacingAngle = separacionMenuInicial;

            menuActivator.OpenMenu(initialOptions);
        }

        // Líneas de prueba: simula que la tablet envía datos
      //  List<string> coloresDePrueba = new List<string> { "blau_cel", "groc", "rosa","verd","taronja","blau_fosc" };
      //  RecibirDatosDeLaTablet(coloresDePrueba);
    }

    void OnEnable() { FanOption.OnInitialMenuSelected += AccionDelBoton; if (teleportManager != null) teleportManager.OnUsuarioTeletransportado += AvisarTabletUbicacion; }
    void OnDisable() { FanOption.OnInitialMenuSelected -= AccionDelBoton; if (teleportManager != null) teleportManager.OnUsuarioTeletransportado -= AvisarTabletUbicacion; }

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
        }
        else if (faseActual == FaseApp.SeleccionandoUsuario)
        {
            Debug.Log($"Usuario {index} seleccionado. Iniciando experiencia VR...");
            faseActual = FaseApp.Jugando;
            StartCoroutine(EsperarCierreMenuYIniciarVR());
        }
    }

    void OnTabletConnected()
    {
        Debug.Log("¡Conexión recibida de la tablet! Transicionando a VR...");
        tabletRecienConectada = true;
    }

    IEnumerator EsperarCierreMenuYIniciarVR()
    {
        yield return new WaitForSeconds(1.4f);
        IniciarExperienciaVR();
    }

    IEnumerator TransicionAlMenuUsuarios()
    {
        yield return new WaitForSeconds(0.6f);

        if (menuActivator != null && userSelectionOptions != null && userSelectionOptions.Count > 0)
        {
            menuActivator.isLockedOpen = true;
            menuActivator.distancia = distanciaMenuUsuarios;
            if (menuActivator.fanMenu != null) menuActivator.fanMenu.spacingAngle = separacionMenuUsuarios;

            menuActivator.OpenMenu(userSelectionOptions);

            if (menuActivator.fanMenu != null)
            {
                int i = 0;
                foreach (Transform botonClonado in menuActivator.fanMenu.transform)
                {
                    if (i < nombresDeUsuarios.Count)
                    {
                        TMPro.TMP_Text texto = botonClonado.GetComponentInChildren<TMPro.TMP_Text>();
                        if (texto != null) texto.text = nombresDeUsuarios[i];
                    }
                    i++;
                }
            }
        }
    }

    // ==========================================
    // FASE VR COMPLETA (CON FUNDIDO)
    // ==========================================

    void IniciarExperienciaVR()
    {
        faseActual = FaseApp.Jugando;
        // Lanzamos la corrutina que hace el efecto visual
        StartCoroutine(RutinaTransicionVR());
    }

    IEnumerator RutinaTransicionVR()
    {
        // 1. Pantalla a negro lentamente (1.5 segundos)
        if (teleportManager != null)
            yield return StartCoroutine(teleportManager.FadeToBlack(1.5f));

        // 2. En la oscuridad, damos el cambiazo (Apagamos realidad, encendemos VR)
        if (passthroughLayer != null) passthroughLayer.hidden = true;
        if (salaMultisensorial != null) salaMultisensorial.SetActive(true);

        if (menuActivator != null)
        {
            menuActivator.isLockedOpen = false;
            // AHORA DEPENDE DE LO QUE HAYA DICHO LA TABLET:
            menuActivator.canOpenWithPalms = permisoMenuVRTerapeuta;
            menuActivator.distancia = distanciaMenuUsuarios;
            if (menuActivator.fanMenu != null) menuActivator.fanMenu.spacingAngle = separacionMenuUsuarios;
        }

        // 3. Volvemos a la luz lentamente (1.5 segundos) revelando el mundo virtual
        if (teleportManager != null)
            yield return StartCoroutine(teleportManager.FadeToClear(1.5f));

        Debug.Log("¡Transición a VR completada con elegancia!");
    }

    // ==========================================
    // EMERGENCIA SOS (CON FUNDIDO RÁPIDO)
    // ==========================================

    public void ActivarModoSOS()
    {
        // Lanzamos la corrutina de emergencia
        StartCoroutine(RutinaSOS());
    }

    IEnumerator RutinaSOS()
    {
        Debug.LogWarning("¡SEÑAL SOS RECIBIDA! Iniciando fundido de emergencia.");

        // 1. Pantalla a negro rápida (0.4 segundos). Cortamos estímulos casi al instante.
        if (teleportManager != null)
            yield return StartCoroutine(teleportManager.FadeToBlack(0.4f));

        // 2. En la oscuridad, apagamos la sala virtual y activamos las cámaras reales
        if (salaMultisensorial != null) salaMultisensorial.SetActive(false);
        if (passthroughLayer != null) passthroughLayer.hidden = false;

        // Bloqueamos el menú por seguridad
        if (menuActivator != null) menuActivator.canOpenWithPalms = false;

        // 3. Volvemos a la luz del mundo real a una velocidad suave (1.0 segundos)
        if (teleportManager != null)
            yield return StartCoroutine(teleportManager.FadeToClear(1.0f));

        Debug.Log("Rescate completado. Usuario en Passthrough.");
    }

    void Update()
    {
        // 1. Lógica de cuando la tablet se conecta por primera vez
        if (tabletRecienConectada && faseActual == FaseApp.EsperandoConexion)
        {
            tabletRecienConectada = false;

            if (canvasConexion != null) canvasConexion.SetActive(false);
            if (lanDiscovery != null) lanDiscovery.StopBroadcast();

            // Al conectar, entramos en modo TUTORIAL (Passthrough)
            ProcesarCambioAPassthrough(FaseApp.Tutorial);
        }

        // 2. NUEVO: LÓGICA PARA ESCUCHAR A LA TABLET CONSTANTEMENTE
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
                    // Delegamos la decisión a nuestra nueva función inteligente
                    ProcesarCambioAPassthrough(FaseApp.Tutorial);
                    continue;
                }
                else if (mensajeRecibido == "CMD_PREPARACION")
                {
                    // Delegamos la decisión (usamos FaseApp.Jugando pero con la sala apagada)
                    ProcesarCambioAPassthrough(FaseApp.Jugando);
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
                    string colores = mensajeRecibido.Split(':')[1];
                    List<string> listaParaElEscenario = new List<string>(colores.Split(','));

                    RecibirDatosDeLaTablet(listaParaElEscenario);
                    IniciarExperienciaVR();
                }
                else if (mensajeRecibido.StartsWith("TELEPORT:"))
                {
                    string idColor = mensajeRecibido.Split(':')[1];

                    if (mapaDeTeleports.ContainsKey(idColor))
                    {
                        Transform destino = mapaDeTeleports[idColor];
                        if (teleportManager != null)
                        {
                            teleportManager.ForzarTeletransporte(destino);
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
                    Debug.Log("Modo Sentado: Restringiendo aparición a 180º frontales.");
                    continue;
                }
                else if (mensajeRecibido == "POSTURA:DE_PIE")
                {
                    usuarioSentado = false;
                    Debug.Log("Modo De Pie: Aparición 360º libre.");
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
                    // Encendemos los objetos de partículas en vivo
                    if (particulasManoIzquierda != null) particulasManoIzquierda.SetActive(true);
                    if (particulasManoDerecha != null) particulasManoDerecha.SetActive(true);
                    continue;
                }
                else if (mensajeRecibido == "CMD_PARTICULAS:OFF")
                {
                    permisoParticulasTerapeuta = false;
                    // Apagamos los objetos de partículas en vivo
                    if (particulasManoIzquierda != null) particulasManoIzquierda.SetActive(false);
                    if (particulasManoDerecha != null) particulasManoDerecha.SetActive(false);
                    continue;
                }
            }
        }
    }
  
    // --- NUEVO: Función que decide si hace falta fundido o no ---
    private void ProcesarCambioAPassthrough(FaseApp nuevaFase)
    {
        if (faseActual == FaseApp.Jugando && salaMultisensorial != null && salaMultisensorial.activeSelf)
        {
            // CASO A: Venimos del mundo VR (inmersión total). SÍ hacemos el fundido lento a negro.
            StartCoroutine(RutinaSalidaLentaDeVR(nuevaFase));
        }
        else
        {
            // CASO B: Ya estábamos en Passthrough (ej: pasando de Tutorial a Preparación).
            // NO hay fundido a negro de pantalla. Solo borramos el objeto 3D del tutorial al instante.
            faseActual = nuevaFase;

            if (elementoTutorialActivo != null) Destroy(elementoTutorialActivo);

            // Nos aseguramos de que el entorno VR está apagado y las cámaras encendidas
            if (salaMultisensorial != null) salaMultisensorial.SetActive(false);
            if (passthroughLayer != null) passthroughLayer.hidden = false;
            if (menuActivator != null) menuActivator.canOpenWithPalms = false;
        }
    }

    // --- NUEVO: Rutina de salida MUCHO más lenta y agradable ---
    IEnumerator RutinaSalidaLentaDeVR(FaseApp nuevaFase)
    {
        // 1. Pantalla a negro lentamente (1.0 segundos para no asustar)
        if (teleportManager != null)
            yield return StartCoroutine(teleportManager.FadeToBlack(1.0f));

        // 2. EN LA OSCURIDAD: Hacemos el cambiazo
        faseActual = nuevaFase;
        if (elementoTutorialActivo != null) Destroy(elementoTutorialActivo);

        if (salaMultisensorial != null) salaMultisensorial.SetActive(false);
        if (passthroughLayer != null) passthroughLayer.hidden = false;
        if (menuActivator != null) menuActivator.canOpenWithPalms = false;

        // 3. Volvemos a la luz del mundo real a la misma velocidad cómoda que te gustó (1.5 segundos)
        if (teleportManager != null)
            yield return StartCoroutine(teleportManager.FadeToClear(1.5f));

        Debug.Log($"Salida suave de VR completada. Passthrough nítido.");
    }

    // ==========================================
    // NUEVO: FASE TUTORIAL
    // ==========================================
    void MostrarElementoEnTutorial(string idTablet)
    {
        // 1. Borramos el elemento que estuviéramos enseñando antes
        if (elementoTutorialActivo != null)
        {
            Destroy(elementoTutorialActivo);
        }

        // 2. Buscamos el elemento en la base de datos
        ElementoVR elemento = baseDatosElementos.Find(e => e.idElemento == idTablet);

        if (elemento.prefabHabitacion != null)
        {
            // 3. Calculamos una posición cómoda: 1 metro directamente delante de la cara del usuario, a la altura de su pecho
            Transform cam = Camera.main.transform;
            Vector3 direccionPlana = cam.forward;
            direccionPlana.y = 0;
            direccionPlana.Normalize();

            Vector3 posicionAparicion = cam.position + (direccionPlana * 1.0f);
            posicionAparicion.y = cam.position.y - 0.2f; // Un poco por debajo de los ojos

            // 4. Instanciamos el objeto en el mundo real (Passthrough)
            elementoTutorialActivo = Instantiate(elemento.prefabHabitacion, posicionAparicion, Quaternion.LookRotation(direccionPlana));

            Debug.Log($"Mostrando {idTablet} en modo Tutorial.");

            // --- NUEVO: Chivatazo a la tablet (Modo Tutorial) ---
            if (connectionServer != null && connectionServer.connected)
            {
                string px = posicionAparicion.x.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
                string py = posicionAparicion.y.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
                string pz = posicionAparicion.z.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
                string ry = elementoTutorialActivo.transform.eulerAngles.y.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);

                connectionServer.Send($"SYNC_TUT:{idTablet}|{px},{py},{pz}|{ry}");
            }
        }

    }

  

    // ==========================================
    // FASE VR COMPLETA
    // ==========================================
    public void RecibirDatosDeLaTablet(List<string> elementosSeleccionadosTablet)
    {
        string paqueteSync = "SYNC:"; // Empezamos a preparar el paquete

        mapaDeTeleports.Clear();
        idsOrdenadosParaTeleport.Clear();
        // Si había un elemento del tutorial flotando por ahí, lo destruimos
        if (elementoTutorialActivo != null) Destroy(elementoTutorialActivo);

        foreach (GameObject obj in objetosGeneradosEnSala) { Destroy(obj); }
        objetosGeneradosEnSala.Clear();
        opcionesMenuJugando.Clear();

        List<Transform> destinosTeleportActivos = new List<Transform>();
        // List<PuntoSala> posicionesDisponibles = new List<PuntoSala>(puntosDeAparicion);

        // --- NUEVA LÓGICA DE APARICIÓN (Sentado vs De Pie) ---
        List<PuntoSala> posicionesDisponibles = new List<PuntoSala>();
        List<PuntoSala> puntosTraserosReserva = new List<PuntoSala>();

        if (usuarioSentado)
        {
            // SOLUCIÓN: Usamos el FRENTE ABSOLUTO de la sala virtual en lugar de la cabeza.
            // Así no importa si el usuario está mirando al suelo o girado al iniciar.
            Vector3 frenteHabitacion = salaMultisensorial != null ? salaMultisensorial.transform.forward : Vector3.forward;
            frenteHabitacion.y = 0;
            frenteHabitacion.Normalize();

            // Usamos el centro exacto de la sala como punto de referencia
            Vector3 centroHabitacion = salaMultisensorial != null ? salaMultisensorial.transform.position : Vector3.zero;
            centroHabitacion.y = 0;

            foreach (var punto in puntosDeAparicion)
            {
                Vector3 posicionPunto = punto.puntoAparicion.position;
                posicionPunto.y = 0;

                Vector3 direccionAlPunto = (posicionPunto - centroHabitacion).normalized;

                if (Vector3.Angle(frenteHabitacion, direccionAlPunto) <= 90f)
                {
                    posicionesDisponibles.Add(punto);
                }
                else
                {
                    puntosTraserosReserva.Add(punto);
                }
            }

            // --- CHIVATO VITAL PARA TI ---
            Debug.Log($"[SPAWN SENTADO] Puntos Frontales encontrados: {posicionesDisponibles.Count} | Puntos Traseros: {puntosTraserosReserva.Count}");
        }
        else
        {
            posicionesDisponibles.AddRange(puntosDeAparicion);
        }


        foreach (string idTablet in elementosSeleccionadosTablet)
        {
            ElementoVR elemento = baseDatosElementos.Find(e => e.idElemento == idTablet);

            // Si nos hemos quedado sin puntos frontales, tiramos de los traseros de emergencia
            if (posicionesDisponibles.Count == 0 && usuarioSentado && puntosTraserosReserva.Count > 0)
            {
                posicionesDisponibles.AddRange(puntosTraserosReserva);
                puntosTraserosReserva.Clear();
                Debug.LogWarning("Se agotaron los puntos frontales. Usando puntos traseros por seguridad.");
            }

            if (elemento.idElemento == idTablet && posicionesDisponibles.Count > 0)
            {
                int indiceAleatorio = Random.Range(0, posicionesDisponibles.Count);
                PuntoSala puntoElegido = posicionesDisponibles[indiceAleatorio];
                posicionesDisponibles.RemoveAt(indiceAleatorio);

                Transform parentTransform = salaMultisensorial != null ? salaMultisensorial.transform : null;
                GameObject nuevoObjetoSala = Instantiate(elemento.prefabHabitacion, puntoElegido.puntoAparicion.position, puntoElegido.puntoAparicion.rotation, parentTransform);

                objetosGeneradosEnSala.Add(nuevoObjetoSala);

                // --- NUEVO: Añadimos este objeto al paquete de datos ---
                string px = puntoElegido.puntoAparicion.position.x.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
                string py = puntoElegido.puntoAparicion.position.y.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
                string pz = puntoElegido.puntoAparicion.position.z.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
                string ry = puntoElegido.puntoAparicion.eulerAngles.y.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);

                paqueteSync += $"{idTablet}|{px},{py},{pz}|{ry};";


                opcionesMenuJugando.Add(elemento.prefabMenuMano);
                destinosTeleportActivos.Add(puntoElegido.puntoTeletransporte);
                //  Guardamos en el diccionario el ID y su coordenada
                mapaDeTeleports[idTablet] = puntoElegido.puntoTeletransporte;
                idsOrdenadosParaTeleport.Add(idTablet);
            }
        }

        // --- NUEVO: Enviamos el mapa completo a la tablet ---
        if (paqueteSync.EndsWith(";")) paqueteSync = paqueteSync.Substring(0, paqueteSync.Length - 1); // Quitamos el último punto y coma
        if (connectionServer != null && connectionServer.connected) connectionServer.Send(paqueteSync);

        if (menuActivator != null) menuActivator.opcionesDePartida = opcionesMenuJugando;
        if (teleportManager != null) teleportManager.teleportDestinations = destinosTeleportActivos.ToArray();
    }

   
}