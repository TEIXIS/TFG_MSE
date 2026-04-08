using UnityEngine;

public class TogglePassthrough : MonoBehaviour
{
    [Header("Passthrough")]
    public OVRPassthroughLayer passthroughLayer;

    [Header("Objetos a ocultar con Passthrough")]
    public GameObject[] planes;

    [Header("Skybox")]
    public Material skyboxNormal;   // Skybox cuando passthrough está OFF
    public Material skyboxPassthrough; // Opcional (puede ser null)

    private bool isPassthroughOn = false;


    void Update()
    {
        if (OVRInput.GetDown(OVRInput.Button.One)) // Botón A
        {
            TogglePassthroughState();
        }
    }

    void TogglePassthroughState()
    {
        isPassthroughOn = !isPassthroughOn;

        // Activar / desactivar Passthrough
        passthroughLayer.hidden = !isPassthroughOn;

        // Activar / desactivar planos
        foreach (GameObject plane in planes)
        {
            if (plane != null)
                plane.SetActive(!isPassthroughOn);
        }

        // Cambiar skybox
        if (isPassthroughOn)
        {
            if (skyboxPassthrough != null)
                RenderSettings.skybox = skyboxPassthrough;
        }
        else
        {
            if (skyboxNormal != null)
                RenderSettings.skybox = skyboxNormal;
        }

        // Forzar actualización del skybox
        DynamicGI.UpdateEnvironment();
    }
}