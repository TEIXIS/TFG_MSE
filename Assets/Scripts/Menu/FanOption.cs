using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
public class FanOption : MonoBehaviour
{
    [Header("Tipo de Bot�n")]
    [Tooltip("M�rcalo para los viajes de la app. Desm�rcalo para los botones del men� inicial.")]
    public bool isTeleportButton = true;

    // --- NUEVO: BILLBOARDING ---
    [Header("Billboarding (Mirar al Usuario)")]
    [Tooltip("Si est� marcado, el bot�n girar� constantemente para mirar a la cara del usuario. Ideal para leer textos.")]
    public bool lookAtUser = false;
    [Tooltip("Si est� marcado, el bot�n se mantendr� recto (sin inclinarse hacia arriba/abajo). Ideal para men�s en cubos.")]
    public bool lockRotationToYAxis = true;
    private Transform mainCamera;



    [Header("L�gica Personalizada")]
    public UnityEvent onCustomClick;

    public int optionIndex;

    // MEG�FONO 1: Para los viajes antiguos (Teleport, Fadeout)
    public static event System.Action<int> OnOptionSelected;

    // MEG�FONO 2: NUEVO. Solo para el men� de inicio (Canvas, etc.)
    public static event System.Action<int> OnInitialMenuSelected;

    public static event System.Action<int, string> OnInitialMenuSelectedWithLabel;

    // MEG�FONO 3: Para cerrar el men� visual
    public static event System.Action OnMenuInteractionConfirmed;

    Vector3 targetLocalPos;
    Vector3 baseScale;
    Renderer rend;
    MaterialPropertyBlock mpb;

    public float hover;
    public float press;

    private float selectionBump = 0f;
    private bool isSelected = false;

    private bool needsReset = true;
    private float lifeTime = 0f;

    private Vector3 handWorldPos;

    Vector3 currentInteractionOffset;
    Quaternion currentInteractionRotation = Quaternion.identity;

    bool isAppearing = true;
    bool isFadingOut = false;
    float randomBreathPhase;
    private Vector3 currentAnimPos;

    private float currentScaleMultiplier = 1f;


    [Header("Accesibilidad - Movimiento Relax")]
    [Tooltip("Distancia que el bot�n vuela hacia la mano. Ponlo a 0 para que no se mueva de su sitio (ideal para Quads).")]
    public float attractionDistance = 0.08f;
   
    [Header("Accesibilidad - Movimiento Relax")]
    public float smoothSpeed = 4f;

    [Header("Feedback Visual y Tiempos")]
    public float maxGrowth = 1.25f;
    public float maxBrightness = 1.5f;

    [Range(0f, 1f)] public float requiredInflationToClick = 0.95f;

    private Color baseEmissionColor = Color.black;

    static readonly int ColorPropURP = Shader.PropertyToID("_BaseColor");
    static readonly int ColorPropBuiltIn = Shader.PropertyToID("_Color");
    static readonly int EmissionProp = Shader.PropertyToID("_EmissionColor");

    [Header("Audio Feedback")]
    public AudioClip pressSound;
    private AudioSource audioSource;
    private bool hasPlayedPressSound = false;

    public void SetHandPosition(Vector3 pos) { handWorldPos = pos; }

    public void SetTarget(Vector3 localPos)
    {
        targetLocalPos = localPos;
        isAppearing = true;
        lifeTime = 0f;
    }

    public Vector3 GetBaseWorldPosition()
    {
        if (transform.parent != null)
            return transform.parent.TransformPoint(targetLocalPos);
        return transform.position;
    }

    void Awake()
    {
        rend = GetComponentInChildren<Renderer>();
        mpb = new MaterialPropertyBlock();
        baseScale = transform.localScale;
        randomBreathPhase = Random.Range(0f, Mathf.PI * 2f);

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        audioSource.volume = 0.6f;

        if (rend.sharedMaterial.HasProperty(EmissionProp))
        {
            baseEmissionColor = rend.sharedMaterial.GetColor(EmissionProp);
        }

        // Buscamos la c�mara principal del casco al nacer
        if (Camera.main != null) mainCamera = Camera.main.transform;
    }

    public void AnimateAppear(float t)
    {
        Vector3 radial = Vector3.Lerp(Vector3.zero, targetLocalPos, t);
        float arcHeight = Mathf.Sin(t * Mathf.PI) * 0.08f;
        radial.y += arcHeight;

        currentAnimPos = radial;
        transform.localScale = baseScale * t;
        currentScaleMultiplier = 1f;

        SetAlpha(t);

        if (t >= 0.999f) isAppearing = false;
    }

