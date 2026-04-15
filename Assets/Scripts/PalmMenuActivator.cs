/*using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Management;
using UnityEngine.InputSystem;

public class PalmMenuActivator : MonoBehaviour
{
    [Header("Menu")]
    public FanMenu fanMenu;
    public Transform head;

    [Header("Hand Transforms")]
    public Transform leftHand;
    public Transform rightHand;
    public float distancia;

    private bool menuOpened = false;
    private bool menuSuspended = false;

    // --- NUEVO: Temporizador seguro contra teletransportes ---
    private float suspendTimer = 0f;

    public float menuAutoCloseTime = 10f;
    private float idleTimer = 0f;

    [Header("Palm Facing")]
    [Range(0.6f, 0.95f)] public float activateDot = 0.82f;
    [Range(0.6f, 0.95f)] public float deactivateDot = 0.72f;

    [Header("Hand Open Detection")]
    public float minFingerSpread = 0.035f;
    public float minFingerExtension = 0.055f;

    [Header("Smoothing")]
    [Range(0f, 1f)] public float smoothing = 0.35f;

    [Header("Both Palms Hold")]
    public float bothPalmsHoldTime = 0.5f;

    XRHandSubsystem handSubsystem;
    Vector3 leftPalmSmooth;
    Vector3 rightPalmSmooth;
    bool leftInit;
    bool rightInit;
    bool leftActive;
    bool rightActive;
    float bothTimer;

    void OnEnable()
    {
        FanOption.OnOptionSelected += HandleMenuTeleport;
    }

    void OnDisable()
    {
        FanOption.OnOptionSelected -= HandleMenuTeleport;
    }

    void HandleMenuTeleport(int index)
    {
        if (menuSuspended) return;

        menuSuspended = true;
        suspendTimer = 0f; // Reiniciamos el reloj de bloqueo blindado

        // CIERRE RÁPIDO (0.5s) al viajar
        CloseMenu(0.5f);
    }

    void Start()
    {
        TryGetSubsystem();
    }

    void TryGetSubsystem()
    {
        var loader = XRGeneralSettings.Instance?.Manager?.activeLoader;
        if (loader != null) handSubsystem = loader.GetLoadedSubsystem<XRHandSubsystem>();
    }

    void Update()
    {
        HandleKeyboardTesting();

        // --- EL SEGURO ANTI-TELETRANSPORTES ---
        // Contamos el tiempo aquí. Aunque el CameraRig parpadee o se apague 
        // durante el viaje, este reloj sobrevivirá y desbloqueará el menú.
        if (menuSuspended)
        {
            suspendTimer += Time.deltaTime;
            if (suspendTimer >= 5.0f)
            {
                leftInit = false;
                rightInit = false;
                menuSuspended = false;
                Debug.Log("✅ Menú desbloqueado de forma segura tras el viaje.");
            }
        }

        if (menuOpened)
        {
            if (IsUserInteractingWithMenu()) idleTimer = 0f;
            else idleTimer += Time.deltaTime;

            if (idleTimer >= menuAutoCloseTime)
            {
                CloseMenu(3.0f);
            }
        }

        if (handSubsystem == null || !handSubsystem.running)
        {
            TryGetSubsystem();
            return;
        }

        leftActive = UpdateHand(handSubsystem.leftHand, ref leftPalmSmooth, ref leftInit, leftActive);
        rightActive = UpdateHand(handSubsystem.rightHand, ref rightPalmSmooth, ref rightInit, rightActive);

        if (leftActive && rightActive) bothTimer += Time.deltaTime;
        else bothTimer = 0f;

        bool bothPalmsDetected = bothTimer >= bothPalmsHoldTime;

        if (bothPalmsDetected && !menuOpened && !menuSuspended)
        {
            OpenMenu();
        }
    }

    void OpenMenu(List<GameObject> specificOptions = null)
    {
        if (!fanMenu || !head || !leftHand || !rightHand) return;

        fanMenu.PlaceInFrontOfUser(head, leftHand, rightHand, distancia, 0.05f);
        fanMenu.Build(specificOptions);

        menuOpened = true;
        idleTimer = 0f;
        bothTimer = 0f;
    }

    void CloseMenu(float fadeDuration = 1.2f)
    {
        if (!menuOpened || !fanMenu) return;

        menuOpened = false;
        idleTimer = 0f;
        bothTimer = 0f;

        fanMenu.CloseMenuAnimated(fadeDuration, null);
    }

    bool IsUserInteractingWithMenu()
    {
        foreach (Transform option in fanMenu.transform)
        {
            var fanOpt = option.GetComponent<FanOption>();
            if (fanOpt != null)
            {
                if (fanOpt.press > 0.05f || Mathf.Abs(fanOpt.hover) > 0.05f)
                {
                    return true;
                }
            }
        }
        return false;
    }

    bool UpdateHand(XRHand hand, ref Vector3 smoothNormal, ref bool init, bool currentState)
    {
        if (!hand.isTracked) return false;
        if (!TryGetPalmFacing(hand, ref smoothNormal, ref init, currentState)) return false;
        if (!IsHandOpen(hand)) return false;
        return true;
    }

    bool TryGetPalmFacing(XRHand hand, ref Vector3 smoothNormal, ref bool init, bool currentState)
    {
        var palm = hand.GetJoint(XRHandJointID.Palm);
        if (!palm.TryGetPose(out Pose pose)) return false;

        Vector3 palmNormal = -pose.up;

        if (!init)
        {
            smoothNormal = palmNormal;
            init = true;
        }
        else
        {
            smoothNormal = Vector3.Lerp(smoothNormal, palmNormal, smoothing);
        }

        // --- LA SOLUCIÓN A TU INSTINTO ---
        // En lugar de usar Camera.main.transform.position, usamos 'head.position'
        // Esto garantiza que siempre apunte al CenterEyeAnchor sin importar a dónde viajes.
        Vector3 toCam = (head.position - pose.position).normalized;
        float dot = Vector3.Dot(smoothNormal, toCam);

        if (currentState) return dot > deactivateDot;
        else return dot > activateDot;
    }

    bool IsHandOpen(XRHand hand)
    {
        var palm = hand.GetJoint(XRHandJointID.Palm);
        var indexTip = hand.GetJoint(XRHandJointID.IndexTip);
        var middleTip = hand.GetJoint(XRHandJointID.MiddleTip);
        var ringTip = hand.GetJoint(XRHandJointID.RingTip);
        var littleTip = hand.GetJoint(XRHandJointID.LittleTip);

        if (!palm.TryGetPose(out Pose palmPose) ||
            !indexTip.TryGetPose(out Pose iPose) ||
            !middleTip.TryGetPose(out Pose mPose) ||
            !ringTip.TryGetPose(out Pose rPose) ||
            !littleTip.TryGetPose(out Pose lPose))
            return false;

        float iDist = Vector3.Distance(palmPose.position, iPose.position);
        float mDist = Vector3.Distance(palmPose.position, mPose.position);
        float rDist = Vector3.Distance(palmPose.position, rPose.position);
        float lDist = Vector3.Distance(palmPose.position, lPose.position);

        if (iDist < minFingerExtension || mDist < minFingerExtension || rDist < minFingerExtension || lDist < minFingerExtension)
            return false;

        float spread1 = Vector3.Distance(iPose.position, lPose.position);
        float spread2 = Vector3.Distance(iPose.position, rPose.position);

        if (spread1 < minFingerSpread || spread2 < minFingerSpread) return false;

        return true;
    }

    void HandleKeyboardTesting()
    {
        if (Keyboard.current == null || menuSuspended) return;

        if (Keyboard.current.mKey.wasPressedThisFrame)
        {
            if (menuOpened) CloseMenu();
            else OpenMenu();
        }

        if (fanMenu.selection == null || fanMenu.selection.selectedOptions.Count == 0) return;

        var pool = fanMenu.selection.selectedOptions;

        if (Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            if (menuOpened) CloseMenu();
            List<GameObject> custom = new List<GameObject>();
            custom.Add(pool[0 % pool.Count]);
            OpenMenu(custom);
        }

        if (Keyboard.current.digit2Key.wasPressedThisFrame)
        {
            if (menuOpened) CloseMenu();
            List<GameObject> custom = new List<GameObject>();
            custom.Add(pool[0 % pool.Count]);
            custom.Add(pool[1 % pool.Count]);
            OpenMenu(custom);
        }

        if (Keyboard.current.digit3Key.wasPressedThisFrame)
        {
            if (menuOpened) CloseMenu();
            List<GameObject> custom = new List<GameObject>();
            custom.Add(pool[0 % pool.Count]);
            custom.Add(pool[1 % pool.Count]);
            custom.Add(pool[2 % pool.Count]);
            OpenMenu(custom);
        }

        if (Keyboard.current.digit4Key.wasPressedThisFrame)
        {
            if (menuOpened) CloseMenu();
            List<GameObject> custom = new List<GameObject>();
            custom.Add(pool[0 % pool.Count]);
            custom.Add(pool[1 % pool.Count]);
            custom.Add(pool[2 % pool.Count]);
            custom.Add(pool[3 % pool.Count]);
            OpenMenu(custom);
        }

        if (Keyboard.current.digit5Key.wasPressedThisFrame)
        {
            if (menuOpened) CloseMenu();
            List<GameObject> custom = new List<GameObject>();
            custom.Add(pool[0 % pool.Count]);
            custom.Add(pool[1 % pool.Count]);
            custom.Add(pool[2 % pool.Count]);
            custom.Add(pool[3 % pool.Count]);
            custom.Add(pool[4 % pool.Count]);
            OpenMenu(custom);
        }

        if (Keyboard.current.digit6Key.wasPressedThisFrame)
        {
            if (menuOpened) CloseMenu();
            List<GameObject> custom = new List<GameObject>();
            custom.Add(pool[0 % pool.Count]);
            custom.Add(pool[1 % pool.Count]);
            custom.Add(pool[2 % pool.Count]);
            custom.Add(pool[3 % pool.Count]);
            custom.Add(pool[4 % pool.Count]);
            custom.Add(pool[5 % pool.Count]);
            OpenMenu(custom);
        }
    }
}*/

