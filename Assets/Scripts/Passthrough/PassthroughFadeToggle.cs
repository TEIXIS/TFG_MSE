using UnityEngine;
using System.Collections;

public class PassthroughFadeToggle : MonoBehaviour
{
    public OVRPassthroughLayer passthroughLayer;
    public float fadeDuration = 0.6f;

    private bool isPassthroughOn = false;
    private bool isFading = false;

    void Start()
    {
        // Aseguramos estado inicial
        passthroughLayer.hidden = true;
        passthroughLayer.textureOpacity = 0f;
    }

    void Update()
    {
        if (OVRInput.GetDown(OVRInput.Button.One) && !isFading)
        {
            StartCoroutine(FadePassthrough(!isPassthroughOn));
        }
    }

    IEnumerator FadePassthrough(bool turnOn)
    {
        isFading = true;

        if (turnOn)
        {
            passthroughLayer.hidden = false;
        }

        float start = passthroughLayer.textureOpacity;
        float end = turnOn ? 1f : 0f;
        float t = 0f;

        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            float normalized = t / fadeDuration;

            // Fade suave (mejor que Lerp lineal)
            passthroughLayer.textureOpacity =
                Mathf.SmoothStep(start, end, normalized);

            yield return null;
        }

        passthroughLayer.textureOpacity = end;

        if (!turnOn)
        {
            passthroughLayer.hidden = true;
        }

        isPassthroughOn = turnOn;
        isFading = false;
    }
}