    void Update()
    {
        lifeTime += Time.deltaTime;
        if (needsReset && lifeTime > 1.0f) needsReset = false;

        if (!isAppearing && !isFadingOut)
        {
            ProcessInteractions();

            float interactionLevel = Mathf.Clamp01(Mathf.Max(Mathf.Abs(hover), press));
            float targetScale = Mathf.Lerp(1f, maxGrowth, interactionLevel);

            currentScaleMultiplier = Mathf.Lerp(currentScaleMultiplier, targetScale, Time.deltaTime * smoothSpeed * 1.5f);
            transform.localScale = baseScale * currentScaleMultiplier;

            SetAlpha(1f);
        }
        else if (isFadingOut)
        {
            currentInteractionOffset = Vector3.Lerp(currentInteractionOffset, Vector3.zero, Time.deltaTime * smoothSpeed);
            currentInteractionRotation = Quaternion.Slerp(currentInteractionRotation, Quaternion.identity, Time.deltaTime * smoothSpeed);
        }

        float breath = Mathf.Sin(Time.time * 1.5f + randomBreathPhase) * 0.005f;
        Vector3 breathOffset = new Vector3(0, breath, 0);

        if (!isAppearing) currentAnimPos = targetLocalPos;

        transform.localPosition = currentAnimPos + currentInteractionOffset + breathOffset;

        // ====================================================
        // LA MAGIA DEL BILLBOARDING (Mirar al usuario)
        // ====================================================
        if (lookAtUser && mainCamera != null)
        {
            Vector3 targetCameraPos = mainCamera.position;

            // Si bloqueamos el eje Y, le mentimos a las matem�ticas y le decimos que 
            // la c�mara est� exactamente a la misma altura que el bot�n.
            if (lockRotationToYAxis)
            {
                targetCameraPos.y = transform.position.y;
            }

            Vector3 dirToCamera = transform.position - targetCameraPos;

            // Seguro anti-errores por si la c�mara est� matem�ticamente en el mismo p�xel
            if (dirToCamera.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(dirToCamera);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 10f);
            }
        }
        else
        {
            transform.localRotation = currentInteractionRotation;
        }
        //transform.localRotation = currentInteractionRotation;

        if (!isAppearing) selectionBump = Mathf.Lerp(selectionBump, 0f, Time.deltaTime * 6f);
    }

    void ProcessInteractions()
    {
        Vector3 targetOffset = Vector3.zero;

        Vector3 directionWorld = handWorldPos - GetBaseWorldPosition();
        Vector3 directionLocal = transform.parent != null
            ? transform.parent.InverseTransformDirection(directionWorld)
            : directionWorld;

        directionLocal.y = 0;

        Vector3 flatDirection = Vector3.back;
        if (directionLocal.sqrMagnitude > 0.001f) flatDirection = directionLocal.normalized;

        float attractionLevel = Mathf.Clamp01(Mathf.Max(Mathf.Abs(hover), press));

        // --- AQU� APLICAMOS TU VARIABLE ---
        targetOffset = flatDirection * (attractionLevel * attractionDistance);

        Vector3 neighborRepulsion = Vector3.zero;
        if (transform.parent != null)
        {
            foreach (Transform sibling in transform.parent)
            {
                if (sibling == transform) continue;

                Vector3 diff = transform.position - sibling.position;
                diff.y = 0;

                float dist = diff.magnitude;
                float minSpacing = 0.14f;

                if (dist < minSpacing && dist > 0.001f)
                {
                    float pushFactor = 1f - (dist / minSpacing);
                    Vector3 localPushDir = transform.parent.InverseTransformDirection(diff.normalized);
                    localPushDir.y = 0;
                    neighborRepulsion += localPushDir * (pushFactor * 0.05f);
                }
            }
        }
        targetOffset += neighborRepulsion;
        targetOffset += Vector3.down * selectionBump;

        currentInteractionOffset = Vector3.Lerp(currentInteractionOffset, targetOffset, Time.deltaTime * smoothSpeed);
        currentInteractionRotation = Quaternion.Slerp(currentInteractionRotation, Quaternion.identity, Time.deltaTime * smoothSpeed);
    }

    public void SetHover(float v)
    {
        if (isAppearing || isFadingOut) return;
        hover = Mathf.Clamp(v, -1f, 1f);
    }

    public void SetPress(float v)
    {
        float incomingPress = Mathf.Clamp01(v);
        press = incomingPress;

        if (isAppearing || isFadingOut) return;

        if (needsReset)
        {
            if (incomingPress < 0.5f) needsReset = false;
            else return;
        }

        float currentInflationProgress = 0f;
        if (maxGrowth > 1.01f)
            currentInflationProgress = Mathf.Clamp01((currentScaleMultiplier - 1f) / (maxGrowth - 1f));
        else
            currentInflationProgress = 1f;

        if (press >= 0.99f && currentInflationProgress >= requiredInflationToClick)
        {
            if (!hasPlayedPressSound && !isSelected)
            {
                hasPlayedPressSound = true;
                isSelected = true;
                selectionBump = 0.05f;

                if (audioSource != null && pressSound != null)
                {
                    audioSource.pitch = Random.Range(0.95f, 1.05f);
                    audioSource.PlayOneShot(pressSound, 1.0f);
                }

                TMPro.TMP_Text texto = GetComponentInChildren<TMPro.TMP_Text>();
                string label = texto != null ? texto.text.Trim() : "";
                bool esFlecha = (label == "<-" || label == "->");

                if (isTeleportButton)
                {
                    OnOptionSelected?.Invoke(optionIndex);
                }
                else
                {
                    OnInitialMenuSelected?.Invoke(optionIndex);
                    OnInitialMenuSelectedWithLabel?.Invoke(optionIndex, label);
                }

                onCustomClick?.Invoke();

                if (!esFlecha)
                {
                    OnMenuInteractionConfirmed?.Invoke();
                }
            }
        }
        else if (press < 0.1f)
        {
            hasPlayedPressSound = false;
            isSelected = false;
        }
    }

    public void ForceInteractionOff()
    {
        isFadingOut = true;
        press = 0f;
        hover = 0f;
    }

    public void SetAlpha(float alpha)
    {
        if (rend == null) return;
        rend.GetPropertyBlock(mpb);

        Color currentColor = rend.sharedMaterial.HasProperty(ColorPropURP)
            ? rend.sharedMaterial.GetColor(ColorPropURP)
            : rend.sharedMaterial.color;

        currentColor.a = alpha;

        if (rend.sharedMaterial.HasProperty(ColorPropURP))
            mpb.SetColor(ColorPropURP, currentColor);
        else
            mpb.SetColor(ColorPropBuiltIn, currentColor);

        if (rend.sharedMaterial.HasProperty(EmissionProp))
        {
            float scaleProgress = 0f;
            if (maxGrowth > 1f) scaleProgress = Mathf.Clamp01((currentScaleMultiplier - 1f) / (maxGrowth - 1f));

            float currentBrightness = (!isAppearing && !isFadingOut) ? Mathf.Lerp(1f, maxBrightness, scaleProgress) : 1f;
            Color targetEmission = baseEmissionColor * currentBrightness * alpha;
            mpb.SetColor(EmissionProp, targetEmission);
        }

        rend.SetPropertyBlock(mpb);
    }
}