//el codi de sobre no acaba de funcionar. La rotació està afectant. El de sota, hauria de ser igual de bo

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Management;
using UnityEngine.InputSystem;

public class PalmMenuActivator : MonoBehaviour
{
    [Header("Menu")]
    public FanMenu fanMenu;
    public Transform head;

    [Header("Hand Transforms")]
    public Transform leftHand;
    public Transform rightHand;
    public float distancia;

    private bool menuOpened = false;
    private bool menuSuspended = false;

    // Si es true, ignora el temporizador y nunca se cierra solo.
    [HideInInspector] public bool isLockedOpen = false;

    [Header("Permisos del Menú")]
    [Tooltip("Si está desmarcado, mirar las palmas NO abrirá el menú. Ideal para el inicio de la app.")]
    public bool canOpenWithPalms = false;

    [Header("Menú en Partida (VR)")]
    [Tooltip("Lista dinámica que rellena el SceneManager con los datos de la tablet.")]
    public List<GameObject> opcionesDePartida = new List<GameObject>();

    public float menuAutoCloseTime = 10f;
    private float idleTimer = 0f;

    [Header("Palm Facing")]
    [Range(0.6f, 0.95f)] public float activateDot = 0.82f;
    [Range(0.6f, 0.95f)] public float deactivateDot = 0.72f;

