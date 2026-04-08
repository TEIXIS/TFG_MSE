using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class TeleportManager : MonoBehaviour
{
    [Header("El Jugador")]
    public Transform vrRig;
    public Camera centerEyeCamera;

    [Header("Puntos de Destino")]
    public Transform[] teleportDestinations;

    [Header("Ajustes del Viaje (Fade)")]
    public float fadeOutDuration = 1.0f;
    public float fadeInDuration = 1.5f;
    public Color fadeColor = Color.black;

    private Image fadeImage;

    // Creamos un "altavoz" para avisar a otros scripts de que hemos viajado
    public System.Action<int> OnUsuarioTeletransportado;
    void Start()
    {
        if (centerEyeCamera == null)
        {
            centerEyeCamera = Camera.main;
        }
        CrearPantallaDeFundido();
    }

    void OnEnable() { FanOption.OnOptionSelected += HandleTeleport; }
    void OnDisable() { FanOption.OnOptionSelected -= HandleTeleport; }

    void HandleTeleport(int optionIndex)
    {
        if (optionIndex < 0 || optionIndex >= teleportDestinations.Length) return;

        Transform targetPoint = teleportDestinations[optionIndex];
        if (targetPoint != null)
        {
            StartCoroutine(TeleportRoutine(targetPoint));

            //  ¡Gritamos por el altavoz a qué índice hemos viajado! ---
            OnUsuarioTeletransportado?.Invoke(optionIndex);
        }
    }

    IEnumerator TeleportRoutine(Transform target)
    {
        // 1. Viaje a la oscuridad (usamos la nueva función pública)
        yield return StartCoroutine(FadeToBlack(fadeOutDuration));

        // 2. TELETRANSPORTE FÍSICO
        float targetRotY = target.eulerAngles.y;
        float headRotY = centerEyeCamera.transform.localEulerAngles.y;
        vrRig.eulerAngles = new Vector3(0, targetRotY - headRotY, 0);

        Vector3 offsetPos = centerEyeCamera.transform.position - vrRig.position;
        offsetPos.y = 0;
        vrRig.position = target.position - offsetPos;

        yield return new WaitForSeconds(0.2f);

        // 3. Volver a la luz
        yield return StartCoroutine(FadeToClear(fadeInDuration));
    }

    // =======================================================
    // NUEVAS FUNCIONES PÚBLICAS DE FUNDIDO (Para que las use SceneManager)
    // =======================================================

    public IEnumerator FadeToBlack(float duration)
    {
        if (fadeImage == null) yield break;

        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            float alpha = Mathf.Lerp(0f, 1f, timer / duration);
            fadeImage.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, alpha);
            yield return null;
        }
        fadeImage.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 1f);
    }

    public IEnumerator FadeToClear(float duration)
    {
        if (fadeImage == null) yield break;

        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, timer / duration);
            fadeImage.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, alpha);
            yield return null;
        }
        fadeImage.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 0f);
    }

    // =======================================================

    void CrearPantallaDeFundido()
    {
        if (centerEyeCamera == null) return;

        GameObject canvasObj = new GameObject("VR_Fade_Canvas");
        canvasObj.transform.SetParent(centerEyeCamera.transform, false);
        canvasObj.transform.localPosition = new Vector3(0, 0, 0.15f);
        canvasObj.transform.localRotation = Quaternion.identity;

        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 100;

        RectTransform rect = canvasObj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(4f, 4f);

        GameObject imageObj = new GameObject("Fade_Image");
        imageObj.transform.SetParent(canvasObj.transform, false);
        fadeImage = imageObj.AddComponent<Image>();
        fadeImage.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 0f);

        RectTransform imageRect = imageObj.GetComponent<RectTransform>();
        imageRect.anchorMin = Vector2.zero;
        imageRect.anchorMax = Vector2.one;
        imageRect.sizeDelta = Vector2.zero;
        imageRect.localPosition = Vector3.zero;
    }


    //  FUNCIÓN PÚBLICA: Permite forzar el viaje desde fuera ---
    public void ForzarTeletransporte(Transform destino)
    {
        if (destino != null)
        {
            // Reutilizamos tu Corrutina maestra que ya tiene el fundido a negro
            StartCoroutine(TeleportRoutine(destino));
        }
    }

}