/*using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
public class FanOption : MonoBehaviour
{

    [Header("L�gica Personalizada")]
    [Tooltip("A�ade aqu� qu� quieres que pase al hacer clic (Ej: Mostrar Canvas)")]
    public UnityEvent onCustomClick;

    public int optionIndex;
    // Evento antiguo (Por si tienes otros scripts us�ndolo para teletransportar)
    public static event System.Action<int> OnOptionSelected;

    // NUEVO: Evento que solo sirve para decirle al men� "Cierra los visuales, que han hecho click"
    public static event System.Action OnMenuInteractionConfirmed;

    Vector3 targetLocalPos;
    Vector3 baseScale;
    Renderer rend;
    MaterialPropertyBlock mpb;

    public float hover;
    public float press;

    private float selectionBump = 0f;
    private bool isSelected = false;

    // Seguros anti-bugs de teletransporte
    private bool needsReset = true;
    private float lifeTime = 0f;

    private Vector3 handWorldPos;

    Vector3 currentInteractionOffset;
    Quaternion currentInteractionRotation = Quaternion.identity;

    bool isAppearing = true;
    bool isFadingOut = false;
    float randomBreathPhase;
    private Vector3 currentAnimPos;

    private float currentScaleMultiplier = 1f;

    [Header("Accesibilidad - Movimiento Relax")]
    [Tooltip("Velocidad de atracci�n y crecimiento (menor = m�s zen)")]
    public float smoothSpeed = 4f;

    [Header("Feedback Visual y Tiempos")]
    [Tooltip("Cu�nto crece el bot�n al acercar la mano (1.25 = 25% m�s grande)")]
    public float maxGrowth = 1.25f;
    [Tooltip("Cu�nto aumenta la luz al cargar (1.5 = 50% m�s brillante)")]
    public float maxBrightness = 1.5f;

    // --- LA REGLA DE ORO DE ACCESIBILIDAD ---
    [Tooltip("Porcentaje m�nimo que debe inflarse el bot�n antes de permitir hacer clic (0.95 = 95%)")]
    [Range(0f, 1f)] public float requiredInflationToClick = 0.95f;

    private Color baseEmissionColor = Color.black;

    static readonly int ColorPropURP = Shader.PropertyToID("_BaseColor");
    static readonly int ColorPropBuiltIn = Shader.PropertyToID("_Color");
    static readonly int EmissionProp = Shader.PropertyToID("_EmissionColor");

    [Header("Audio Feedback")]
    public AudioClip pressSound;
    private AudioSource audioSource;
    private bool hasPlayedPressSound = false;

    public void SetHandPosition(Vector3 pos) { handWorldPos = pos; }

    public void SetTarget(Vector3 localPos)
    {
        targetLocalPos = localPos;
        isAppearing = true;
        lifeTime = 0f; // Reiniciamos el reloj de vida al nacer
    }

    public Vector3 GetBaseWorldPosition()
    {
        if (transform.parent != null)
            return transform.parent.TransformPoint(targetLocalPos);
        return transform.position;
    }

    void Awake()
    {
        rend = GetComponentInChildren<Renderer>();
        mpb = new MaterialPropertyBlock();
        baseScale = transform.localScale;
        randomBreathPhase = Random.Range(0f, Mathf.PI * 2f);

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        audioSource.volume = 0.6f;

        if (rend.sharedMaterial.HasProperty(EmissionProp))
        {
            baseEmissionColor = rend.sharedMaterial.GetColor(EmissionProp);
        }
    }

    public void AnimateAppear(float t)
    {
        Vector3 radial = Vector3.Lerp(Vector3.zero, targetLocalPos, t);
        float arcHeight = Mathf.Sin(t * Mathf.PI) * 0.08f;
        radial.y += arcHeight;

        currentAnimPos = radial;
        transform.localScale = baseScale * t;

        // Mientras nace, forzamos que el multiplicador sea 1 para que empiece a crecer desde cero cuando termine
        currentScaleMultiplier = 1f;

        SetAlpha(t);

        if (t >= 0.999f) isAppearing = false;
    }

    void Update()
    {
        // El reloj "Asesino de Bugs": si el bot�n lleva vivo m�s de 1 segundo, 
        // anulamos el seguro del teletransporte porque ya es imposible que sea un bug.
        lifeTime += Time.deltaTime;
        if (needsReset && lifeTime > 1.0f)
        {
            needsReset = false;
        }

        if (!isAppearing && !isFadingOut)
        {
            ProcessInteractions();

            // Calculamos cu�nto DEBE crecer seg�n la proximidad de la mano
            float interactionLevel = Mathf.Clamp01(Mathf.Max(Mathf.Abs(hover), press));
            float targetScale = Mathf.Lerp(1f, maxGrowth, interactionLevel);

            // Crecimiento progresivo y suave (Esto es lo que crea el "Tiempo de Espera")
            currentScaleMultiplier = Mathf.Lerp(currentScaleMultiplier, targetScale, Time.deltaTime * smoothSpeed * 1.5f);
            transform.localScale = baseScale * currentScaleMultiplier;

            SetAlpha(1f);
        }
        else if (isFadingOut)
        {
            currentInteractionOffset = Vector3.Lerp(currentInteractionOffset, Vector3.zero, Time.deltaTime * smoothSpeed);
            currentInteractionRotation = Quaternion.Slerp(currentInteractionRotation, Quaternion.identity, Time.deltaTime * smoothSpeed);
        }

        float breath = Mathf.Sin(Time.time * 1.5f + randomBreathPhase) * 0.005f;
        Vector3 breathOffset = new Vector3(0, breath, 0);

        if (!isAppearing)
        {
            currentAnimPos = targetLocalPos;
        }

        transform.localPosition = currentAnimPos + currentInteractionOffset + breathOffset;
        transform.localRotation = currentInteractionRotation;

        if (!isAppearing)
        {
            selectionBump = Mathf.Lerp(selectionBump, 0f, Time.deltaTime * 6f);
        }
    }

    void ProcessInteractions()
    {
        Vector3 targetOffset = Vector3.zero;

        Vector3 directionWorld = handWorldPos - GetBaseWorldPosition();
        Vector3 directionLocal = transform.parent != null
            ? transform.parent.InverseTransformDirection(directionWorld)
            : directionWorld;

        directionLocal.y = 0;

        Vector3 flatDirection = Vector3.back;
        if (directionLocal.sqrMagnitude > 0.001f)
        {
            flatDirection = directionLocal.normalized;
        }

        float attractionLevel = Mathf.Clamp01(Mathf.Max(Mathf.Abs(hover), press));
        targetOffset = flatDirection * (attractionLevel * 0.08f);

        Vector3 neighborRepulsion = Vector3.zero;
        if (transform.parent != null)
        {
            foreach (Transform sibling in transform.parent)
            {
                if (sibling == transform) continue;

                Vector3 diff = transform.position - sibling.position;
                diff.y = 0;

                float dist = diff.magnitude;
                float minSpacing = 0.14f;

                if (dist < minSpacing && dist > 0.001f)
                {
                    float pushFactor = 1f - (dist / minSpacing);
                    Vector3 localPushDir = transform.parent.InverseTransformDirection(diff.normalized);
                    localPushDir.y = 0;
                    neighborRepulsion += localPushDir * (pushFactor * 0.05f);
                }
            }
        }
        targetOffset += neighborRepulsion;

        targetOffset += Vector3.down * selectionBump;

        currentInteractionOffset = Vector3.Lerp(currentInteractionOffset, targetOffset, Time.deltaTime * smoothSpeed);
        currentInteractionRotation = Quaternion.Slerp(currentInteractionRotation, Quaternion.identity, Time.deltaTime * smoothSpeed);
    }

    public void SetHover(float v)
    {
        if (isAppearing || isFadingOut) return;
        hover = Mathf.Clamp(v, -1f, 1f);
    }

    public void SetPress(float v)
    {
        float incomingPress = Mathf.Clamp01(v);
        press = incomingPress;

        if (isAppearing || isFadingOut) return;

        // Seguro para el primer fotograma post-teletransporte
        if (needsReset)
        {
            if (incomingPress < 0.5f) needsReset = false;
            else return;
        }

        // --- LA REGLA ESTRICTA DE INFLADO ---
        // Calculamos qu� porcentaje real del inflado se ha completado visualmente (de 0.0 a 1.0)
        float currentInflationProgress = 0f;
        if (maxGrowth > 1.01f)
        {
            currentInflationProgress = Mathf.Clamp01((currentScaleMultiplier - 1f) / (maxGrowth - 1f));
        }
        else
        {
            currentInflationProgress = 1f; // Prevenci�n de errores si el usuario decide poner maxGrowth a 1
        }

        // AHORA EXIGIMOS LAS DOS COSAS A LA VEZ PARA HACER EL CLIC:
        // 1. Que el usuario est� pulsando a fondo (press >= 0.99)
        // 2. Que el bot�n haya tenido tiempo de inflarse en pantalla hasta el nivel exigido (ej: 95%)
        if (press >= 0.99f && currentInflationProgress >= requiredInflationToClick)
        {
            if (!hasPlayedPressSound && !isSelected)
            {
                hasPlayedPressSound = true;
                isSelected = true;
                selectionBump = 0.05f;

                if (audioSource != null && pressSound != null)
                {
                    audioSource.pitch = Random.Range(0.95f, 1.05f);
                    audioSource.PlayOneShot(pressSound, 1.0f);
                }

                // NUEVA L�GICA MODULAR:
                OnOptionSelected?.Invoke(optionIndex); // Para los viajes antiguos
                onCustomClick?.Invoke(); // Ejecuta lo que le pongas en el Inspector (Canvas, etc.)
                OnMenuInteractionConfirmed?.Invoke(); // Le dice al PalmMenuActivator que se cierre
            }
        }
        else if (press < 0.1f)
        {
            hasPlayedPressSound = false;
            isSelected = false;
        }
    }

    public void ForceInteractionOff()
    {
        isFadingOut = true;
        press = 0f;
        hover = 0f;
    }

    public void SetAlpha(float alpha)
    {
        if (rend == null) return;
        rend.GetPropertyBlock(mpb);

        Color currentColor = rend.sharedMaterial.HasProperty(ColorPropURP)
            ? rend.sharedMaterial.GetColor(ColorPropURP)
            : rend.sharedMaterial.color;

        currentColor.a = alpha;

        if (rend.sharedMaterial.HasProperty(ColorPropURP))
            mpb.SetColor(ColorPropURP, currentColor);
        else
            mpb.SetColor(ColorPropBuiltIn, currentColor);

        if (rend.sharedMaterial.HasProperty(EmissionProp))
        {
            float scaleProgress = 0f;
            if (maxGrowth > 1f) scaleProgress = Mathf.Clamp01((currentScaleMultiplier - 1f) / (maxGrowth - 1f));

            float currentBrightness = (!isAppearing && !isFadingOut) ? Mathf.Lerp(1f, maxBrightness, scaleProgress) : 1f;
            Color targetEmission = baseEmissionColor * currentBrightness * alpha;
            mpb.SetColor(EmissionProp, targetEmission);
        }

        rend.SetPropertyBlock(mpb);
    }
}*/
/*using UnityEngine;

[RequireComponent(typeof(Collider))]
public class FanOption : MonoBehaviour
{
    public int optionIndex;
    public static event System.Action<int> OnOptionSelected;

    Vector3 targetLocalPos;
    Vector3 baseScale;
    Renderer rend;
    MaterialPropertyBlock mpb;

    public float hover;
    public float press;

    private float selectionBump = 0f;
    private bool isSelected = false;

    // Seguro: Solo bloquea el Click, pero permite toda la animaci�n visual.
    private bool needsReset = true;

    private Vector3 handWorldPos;

    Vector3 currentInteractionOffset;
    Quaternion currentInteractionRotation = Quaternion.identity;

    bool isAppearing = true;
    bool isFadingOut = false;
    float randomBreathPhase;
    private Vector3 currentAnimPos;

    // Variable para suavizar el crecimiento siempre
    private float currentScaleMultiplier = 1f;

    [Header("Accesibilidad - Movimiento Relax")]
    [Tooltip("Velocidad de atracci�n y crecimiento (menor = m�s zen)")]
    public float smoothSpeed = 4f;

    [Header("Feedback Visual")]
    public float maxGrowth = 1.25f;
    public float maxBrightness = 1.5f;
    private Color baseEmissionColor = Color.black;

    static readonly int ColorPropURP = Shader.PropertyToID("_BaseColor");
    static readonly int ColorPropBuiltIn = Shader.PropertyToID("_Color");
    static readonly int EmissionProp = Shader.PropertyToID("_EmissionColor");

    [Header("Audio Feedback")]
    public AudioClip pressSound;
    private AudioSource audioSource;
    private bool hasPlayedPressSound = false;

    public void SetHandPosition(Vector3 pos) { handWorldPos = pos; }

    public void SetTarget(Vector3 localPos)
    {
        targetLocalPos = localPos;
        isAppearing = true;
    }

    public Vector3 GetBaseWorldPosition()
    {
        if (transform.parent != null)
            return transform.parent.TransformPoint(targetLocalPos);
        return transform.position;
    }

    void Awake()
    {
        rend = GetComponentInChildren<Renderer>();
        mpb = new MaterialPropertyBlock();
        baseScale = transform.localScale;
        randomBreathPhase = Random.Range(0f, Mathf.PI * 2f);

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        audioSource.volume = 0.6f;

        if (rend.sharedMaterial.HasProperty(EmissionProp))
        {
            baseEmissionColor = rend.sharedMaterial.GetColor(EmissionProp);
        }
    }

    public void AnimateAppear(float t)
    {
        Vector3 radial = Vector3.Lerp(Vector3.zero, targetLocalPos, t);
        float arcHeight = Mathf.Sin(t * Mathf.PI) * 0.08f;
        radial.y += arcHeight;

        currentAnimPos = radial;
        transform.localScale = baseScale * t;
        currentScaleMultiplier = 1f;

        SetAlpha(t);

        if (t >= 0.999f) isAppearing = false;
    }

    void Update()
    {
        if (!isAppearing && !isFadingOut)
        {
            ProcessInteractions();

            // Crecimiento guiado por la atracci�n pura
            float interactionLevel = Mathf.Clamp01(Mathf.Max(Mathf.Abs(hover), press));
            float targetScale = Mathf.Lerp(1f, maxGrowth, interactionLevel);

            // Crecimiento siempre suave, sin saltos
            currentScaleMultiplier = Mathf.Lerp(currentScaleMultiplier, targetScale, Time.deltaTime * smoothSpeed * 1.5f);
            transform.localScale = baseScale * currentScaleMultiplier;

            SetAlpha(1f);
        }
        else if (isFadingOut)
        {
            currentInteractionOffset = Vector3.Lerp(currentInteractionOffset, Vector3.zero, Time.deltaTime * smoothSpeed);
            currentInteractionRotation = Quaternion.Slerp(currentInteractionRotation, Quaternion.identity, Time.deltaTime * smoothSpeed);
        }

        // Flotaci�n constante que no se interrumpe nunca
        float breath = Mathf.Sin(Time.time * 1.5f + randomBreathPhase) * 0.005f;
        Vector3 breathOffset = new Vector3(0, breath, 0);

        if (!isAppearing)
        {
            currentAnimPos = targetLocalPos;
        }

        transform.localPosition = currentAnimPos + currentInteractionOffset + breathOffset;
        transform.localRotation = currentInteractionRotation;

        if (!isAppearing)
        {
            selectionBump = Mathf.Lerp(selectionBump, 0f, Time.deltaTime * 6f);
        }
    }

    void ProcessInteractions()
    {
        Vector3 targetOffset = Vector3.zero;

        // Atracci�n hacia la mano bloqueada en el plano 2D (Y = 0)
        Vector3 directionWorld = handWorldPos - GetBaseWorldPosition();
        Vector3 directionLocal = transform.parent != null
            ? transform.parent.InverseTransformDirection(directionWorld)
            : directionWorld;

        directionLocal.y = 0;

        Vector3 flatDirection = Vector3.back;
        if (directionLocal.sqrMagnitude > 0.001f)
        {
            flatDirection = directionLocal.normalized;
        }

        float attractionLevel = Mathf.Clamp01(Mathf.Max(Mathf.Abs(hover), press));
        targetOffset = flatDirection * (attractionLevel * 0.08f);

        // Repulsi�n de vecinos plana (para mantener distancias sin saltar hacia arriba/abajo)
        Vector3 neighborRepulsion = Vector3.zero;
        if (transform.parent != null)
        {
            foreach (Transform sibling in transform.parent)
            {
                if (sibling == transform) continue;

                Vector3 diff = transform.position - sibling.position;
                diff.y = 0;

                float dist = diff.magnitude;
                float minSpacing = 0.14f;

                if (dist < minSpacing && dist > 0.001f)
                {
                    float pushFactor = 1f - (dist / minSpacing);
                    Vector3 localPushDir = transform.parent.InverseTransformDirection(diff.normalized);
                    localPushDir.y = 0;
                    neighborRepulsion += localPushDir * (pushFactor * 0.05f);
                }
            }
        }
        targetOffset += neighborRepulsion;

        targetOffset += Vector3.down * selectionBump;

        // Movimiento suave y fluido
        currentInteractionOffset = Vector3.Lerp(currentInteractionOffset, targetOffset, Time.deltaTime * smoothSpeed);
        currentInteractionRotation = Quaternion.Slerp(currentInteractionRotation, Quaternion.identity, Time.deltaTime * smoothSpeed);
    }

    public void SetHover(float v)
    {
        // �LA VENDA FUERA! Siempre registra la curiosidad de la mano.
        hover = Mathf.Clamp(v, -1f, 1f);
    }

    public void SetPress(float v)
    {
        float incomingPress = Mathf.Clamp01(v);

        // �LA VENDA FUERA! Siempre guarda la presi�n para que las animaciones visuales funcionen.
        press = incomingPress;

        // Si est� naciendo o muriendo, NO EVALUAMOS CLICKS. 
        // As� evitamos viajar por accidente a mitad de animaci�n.
        if (isAppearing || isFadingOut) return;

        // El seguro ahora es perfecto: como el bot�n ya vio llegar tu mano de lejos, 
        // no se asustar� ni se bloquear�.
        if (needsReset)
        {
            if (incomingPress < 0.5f) needsReset = false;
            else return;
        }

        if (press >= 0.99f)
        {
            if (!hasPlayedPressSound && !isSelected)
            {
                hasPlayedPressSound = true;
                isSelected = true;
                selectionBump = 0.05f;

                if (audioSource != null && pressSound != null)
                {
                    audioSource.pitch = Random.Range(0.95f, 1.05f);
                    audioSource.PlayOneShot(pressSound, 1.0f);
                }

                OnOptionSelected?.Invoke(optionIndex);
            }
        }
        else if (press < 0.1f)
        {
            hasPlayedPressSound = false;
            isSelected = false;
        }
    }

    public void ForceInteractionOff()
    {
        isFadingOut = true;
        press = 0f;
        hover = 0f;
    }

    public void SetAlpha(float alpha)
    {
        if (rend == null) return;
        rend.GetPropertyBlock(mpb);

        Color currentColor = rend.sharedMaterial.HasProperty(ColorPropURP)
            ? rend.sharedMaterial.GetColor(ColorPropURP)
            : rend.sharedMaterial.color;

        currentColor.a = alpha;

        if (rend.sharedMaterial.HasProperty(ColorPropURP))
            mpb.SetColor(ColorPropURP, currentColor);
        else
            mpb.SetColor(ColorPropBuiltIn, currentColor);

        if (rend.sharedMaterial.HasProperty(EmissionProp))
        {
            float scaleProgress = 0f;
            if (maxGrowth > 1f) scaleProgress = Mathf.Clamp01((currentScaleMultiplier - 1f) / (maxGrowth - 1f));

            float currentBrightness = (!isAppearing && !isFadingOut) ? Mathf.Lerp(1f, maxBrightness, scaleProgress) : 1f;
            Color targetEmission = baseEmissionColor * currentBrightness * alpha;
            mpb.SetColor(EmissionProp, targetEmission);
        }

        rend.SetPropertyBlock(mpb);
    }
}*/
/*using UnityEngine;

[RequireComponent(typeof(Collider))]
public class FanOption : MonoBehaviour
{
    public int optionIndex;
    public static event System.Action<int> OnOptionSelected;

    Vector3 targetLocalPos;
    Vector3 baseScale;
    Renderer rend;
    MaterialPropertyBlock mpb;

    public float hover;
    public float press;

    private float selectionBump = 0f;
    private bool isSelected = false;

    // Seguro silencioso: Evita clicks fantasmas al teletransportarse, pero no interrumpe NINGUNA animaci�n.
    private bool ghostClickPreventer = true;

    private Vector3 handWorldPos;

    Vector3 currentInteractionOffset;
    Quaternion currentInteractionRotation = Quaternion.identity;

    bool isAppearing = true;
    bool isFadingOut = false;
    float randomBreathPhase;
    private Vector3 currentAnimPos;

    private float currentScaleMultiplier = 1f;

    [Header("Accesibilidad - Movimiento Relax")]
    [Tooltip("Velocidad de todas las animaciones (menor = m�s suave y zen)")]
    public float smoothSpeed = 3.5f;

    [Header("Feedback Visual")]
    [Tooltip("Cu�nto crece el bot�n al acercar la mano (1.25 = 25% m�s grande)")]
    public float maxGrowth = 1.25f;
    [Tooltip("Cu�nto aumenta la luz al cargar (1.5 = 50% m�s brillante)")]
    public float maxBrightness = 1.5f;
    private Color baseEmissionColor = Color.black;

    static readonly int ColorPropURP = Shader.PropertyToID("_BaseColor");
    static readonly int ColorPropBuiltIn = Shader.PropertyToID("_Color");
    static readonly int EmissionProp = Shader.PropertyToID("_EmissionColor");

    [Header("Audio Feedback")]
    public AudioClip pressSound;
    private AudioSource audioSource;
    private bool hasPlayedPressSound = false;

    public void SetHandPosition(Vector3 pos) { handWorldPos = pos; }

    public void SetTarget(Vector3 localPos)
    {
        targetLocalPos = localPos;
        isAppearing = true;
    }

    public Vector3 GetBaseWorldPosition()
    {
        if (transform.parent != null)
            return transform.parent.TransformPoint(targetLocalPos);
        return transform.position;
    }

    void Awake()
    {
        rend = GetComponentInChildren<Renderer>();
        mpb = new MaterialPropertyBlock();
        baseScale = transform.localScale;
        randomBreathPhase = Random.Range(0f, Mathf.PI * 2f);

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        audioSource.volume = 0.6f;

        if (rend.sharedMaterial.HasProperty(EmissionProp))
        {
            baseEmissionColor = rend.sharedMaterial.GetColor(EmissionProp);
        }
    }

    public void AnimateAppear(float t)
    {
        Vector3 radial = Vector3.Lerp(Vector3.zero, targetLocalPos, t);
        float arcHeight = Mathf.Sin(t * Mathf.PI) * 0.08f;
        radial.y += arcHeight;

        currentAnimPos = radial;
        transform.localScale = baseScale * t;
        currentScaleMultiplier = 1f;

        SetAlpha(t);

        if (t >= 0.999f) isAppearing = false;
    }

    void Update()
    {
        if (!isAppearing && !isFadingOut)
        {
            ProcessInteractions();

            // Crecimiento suave basado puramente en cu�nto te acercas
            float interactionLevel = Mathf.Clamp01(Mathf.Max(Mathf.Abs(hover), press));
            float targetScale = Mathf.Lerp(1f, maxGrowth, interactionLevel);

            // Armon�a: Se escala usando el mismo suavizado que el movimiento
            currentScaleMultiplier = Mathf.Lerp(currentScaleMultiplier, targetScale, Time.deltaTime * smoothSpeed);
            transform.localScale = baseScale * currentScaleMultiplier;

            SetAlpha(1f);
        }
        else if (isFadingOut)
        {
            // Retorno suave a la posici�n base
            currentInteractionOffset = Vector3.Lerp(currentInteractionOffset, Vector3.zero, Time.deltaTime * smoothSpeed);
            currentInteractionRotation = Quaternion.Slerp(currentInteractionRotation, Quaternion.identity, Time.deltaTime * smoothSpeed);
        }

        // Flotaci�n constante (Eje Y) inalterada
        float breath = Mathf.Sin(Time.time * 1.5f + randomBreathPhase) * 0.005f;
        Vector3 breathOffset = new Vector3(0, breath, 0);

        if (!isAppearing)
        {
            currentAnimPos = targetLocalPos;
        }

        transform.localPosition = currentAnimPos + currentInteractionOffset + breathOffset;
        transform.localRotation = currentInteractionRotation;

        if (!isAppearing)
        {
            selectionBump = Mathf.Lerp(selectionBump, 0f, Time.deltaTime * 6f);
        }
    }

    void ProcessInteractions()
    {
        Vector3 targetOffset = Vector3.zero;
        Quaternion targetRotation = Quaternion.identity;

        // 1. Atracci�n hacia la mano
        Vector3 directionWorld = handWorldPos - GetBaseWorldPosition();
        Vector3 directionLocal = transform.parent != null
            ? transform.parent.InverseTransformDirection(directionWorld)
            : directionWorld;

        // BLOQUEO AL PLANO HORIZONTAL
        directionLocal.y = 0;

        Vector3 flatDirection = Vector3.back;
        if (directionLocal.sqrMagnitude > 0.001f)
        {
            flatDirection = directionLocal.normalized;
        }

        // Simplemente calculamos el nivel de inter�s (hover o press)
        float attractionLevel = Mathf.Clamp01(Mathf.Max(Mathf.Abs(hover), press));

        // El bot�n se desliza hasta un m�ximo de 8cm hacia la mano
        targetOffset = flatDirection * (attractionLevel * 0.08f);

        // 2. Repulsi�n entre vecinos
        Vector3 neighborRepulsion = Vector3.zero;
        if (transform.parent != null)
        {
            foreach (Transform sibling in transform.parent)
            {
                if (sibling == transform) continue;

                Vector3 diff = transform.position - sibling.position;
                // BLOQUEO AL PLANO PARA VECINOS: As� no se empujan hacia arriba o abajo
                diff.y = 0;

                float dist = diff.magnitude;
                float minSpacing = 0.14f;

                if (dist < minSpacing && dist > 0.001f)
                {
                    float pushFactor = 1f - (dist / minSpacing);
                    Vector3 localPushDir = transform.parent.InverseTransformDirection(diff.normalized);
                    localPushDir.y = 0;
                    neighborRepulsion += localPushDir * (pushFactor * 0.05f);
                }
            }
        }
        targetOffset += neighborRepulsion;

        // 3. Rebote de selecci�n
        targetOffset += Vector3.down * selectionBump;

        // Suavizado total e incondicional
        currentInteractionOffset = Vector3.Lerp(currentInteractionOffset, targetOffset, Time.deltaTime * smoothSpeed);
        currentInteractionRotation = Quaternion.Slerp(currentInteractionRotation, targetRotation, Time.deltaTime * smoothSpeed);
    }

    // --- ENTRADA DE DATOS (Input) ---

    public void SetHover(float v)
    {
        if (isAppearing || isFadingOut) return;
        hover = Mathf.Clamp(v, -1f, 1f); // Siempre lo aceptamos sin condiciones
    }

    public void SetPress(float v)
    {
        if (isAppearing || isFadingOut) return;

        float incomingPress = Mathf.Clamp01(v);

        // Siempre lo aceptamos visualmente. Si retira la mano, esto bajar� suavemente a 0.
        press = incomingPress;

        // Seguro para el Click (Para no activar el viaje sin querer)
        if (ghostClickPreventer)
        {
            if (incomingPress < 0.5f) ghostClickPreventer = false;
            else return; // Bloquea �NICAMENTE el click final, pero permite toda la animaci�n visual.
        }

        // Acci�n de Confirmaci�n
        if (press >= 0.99f)
        {
            if (!hasPlayedPressSound && !isSelected)
            {
                hasPlayedPressSound = true;
                isSelected = true;
                selectionBump = 0.05f;

                if (audioSource != null && pressSound != null)
                {
                    audioSource.pitch = Random.Range(0.95f, 1.05f);
                    audioSource.PlayOneShot(pressSound, 1.0f);
                }

                OnOptionSelected?.Invoke(optionIndex);
            }
        }
        else if (press < 0.1f)
        {
            hasPlayedPressSound = false;
            isSelected = false;
        }
    }

    public void ForceInteractionOff()
    {
        isFadingOut = true;
        press = 0f;
        hover = 0f;
    }

    public void SetAlpha(float alpha)
    {
        if (rend == null) return;
        rend.GetPropertyBlock(mpb);

        Color currentColor = rend.sharedMaterial.HasProperty(ColorPropURP)
            ? rend.sharedMaterial.GetColor(ColorPropURP)
            : rend.sharedMaterial.color;

        currentColor.a = alpha;

        if (rend.sharedMaterial.HasProperty(ColorPropURP))
            mpb.SetColor(ColorPropURP, currentColor);
        else
            mpb.SetColor(ColorPropBuiltIn, currentColor);

        if (rend.sharedMaterial.HasProperty(EmissionProp))
        {
            float scaleProgress = 0f;
            if (maxGrowth > 1f) scaleProgress = Mathf.Clamp01((currentScaleMultiplier - 1f) / (maxGrowth - 1f));

            float currentBrightness = (!isAppearing && !isFadingOut) ? Mathf.Lerp(1f, maxBrightness, scaleProgress) : 1f;
            Color targetEmission = baseEmissionColor * currentBrightness * alpha;
            mpb.SetColor(EmissionProp, targetEmission);
        }

        rend.SetPropertyBlock(mpb);
    }
}*/