    [Header("Hand Open Detection")]
    public float minFingerSpread = 0.035f;
    public float minFingerExtension = 0.055f;

    [Header("Smoothing")]
    [Range(0f, 1f)] public float smoothing = 0.35f;

    [Header("Both Palms Hold")]
    public float bothPalmsHoldTime = 0.5f;

    XRHandSubsystem handSubsystem;
    Vector3 leftPalmSmooth;
    Vector3 rightPalmSmooth;
    bool leftInit;
    bool rightInit;
    bool leftActive;
    bool rightActive;
    float bothTimer;

    void OnEnable()
    {
      //  FanOption.OnOptionSelected += HandleMenuTeleport;
        // Ahora solo escucha la orden general de cerrar
        FanOption.OnMenuInteractionConfirmed += HandleMenuCloseRequest;
    }

    void OnDisable()
    {
      //  FanOption.OnOptionSelected -= HandleMenuTeleport;
        FanOption.OnMenuInteractionConfirmed -= HandleMenuCloseRequest;
    }
    void HandleMenuCloseRequest()
    {
        if (menuSuspended) return;
        menuSuspended = true;
        isLockedOpen = false; // Quita el candado del SceneManager

        CloseMenu(0.5f);
        StartCoroutine(UnlockMenuRoutine());
    }

