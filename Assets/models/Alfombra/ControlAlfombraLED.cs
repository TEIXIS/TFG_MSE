using UnityEngine;
// Afegim aquesta línia per utilitzar el nou sistema de controls de Unity
using UnityEngine.InputSystem;

public class ControlAlfombraLED : MonoBehaviour
{
    [Tooltip("Arrastra aquí el sistema de partículas desde el Inspector")]
    public ParticleSystem sistemaParticulas;

    void Start()
    {
        if (sistemaParticulas != null)
        {
            var main = sistemaParticulas.main;
            main.loop = true;
        }
        else
        {
            Debug.LogWarning("Falta asignar el Sistema de Partículas al script ControlAlfombraLED.");
        }
    }

    void Update()
    {
        // NOSTRE NOU CODI: Comprovem que hi hagi un teclat i si s'ha premut l'Espai
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            AlternarLEDs();
        }
    }

    public void AlternarLEDs()
    {
        if (sistemaParticulas == null) return;

        if (sistemaParticulas.isPlaying)
        {
            Apagar();
        }
        else
        {
            Encender();
        }
    }

    public void Encender()
    {
        if (sistemaParticulas != null && !sistemaParticulas.isPlaying)
        {
            sistemaParticulas.Play();
        }
    }

    public void Apagar()
    {
        if (sistemaParticulas != null && sistemaParticulas.isPlaying)
        {
            sistemaParticulas.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }
}