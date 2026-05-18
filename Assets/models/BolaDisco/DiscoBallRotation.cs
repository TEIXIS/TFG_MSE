using UnityEngine;

public class DiscoBallRotation : MonoBehaviour
{
    public Vector3 rotationSpeed = new Vector3(0, 50, 0);

    void Update()
    {
        // En VR, es mejor usar Space.Self para evitar problemas de orientación
        transform.Rotate(rotationSpeed * Time.deltaTime, Space.Self);
    }
}