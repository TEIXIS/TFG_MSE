using TMPro;
using UnityEngine;

public class RoomLightIntensityControl : MonoBehaviour
{
    private const string CommandPrefix = "CMD_AMBIENT_LIGHT_LEVEL:";

    [Header("Controlador")]
    public AmbientLightIntensityController lightController;
    public Connection connectionServer;

    [Header("Mostrador")]
    public TMP_Text levelText;
    public Renderer[] levelIndicators;
    public Material indicatorOffMaterial;
    public Material indicatorOnMaterial;
    public Color indicatorOffColor = new Color(1f, 1f, 1f, 0.2f);
    public Color indicatorOnColor = Color.white;

    private MaterialPropertyBlock propertyBlock;
    private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorProperty = Shader.PropertyToID("_Color");

    private void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();
    }

    private void OnEnable()
    {
        if (lightController == null)
            lightController = FindFirstObjectByType<AmbientLightIntensityController>();

        if (connectionServer == null)
            connectionServer = FindFirstObjectByType<Connection>();

        if (lightController != null)
            lightController.LevelChanged += UpdateDisplay;
    }

    private void Start()
    {
        if (lightController != null)
            UpdateDisplay(lightController.currentLevel, lightController.TotalLevels);
    }

    private void OnDisable()
    {
        if (lightController != null)
            lightController.LevelChanged -= UpdateDisplay;
    }

    public void IncreaseLevel()
    {
        if (lightController == null)
            return;

        lightController.IncreaseLevel();
        SendLevelToTablet();
    }

    public void DecreaseLevel()
    {
        if (lightController == null)
            return;

        lightController.DecreaseLevel();
        SendLevelToTablet();
    }

    private void SendLevelToTablet()
    {
        if (connectionServer != null && connectionServer.connected && lightController != null)
            connectionServer.Send(CommandPrefix + lightController.currentLevel);
    }

    private void UpdateDisplay(int level, int totalLevels)
    {
        if (levelText != null)
            levelText.text = level + " / " + totalLevels;

        if (levelIndicators == null)
            return;

        for (int i = 0; i < levelIndicators.Length; i++)
        {
            Renderer indicator = levelIndicators[i];
            if (indicator == null)
                continue;

            bool active = i < level;
            if (indicatorOnMaterial != null && indicatorOffMaterial != null)
            {
                indicator.sharedMaterial = active ? indicatorOnMaterial : indicatorOffMaterial;
                continue;
            }

            indicator.GetPropertyBlock(propertyBlock);
            Material sharedMaterial = indicator.sharedMaterial;
            int colorProperty = sharedMaterial != null && sharedMaterial.HasProperty(BaseColorProperty)
                ? BaseColorProperty
                : ColorProperty;

            propertyBlock.SetColor(colorProperty, active ? indicatorOnColor : indicatorOffColor);
            indicator.SetPropertyBlock(propertyBlock);
        }
    }
}
