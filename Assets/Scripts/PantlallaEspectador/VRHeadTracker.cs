using UnityEngine;
using System.Collections;
using System.Globalization;

public class VRHeadTracker : MonoBehaviour
{
    [Header("Red")]
    [Tooltip("Arrastra aquí el script Connection de tu SceneManager")]
    public Connection connectionServer;

    [Header("Ajustes de Transmisión")]
    [Tooltip("Envíos por segundo. 20-30 es ideal para que se vea fluido en la tablet sin saturar la red.")]
    public float enviosPorSegundo = 20f;

    // Lo mantenemos público para que la tablet pueda encenderlo y apagarlo a distancia
    public bool transmitiendo = false;

    private float tiempoEntreEnvios;

    void Start()
    {
        tiempoEntreEnvios = 1f / enviosPorSegundo;
        StartCoroutine(RutinaDeTransmision());
    }

    IEnumerator RutinaDeTransmision()
    {
        while (true)
        {
            if (transmitiendo && connectionServer != null && connectionServer.connected)
            {
                // Obtenemos la posición local o global (mejor global para la replicación exacta)
                Vector3 pos = transform.position;
                Quaternion rot = transform.rotation;

                // 1. Convertimos CADA número a texto obligando a usar el punto decimal (.)
                string px = pos.x.ToString("F3", CultureInfo.InvariantCulture);
                string py = pos.y.ToString("F3", CultureInfo.InvariantCulture);
                string pz = pos.z.ToString("F3", CultureInfo.InvariantCulture);

                string rx = rot.x.ToString("F3", CultureInfo.InvariantCulture);
                string ry = rot.y.ToString("F3", CultureInfo.InvariantCulture);
                string rz = rot.z.ToString("F3", CultureInfo.InvariantCulture);
                string rw = rot.w.ToString("F3", CultureInfo.InvariantCulture);


                // 2. Ahora sí montamos el mensaje de forma 100% segura
                string mensajeTracking = $"HEAD:{px},{py},{pz}|{rx},{ry},{rz},{rw}";
                // Lo inyectamos en la red
                connectionServer.Send(mensajeTracking);
            }

            // Pausamos la corrutina hasta el siguiente "tick"
            yield return new WaitForSeconds(tiempoEntreEnvios);
        }
    }
}