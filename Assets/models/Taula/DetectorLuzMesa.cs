using UnityEngine;

public class DetectorLuzMesa : MonoBehaviour
{
    private Material miMaterial;

    void Start()
    {
        // Al acceder a .material, Unity crea una copia única de este material 
        // solo para este objeto. Así no apagamos todos los bloques a la vez.
        Renderer ren = GetComponent<Renderer>();
        if (ren != null)
        {
            miMaterial = ren.material;
            // Empezamos asumiendo que el objeto nace fuera de la mesa (apagado)
            miMaterial.SetFloat("_EnZonaLuz", 0f);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Si tocamos el cilindro invisible...
        if (other.CompareTag("ZonaLuz"))
        {
            if (miMaterial != null) miMaterial.SetFloat("_EnZonaLuz", 1f); // Encendemos luz local
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // Si salimos del cilindro invisible...
        if (other.CompareTag("ZonaLuz"))
        {
            if (miMaterial != null) miMaterial.SetFloat("_EnZonaLuz", 0f); // Apagamos luz local
        }
    }
}