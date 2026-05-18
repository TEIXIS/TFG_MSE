using UnityEngine;

public class BillboardY : MonoBehaviour
{
    private Camera mainCamera;

    void Start()
    {
        // Buscamos la cámara principal (en VR suele ser la cámara de los ojos)
        mainCamera = Camera.main;
    }

    void LateUpdate()
    {
        if (mainCamera == null) return;

        // 1. Miramos a la cámara
        transform.LookAt(mainCamera.transform);

        // 2. Anulamos la rotación en X y Z para que solo gire en el eje Y (como un cilindro)
        Vector3 rotacionBloqueada = transform.eulerAngles;
        rotacionBloqueada.x = 0; // Evita cabeceo
        rotacionBloqueada.z = 0; // Evita alabeo

        transform.eulerAngles = rotacionBloqueada;
    }
}