    void HandleMenuTeleport(int index)
    {
        if (menuSuspended) return; // Seguridad extra
        menuSuspended = true;


        // --- QUITAMOS EL CANDADO ---
        // Al seleccionar una opción, el menú inicial cumple su función.
        // Lo desbloqueamos para que las próximas veces que se abra funcione normal.
        isLockedOpen = false;
       
        // CIERRE RÁPIDO (0.5s): El usuario viaja, queremos que desaparezca rápido
        CloseMenu(0.5f);

        // En lugar de Invoke, usamos una Corrutina a prueba de balas
        StartCoroutine(UnlockMenuRoutine());
    }

    System.Collections.IEnumerator UnlockMenuRoutine()
    {
        // Le damos 5 segundos completos (suficiente para que termine tu viaje a negro y la animación)
        yield return new WaitForSeconds(5.0f);

        // BORRAMOS la memoria de las palmas de la habitación anterior
        leftInit = false;
        rightInit = false;

        // Desbloqueamos el menú
        menuSuspended = false;

      //  Debug.Log("✅ Menú desbloqueado. Ya puedes volver a levantar las palmas.");
    }


    void Start()
    {
        TryGetSubsystem();
    }

    void TryGetSubsystem()
    {
        var loader = XRGeneralSettings.Instance?.Manager?.activeLoader;
        if (loader != null) handSubsystem = loader.GetLoadedSubsystem<XRHandSubsystem>();
    }

    void Update()
    {
        HandleKeyboardTesting();
        // Si el menú está abierto y NO está bloqueado, calculamos el auto-cierre
        if (menuOpened && !isLockedOpen)
        {
            if (IsUserInteractingWithMenu()) idleTimer = 0f;
            else idleTimer += Time.deltaTime;

            if (idleTimer >= menuAutoCloseTime)
            {
                // CIERRE LENTO (3.0s): El usuario no hace nada, se disuelve con calma
                CloseMenu(3.0f); // Si pasan 10s, también hace la animación Fade Out
            }
        }

        if (handSubsystem == null || !handSubsystem.running)
        {
            TryGetSubsystem();
            return;
        }

        leftActive = UpdateHand(handSubsystem.leftHand, ref leftPalmSmooth, ref leftInit, leftActive);
        rightActive = UpdateHand(handSubsystem.rightHand, ref rightPalmSmooth, ref rightInit, rightActive);

        if (leftActive && rightActive) bothTimer += Time.deltaTime;
        else bothTimer = 0f;

        bool bothPalmsDetected = bothTimer >= bothPalmsHoldTime;

        // --- LA MAGIA: Añadimos 'canOpenWithPalms' a las condiciones ---
        // Si el permiso es falso, esta línea nunca se ejecuta.
        if (bothPalmsDetected && !menuOpened && !menuSuspended && canOpenWithPalms)
        {
            // Al abrir el menú por defecto, carga la lista de opciones (botones) estándar
            OpenMenu(opcionesDePartida);
        }
    }
    public void OpenMenu(List<GameObject> specificOptions = null, bool forceMenuButtons = false)
    {
        if (!fanMenu || !head || !leftHand || !rightHand) return;

        fanMenu.PlaceInFrontOfUser(head, leftHand, rightHand, distancia, 0.05f);
        fanMenu.Build(specificOptions);

        if (forceMenuButtons)
        {
            foreach (Transform option in fanMenu.transform)
            {
                var fanOption = option.GetComponent<FanOption>();
                if (fanOption != null)
                {
                    fanOption.isTeleportButton = false;
                }
            }
        }

        menuOpened = true;
        idleTimer = 0f;
        bothTimer = 0f;
    }

