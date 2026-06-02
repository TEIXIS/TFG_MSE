using System;
using System.Collections;
using UnityEngine;

public class AmbientLightIntensityController : MonoBehaviour
{
    [Header("Niveles")]
    [Range(1, 4)] public int currentLevel = 2;
    public float[] intensityLevels = { 0.35f, 0.55f, 0.75f, 0.95f };

    [Header("Luces de la sala")]
    [Tooltip("Arrastra aqui la luz o luces ambiente de la sala. Si se deja vacio solo se modifica RenderSettings.")]
    public Light[] roomLights;
    public bool updateRenderSettings = true;
    public bool updateAmbientLightColor = true;
    public bool updateDynamicGI = false;
    public bool updateRoomLights = true;
    public bool autoFindRoomLights = true;

    [Header("Transicion")]
    [Min(0f)] public float transitionDuration = 0.6f;

    public event Action<int, int> LevelChanged;

    private Coroutine transitionCoroutine;
    private Color baseAmbientLight;

    public int TotalLevels
    {
        get
        {
            if (intensityLevels == null || intensityLevels.Length == 0)
                return 1;

            return intensityLevels.Length;
        }
    }

    private void Start()
    {
        baseAmbientLight = RenderSettings.ambientLight.maxColorComponent > 0f
            ? RenderSettings.ambientLight
            : Color.white;

        FindRoomLightsIfNeeded();
        SetLevel(currentLevel);
    }

    public void IncreaseLevel()
    {
        SetLevel(currentLevel + 1);
    }

    public void DecreaseLevel()
    {
        SetLevel(currentLevel - 1);
    }

    public void SetLevel(int level)
    {
        int totalLevels = TotalLevels;
        currentLevel = Mathf.Clamp(level, 1, totalLevels);
        float intensity = intensityLevels != null && intensityLevels.Length > 0
            ? intensityLevels[currentLevel - 1]
            : 1f;

        if (transitionCoroutine != null)
            StopCoroutine(transitionCoroutine);

        if (transitionDuration <= 0f)
        {
            ApplyIntensity(intensity);
        }
        else
        {
            transitionCoroutine = StartCoroutine(TransitionIntensity(intensity));
        }

        LevelChanged?.Invoke(currentLevel, totalLevels);
    }

    private IEnumerator TransitionIntensity(float targetIntensity)
    {
        FindRoomLightsIfNeeded();

        float startAmbientIntensity = RenderSettings.ambientIntensity;
        float[] startLightIntensities = null;

        if (updateRoomLights && roomLights != null)
        {
            startLightIntensities = new float[roomLights.Length];
            for (int i = 0; i < roomLights.Length; i++)
            {
                Light roomLight = roomLights[i];
                startLightIntensities[i] = roomLight != null ? roomLight.intensity : targetIntensity;
            }
        }

        float elapsed = 0f;
        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / transitionDuration);
            float easedT = Mathf.SmoothStep(0f, 1f, t);

            if (updateRenderSettings)
                RenderSettings.ambientIntensity = Mathf.Lerp(startAmbientIntensity, targetIntensity, easedT);

            if (updateAmbientLightColor)
                RenderSettings.ambientLight = baseAmbientLight * Mathf.Lerp(startAmbientIntensity, targetIntensity, easedT);

            if (updateRoomLights && roomLights != null && startLightIntensities != null)
            {
                for (int i = 0; i < roomLights.Length; i++)
                {
                    Light roomLight = roomLights[i];
                    if (roomLight != null)
                        roomLight.intensity = Mathf.Lerp(startLightIntensities[i], targetIntensity, easedT);
                }
            }

            yield return null;
        }

        ApplyIntensity(targetIntensity);
        transitionCoroutine = null;
    }

    private void ApplyIntensity(float intensity)
    {
        FindRoomLightsIfNeeded();

        if (updateRenderSettings)
            RenderSettings.ambientIntensity = intensity;

        if (updateAmbientLightColor)
            RenderSettings.ambientLight = baseAmbientLight * intensity;

        if (updateDynamicGI)
            DynamicGI.UpdateEnvironment();

        if (updateRoomLights && roomLights != null)
        {
            foreach (Light roomLight in roomLights)
            {
                if (roomLight != null)
                    roomLight.intensity = intensity;
            }
        }

        Debug.Log("[LUZ] Intensidad ambiente aplicada: " + intensity
            + " | RenderSettings.ambientIntensity=" + RenderSettings.ambientIntensity
            + " | RenderSettings.ambientLight=" + RenderSettings.ambientLight);
    }

    private void FindRoomLightsIfNeeded()
    {
        if (!autoFindRoomLights || !updateRoomLights || (roomLights != null && roomLights.Length > 0))
            return;

        roomLights = FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    }
}
