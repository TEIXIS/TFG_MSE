using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FanMenu : MonoBehaviour
{
    public FanMenuSelection selection;

    public GameObject optionPrefab;
    [Range(1, 7)] public int optionCount = 4;

    public float radius = 0.35f;
    [Tooltip("Grados de separaci�n entre cada opci�n (Recomendado: 25 para Hand Tracking).")]
    public float spacingAngle = 30f;

    [Header("Tamano automatico de opciones")]
    public bool autoSizeOptions = true;
    [Range(0.2f, 1f)] public float optionSizeBySpacing = 0.7f;
    public Vector2 optionSizeLimits = new Vector2(0.08f, 0.14f);
    public Vector2 optionScaleLimits = new Vector2(0.01f, 0.14f);
    public float interactionColliderPadding = 1.0f;

    [Header("Animaci�n Relajante")]
    [Tooltip("Duraci�n base de la animaci�n para cada bot�n.")]
    public float appearDuration = 1.5f;
    [Tooltip("Retraso entre la aparici�n de cada bot�n (Efecto cascada).")]
    public float staggerDelay = 0.15f;
  //  [Tooltip("Tiempo que tarda el men� en desvanecerse al cerrarlo o viajar.")]
  //  public float disappearDuration = 1.2f;

    List<FanOption> options = new();
    private Coroutine disappearCoroutine;

    public void Build(List<GameObject> customOptions = null, bool fitOptionsToMenu = false)
    {
        // Si estaba desapareciendo, lo cortamos de golpe
        if (disappearCoroutine != null) StopCoroutine(disappearCoroutine);
        StopAllCoroutines();

        Clear();
        List<GameObject> prefabsToUse = new();

        if (customOptions != null && customOptions.Count > 0)
        {
            int count = Mathf.Clamp(customOptions.Count, 1, 7);
            for (int i = 0; i < count; i++) prefabsToUse.Add(customOptions[i]);
            optionCount = count;
        }
        else if (selection != null && selection.selectedOptions.Count > 0)
        {
            int count = Mathf.Clamp(selection.selectedOptions.Count, 1, 7);
            for (int i = 0; i < count; i++) prefabsToUse.Add(selection.selectedOptions[i]);
            optionCount = count;
        }
        else
        {
            optionCount = Mathf.Clamp(optionCount, 1, 7);
            for (int i = 0; i < optionCount; i++) prefabsToUse.Add(optionPrefab);
        }

        float startAngle = -(optionCount - 1) * spacingAngle / 2f;
        float optionSize = CalculateOptionSize();

        for (int i = 0; i < optionCount; i++)
        {
            float angle = startAngle + spacingAngle * i;
            float rad = angle * Mathf.Deg2Rad;

            Vector3 targetPos = new Vector3(Mathf.Sin(rad) * radius, 0, (Mathf.Cos(rad) * radius) - radius);

            GameObject go = Instantiate(prefabsToUse[i], transform);
            go.transform.localPosition = Vector3.zero;
            Debug.Log($"[FanMenu][Build] Spawn index={i} prefab={prefabsToUse[i].name} instance={go.name} fit={fitOptionsToMenu} hasFanOptionBefore={go.GetComponent<FanOption>() != null}");

            if (autoSizeOptions && fitOptionsToMenu)
            {
                go.transform.localScale = CalculateFittedScale(go, optionSize);
                ConfigureInteractionCollider(go);
            }

            var opt = go.GetComponent<FanOption>();
            if (opt == null && autoSizeOptions && fitOptionsToMenu)
                opt = AddRuntimeFanOption(go);

            if (opt != null)
            {
                if (autoSizeOptions && fitOptionsToMenu)
                {
                    opt.ApplyBaseScale(go.transform.localScale);
                }

                opt.optionIndex = i;
                opt.SetTarget(targetPos);
                options.Add(opt);
                Debug.Log($"[FanMenu][Build] Registered option index={i} name={go.name} teleport={opt.isTeleportButton} target={targetPos} scale={go.transform.localScale}");
            }
            else
            {
                Debug.LogWarning($"[FanMenu][Build] Instance {go.name} has no FanOption and will not be clickable.");
            }
        }

        StartCoroutine(AppearAnimation());
    }

    FanOption AddRuntimeFanOption(GameObject option)
    {
        FanOption fanOption = option.AddComponent<FanOption>();
        fanOption.isTeleportButton = true;

        BoxCollider interactionBox = option.GetComponent<BoxCollider>();
        if (interactionBox != null)
            fanOption.SetInteractionLocalCenter(interactionBox.center);

        Debug.Log($"[FanMenu][Build] Added runtime FanOption to {option.name}. collider={(interactionBox != null ? interactionBox.name : "null")} center={(interactionBox != null ? interactionBox.center.ToString() : "none")} size={(interactionBox != null ? interactionBox.size.ToString() : "none")}");
        return fanOption;
    }

    float CalculateOptionSize()
    {
        float spacingRadians = Mathf.Abs(spacingAngle) * Mathf.Deg2Rad;
        float availableWidth = optionCount > 1
            ? 2f * radius * Mathf.Sin(spacingRadians * 0.5f)
            : radius * 0.45f;

        float minSize = Mathf.Min(optionSizeLimits.x, optionSizeLimits.y);
        float maxSize = Mathf.Max(optionSizeLimits.x, optionSizeLimits.y);
        return Mathf.Clamp(availableWidth * optionSizeBySpacing, minSize, maxSize);
    }

    Vector3 CalculateFittedScale(GameObject option, float targetSize)
    {
        Bounds bounds;
        if (!TryCalculateWorldBounds(option, false, out bounds) &&
            !TryCalculateWorldBounds(option, true, out bounds))
        {
            return Vector3.one * targetSize;
        }

        float largestDimension = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        if (largestDimension <= 0.0001f)
            return option.transform.localScale;

        Vector3 fittedScale = option.transform.localScale * (targetSize / largestDimension);
        float largestScaleAxis = Mathf.Max(Mathf.Abs(fittedScale.x), Mathf.Abs(fittedScale.y), Mathf.Abs(fittedScale.z));
        float maxScale = Mathf.Max(optionScaleLimits.x, optionScaleLimits.y);

        if (largestScaleAxis > maxScale && largestScaleAxis > 0.0001f)
            fittedScale *= maxScale / largestScaleAxis;

        return fittedScale;
    }

    void ConfigureInteractionCollider(GameObject option)
    {
        Bounds localBounds;
        if (!TryCalculateLocalModelBounds(option, out localBounds))
        {
            Debug.LogWarning($"[FanMenu][Collider] {option.name} has no model renderers to build an interaction collider.");
            return;
        }

        float padding = Mathf.Max(0.01f, interactionColliderPadding);
        Vector3 size = localBounds.size * padding;
        size.x = Mathf.Max(size.x, 0.02f);
        size.y = Mathf.Max(size.y, 0.02f);
        size.z = Mathf.Max(size.z, 0.02f);

        BoxCollider box = option.GetComponent<BoxCollider>();
        if (box == null)
            box = option.AddComponent<BoxCollider>();

        box.isTrigger = true;
        box.center = localBounds.center;
        box.size = size;
        Debug.Log($"[FanMenu][Collider] option={option.name} center={box.center} size={box.size} scale={option.transform.localScale}");

        FanOption fanOption = option.GetComponent<FanOption>();
        if (fanOption != null)
            fanOption.SetInteractionLocalCenter(localBounds.center);
    }

    bool TryCalculateLocalModelBounds(GameObject option, out Bounds bounds)
    {
        bool hasBounds = false;
        bounds = new Bounds(Vector3.zero, Vector3.zero);
        Renderer[] renderers = option.GetComponentsInChildren<Renderer>(true);

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || renderer is LineRenderer || renderer is TrailRenderer || renderer is ParticleSystemRenderer)
                continue;

            Bounds rendererLocalBounds = renderer.localBounds;
            Vector3 center = rendererLocalBounds.center;
            Vector3 extents = rendererLocalBounds.extents;

            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 rendererLocalCorner = center + Vector3.Scale(extents, new Vector3(x, y, z));
                        Vector3 worldCorner = renderer.transform.TransformPoint(rendererLocalCorner);
                        Vector3 optionLocalCorner = option.transform.InverseTransformPoint(worldCorner);

                        if (!hasBounds)
                        {
                            bounds = new Bounds(optionLocalCorner, Vector3.zero);
                            hasBounds = true;
                        }
                        else
                        {
                            bounds.Encapsulate(optionLocalCorner);
                        }
                    }
                }
            }
        }

        return hasBounds;
    }
    bool TryCalculateWorldBounds(GameObject option, bool useColliders, out Bounds bounds)
    {
        bool hasBounds = false;
        bounds = new Bounds(option.transform.position, Vector3.zero);

        if (!useColliders)
        {
            Renderer[] renderers = option.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null)
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
        }
        else
        {
            Collider[] colliders = option.GetComponentsInChildren<Collider>(true);
            foreach (Collider collider in colliders)
            {
                if (collider == null)
                    continue;

                if (!hasBounds)
                {
                    bounds = collider.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(collider.bounds);
                }
            }
        }

        return hasBounds;
    }

    Bounds WorldBoundsToLocalBounds(Transform root, Bounds worldBounds)
    {
        Vector3 min = Vector3.positiveInfinity;
        Vector3 max = Vector3.negativeInfinity;
        Vector3 center = worldBounds.center;
        Vector3 extents = worldBounds.extents;

        for (int x = -1; x <= 1; x += 2)
        {
            for (int y = -1; y <= 1; y += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 worldCorner = center + Vector3.Scale(extents, new Vector3(x, y, z));
                    Vector3 localCorner = root.InverseTransformPoint(worldCorner);
                    min = Vector3.Min(min, localCorner);
                    max = Vector3.Max(max, localCorner);
                }
            }
        }

        Bounds localBounds = new Bounds((min + max) * 0.5f, max - min);
        return localBounds;
    }

    public Transform GetOptionTransform(int index)
{
    if (index < 0 || index >= transform.childCount) return null;
    return transform.GetChild(index);
}

    void Clear()
    {
        options.Clear();

        while (transform.childCount > 0)
        {
            Transform child = transform.GetChild(0);
            child.SetParent(null);   // lo sacamos del menú inmediatamente
            Destroy(child.gameObject);
        }
    }

    IEnumerator AppearAnimation()
    {
        float totalDuration = appearDuration + (options.Count * staggerDelay);
        float time = 0;

        while (time < totalDuration)
        {
            time += Time.deltaTime;
            for (int i = 0; i < options.Count; i++)
            {
                float startTime = i * staggerDelay;
                float localTime = Mathf.Clamp01((time - startTime) / appearDuration);
                float ease = 1f - Mathf.Pow(1f - localTime, 4f);
                options[i].AnimateAppear(ease);
            }
            yield return null;
        }
    }

    // --- NUEVA L�GICA DE FADE OUT ---
    public void CloseMenuAnimated(float duration, System.Action onComplete)
    {
        if (!gameObject.activeInHierarchy)
        {
            Clear();
            onComplete?.Invoke();
            return;
        }

        if (disappearCoroutine != null) StopCoroutine(disappearCoroutine);
        disappearCoroutine = StartCoroutine(DisappearAnimation(duration, onComplete));
    }

    IEnumerator DisappearAnimation(float duration, System.Action onComplete)
    {
        float time = 0;

        foreach (var opt in options)
        {
            if (opt != null) opt.ForceInteractionOff();
        }

        while (time < duration)
        {
            time += Time.deltaTime;
            float alpha = 1f - Mathf.Clamp01(time / duration);

            foreach (var opt in options)
            {
                if (opt != null) opt.SetAlpha(alpha);
            }
            yield return null;
        }

        Clear();
        onComplete?.Invoke();
    }

    /*  public void PlaceInFrontOfUser(Transform head, Transform leftHand, Transform rightHand, float distance, float verticalOffset)
      {
          Vector3 forward = head.forward;
          forward.y = 0f;
          forward.Normalize();
          Vector3 targetPos = head.position + forward * distance;
          float handsY = (leftHand.position.y + rightHand.position.y) / 2f;
          targetPos.y = handsY + verticalOffset;

          transform.position = targetPos;
          transform.rotation = Quaternion.LookRotation(forward);
      }*/

    public void PlaceInFrontOfUser(Transform head, Transform leftHand, Transform rightHand, float distance, float verticalOffset)
    {
        Vector3 forward = head.forward;
        forward.y = 0f;
        forward.Normalize();

        // 1. Calculamos la posici�n base (X y Z) frente al usuario
        Vector3 targetPos = head.position + forward * distance;

        // --- EL CAMBIO ERGON�MICO ---
        // 2. En lugar de usar la altura de las manos, usamos la altura de la cabeza (ojos).
        // Restamos 0.35f (35 cent�metros) para que quede a la altura c�moda del pecho.
        // Sumamos el verticalOffset por si desde el Inspector quieres ajustarlo un poco.
        targetPos.y = head.position.y - 0.35f + verticalOffset;

        transform.position = targetPos;
        transform.rotation = Quaternion.LookRotation(forward);
    }
}