    // recibe los segundos de duración que queramos (por defecto 1.2s para el teclado)
    void CloseMenu(float fadeDuration = 1.2f)
    {
        if (!menuOpened || !fanMenu) return;

        menuOpened = false;
        idleTimer = 0f;
        bothTimer = 0f;

        fanMenu.CloseMenuAnimated(fadeDuration, null);
    }

    public void CloseMenuPublic(float fadeDuration = 0.5f)
    {
        CloseMenu(fadeDuration);
    }

    bool IsUserInteractingWithMenu()
    {
        foreach (Transform option in fanMenu.transform)
        {
            var fanOpt = option.GetComponent<FanOption>();
            if (fanOpt != null)
            {
                if (fanOpt.press > 0.05f || Mathf.Abs(fanOpt.hover) > 0.05f)
                {
                    return true;
                }
            }
        }
        return false;
    }

    bool UpdateHand(XRHand hand, ref Vector3 smoothNormal, ref bool init, bool currentState)
    {
        if (!hand.isTracked) return false;
        if (!TryGetPalmFacing(hand, ref smoothNormal, ref init, currentState)) return false;
        if (!IsHandOpen(hand)) return false;
        return true;
    }

    bool TryGetPalmFacing(XRHand hand, ref Vector3 smoothNormal, ref bool init, bool currentState)
    {
        var palm = hand.GetJoint(XRHandJointID.Palm);
        if (!palm.TryGetPose(out Pose pose)) return false;

        // --- LA MAGIA QUE ARREGLA EL TELETRANSPORTE ---
        // 'pose' viene en coordenadas locales de tu habitación física.
        // Usamos el 'parent' de la cabeza (TrackingSpace) para convertir 
        // esas coordenadas físicas a las nuevas coordenadas del mundo virtual tras viajar.
        Transform trackingSpace = head.parent != null ? head.parent : head;

        Vector3 worldPalmPos = trackingSpace.TransformPoint(pose.position);
        Vector3 worldPalmNormal = trackingSpace.TransformDirection(-pose.up);

        if (!init)
        {
            smoothNormal = worldPalmNormal;
            init = true;
        }
        else
        {
            smoothNormal = Vector3.Lerp(smoothNormal, worldPalmNormal, smoothing);
        }

        // Ahora comparamos peras con peras (World Space con World Space)
        Vector3 toCam = (head.position - worldPalmPos).normalized;
        float dot = Vector3.Dot(smoothNormal, toCam);

        if (currentState) return dot > deactivateDot;
        else return dot > activateDot;
    }

