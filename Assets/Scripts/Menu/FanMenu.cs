using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FanMenu : MonoBehaviour
{
    public FanMenuSelection selection;

    public GameObject optionPrefab;
    [Range(1, 6)] public int optionCount = 4;

    public float radius = 0.35f;
    [Tooltip("Grados de separación entre cada opción (Recomendado: 25 para Hand Tracking).")]
    public float spacingAngle = 30f;

    [Header("Animación Relajante")]
    [Tooltip("Duración base de la animación para cada botón.")]
    public float appearDuration = 1.5f;
    [Tooltip("Retraso entre la aparición de cada botón (Efecto cascada).")]
    public float staggerDelay = 0.15f;
  //  [Tooltip("Tiempo que tarda el menú en desvanecerse al cerrarlo o viajar.")]
  //  public float disappearDuration = 1.2f;

    List<FanOption> options = new();
    private Coroutine disappearCoroutine;

    public void Build(List<GameObject> customOptions = null)
    {
        // Si estaba desapareciendo, lo cortamos de golpe
        if (disappearCoroutine != null) StopCoroutine(disappearCoroutine);

        Clear();
        List<GameObject> prefabsToUse = new();

        if (customOptions != null && customOptions.Count > 0)
        {
            int count = Mathf.Clamp(customOptions.Count, 1, 6);
            for (int i = 0; i < count; i++) prefabsToUse.Add(customOptions[i]);
            optionCount = count;
        }
        else if (selection != null && selection.selectedOptions.Count > 0)
        {
            int count = Mathf.Clamp(selection.selectedOptions.Count, 1, 6);
            for (int i = 0; i < count; i++) prefabsToUse.Add(selection.selectedOptions[i]);
            optionCount = count;
        }
        else
        {
            optionCount = Mathf.Clamp(optionCount, 1, 6);
            for (int i = 0; i < optionCount; i++) prefabsToUse.Add(optionPrefab);
        }

        float startAngle = -(optionCount - 1) * spacingAngle / 2f;

        for (int i = 0; i < optionCount; i++)
        {
            float angle = startAngle + spacingAngle * i;
            float rad = angle * Mathf.Deg2Rad;

            Vector3 targetPos = new Vector3(Mathf.Sin(rad) * radius, 0, (Mathf.Cos(rad) * radius) - radius);

            GameObject go = Instantiate(prefabsToUse[i], transform);
            go.transform.localPosition = Vector3.zero;

            var opt = go.GetComponent<FanOption>();
            if (opt != null)
            {
                opt.optionIndex = i;
                opt.SetTarget(targetPos);
                options.Add(opt);
            }
        }

        StartCoroutine(AppearAnimation());
    }

    void Clear()
    {
        foreach (Transform c in transform) Destroy(c.gameObject);
        options.Clear();
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

    // --- NUEVA LÓGICA DE FADE OUT ---
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

        // 1. Calculamos la posición base (X y Z) frente al usuario
        Vector3 targetPos = head.position + forward * distance;

        // --- EL CAMBIO ERGONÓMICO ---
        // 2. En lugar de usar la altura de las manos, usamos la altura de la cabeza (ojos).
        // Restamos 0.35f (35 centímetros) para que quede a la altura cómoda del pecho.
        // Sumamos el verticalOffset por si desde el Inspector quieres ajustarlo un poco.
        targetPos.y = head.position.y - 0.35f + verticalOffset;

        transform.position = targetPos;
        transform.rotation = Quaternion.LookRotation(forward);
    }
}