    bool IsHandOpen(XRHand hand)
    {
        var palm = hand.GetJoint(XRHandJointID.Palm);
        var indexTip = hand.GetJoint(XRHandJointID.IndexTip);
        var middleTip = hand.GetJoint(XRHandJointID.MiddleTip);
        var ringTip = hand.GetJoint(XRHandJointID.RingTip);
        var littleTip = hand.GetJoint(XRHandJointID.LittleTip);

        if (!palm.TryGetPose(out Pose palmPose) ||
            !indexTip.TryGetPose(out Pose iPose) ||
            !middleTip.TryGetPose(out Pose mPose) ||
            !ringTip.TryGetPose(out Pose rPose) ||
            !littleTip.TryGetPose(out Pose lPose))
            return false;

        float iDist = Vector3.Distance(palmPose.position, iPose.position);
        float mDist = Vector3.Distance(palmPose.position, mPose.position);
        float rDist = Vector3.Distance(palmPose.position, rPose.position);
        float lDist = Vector3.Distance(palmPose.position, lPose.position);

        if (iDist < minFingerExtension || mDist < minFingerExtension || rDist < minFingerExtension || lDist < minFingerExtension)
            return false;

        float spread1 = Vector3.Distance(iPose.position, lPose.position);
        float spread2 = Vector3.Distance(iPose.position, rPose.position);

        if (spread1 < minFingerSpread || spread2 < minFingerSpread) return false;

        return true;
    }

    void HandleKeyboardTesting()
    {
        // NUEVO: Si no hay teclado o el menú está bloqueado por el teletransporte, ignoramos las teclas
        if (Keyboard.current == null || menuSuspended) return;

        if (Keyboard.current.mKey.wasPressedThisFrame)
        {
            if (menuOpened) CloseMenu();
            else OpenMenu();
        }

        if (fanMenu.selection == null || fanMenu.selection.selectedOptions.Count == 0) return;

        var pool = fanMenu.selection.selectedOptions;

        if (Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            if (menuOpened) CloseMenu();
            List<GameObject> custom = new List<GameObject>();
            custom.Add(pool[0 % pool.Count]);
            OpenMenu(custom);
        }

        if (Keyboard.current.digit2Key.wasPressedThisFrame)
        {
            if (menuOpened) CloseMenu();
            List<GameObject> custom = new List<GameObject>();
            custom.Add(pool[0 % pool.Count]);
            custom.Add(pool[1 % pool.Count]);
            OpenMenu(custom);
        }

        if (Keyboard.current.digit3Key.wasPressedThisFrame)
        {
            if (menuOpened) CloseMenu();
            List<GameObject> custom = new List<GameObject>();
            custom.Add(pool[0 % pool.Count]);
            custom.Add(pool[1 % pool.Count]);
            custom.Add(pool[2 % pool.Count]);
            OpenMenu(custom);
        }

        if (Keyboard.current.digit4Key.wasPressedThisFrame)
        {
            if (menuOpened) CloseMenu();
            List<GameObject> custom = new List<GameObject>();
            custom.Add(pool[0 % pool.Count]);
            custom.Add(pool[1 % pool.Count]);
            custom.Add(pool[2 % pool.Count]);
            custom.Add(pool[3 % pool.Count]);
            OpenMenu(custom);
        }

        if (Keyboard.current.digit5Key.wasPressedThisFrame)
        {
            if (menuOpened) CloseMenu();
            List<GameObject> custom = new List<GameObject>();
            custom.Add(pool[0 % pool.Count]);
            custom.Add(pool[1 % pool.Count]);
            custom.Add(pool[2 % pool.Count]);
            custom.Add(pool[3 % pool.Count]);
            custom.Add(pool[4 % pool.Count]);
            OpenMenu(custom);
        }

        if (Keyboard.current.digit6Key.wasPressedThisFrame)
        {
            if (menuOpened) CloseMenu();
            List<GameObject> custom = new List<GameObject>();
            custom.Add(pool[0 % pool.Count]);
            custom.Add(pool[1 % pool.Count]);
            custom.Add(pool[2 % pool.Count]);
            custom.Add(pool[3 % pool.Count]);
            custom.Add(pool[4 % pool.Count]);
            custom.Add(pool[5 % pool.Count]);
            OpenMenu(custom);
        }
    }
}