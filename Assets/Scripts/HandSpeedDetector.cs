using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Management;

public class HandSpeedDetector : MonoBehaviour
{
    [Header("Particles")]
    public ParticleSystem leftParticles;
    public ParticleSystem rightParticles;

    [Header("Tracking Settings")]
    [Tooltip("El punto de la mano que se usará para calcular la velocidad. MiddleTip es ideal para los dedos.")]
    public XRHandJointID trackedJoint = XRHandJointID.MiddleTip;

    [Header("Speed")]
    public float speedThreshold = 1.5f;   // m/s rápido (puede que necesites subirlo un poco al usar los dedos)
    public float stopThreshold = 0.8f;    // histéresis

    [Header("Smoothing")]
    [Tooltip("Qué tan rápido se adapta la velocidad. Un valor menor filtra más los temblores/ruido, pero reacciona un poco más lento.")]
    public float smoothingFactor = 10f;

    XRHandSubsystem handSubsystem;

    Vector3 lastLeftPos;
    Vector3 lastRightPos;

    // Variables para almacenar la velocidad suavizada de cada mano
    float leftSmoothedSpeed;
    float rightSmoothedSpeed;

    bool leftInit;
    bool rightInit;

    bool leftActive;
    bool rightActive;

    void Start()
    {
        TryGetSubsystem();

        if (leftParticles) leftParticles.Stop();
        if (rightParticles) rightParticles.Stop();
    }

    void TryGetSubsystem()
    {
        var loader = XRGeneralSettings.Instance?.Manager?.activeLoader;
        if (loader != null)
            handSubsystem = loader.GetLoadedSubsystem<XRHandSubsystem>();
    }

    void Update()
    {
        if (handSubsystem == null || !handSubsystem.running)
        {
            TryGetSubsystem();
            return;
        }

        UpdateHand(handSubsystem.leftHand, ref lastLeftPos, ref leftInit, ref leftActive, ref leftSmoothedSpeed, leftParticles);
        UpdateHand(handSubsystem.rightHand, ref lastRightPos, ref rightInit, ref rightActive, ref rightSmoothedSpeed, rightParticles);
    }

    void UpdateHand(XRHand hand,
                    ref Vector3 lastPos,
                    ref bool init,
                    ref bool active,
                    ref float smoothedSpeed,
                    ParticleSystem ps)
    {
        if (!hand.isTracked)
        {
            if (ps && active) ps.Stop();
            active = false;
            init = false;
            smoothedSpeed = 0f; // Reseteamos la velocidad si se pierde el tracking
            return;
        }

        var targetJoint = hand.GetJoint(trackedJoint);

        if (!targetJoint.TryGetPose(out Pose pose))
            return;

        if (!init)
        {
            lastPos = pose.position;
            init = true;
            return;
        }

        // 1. Calculamos la velocidad "cruda" o bruta
        float rawSpeed = Vector3.Distance(pose.position, lastPos) / Time.deltaTime;

        // 2. Aplicamos el suavizado
        smoothedSpeed = Mathf.Lerp(smoothedSpeed, rawSpeed, smoothingFactor * Time.deltaTime);

        // 3. Evaluamos la activación usando la velocidad suavizada (histéresis)
        if (active)
        {
            if (smoothedSpeed < stopThreshold)
            {
                active = false;
                if (ps) ps.Stop();
            }
        }
        else
        {
            if (smoothedSpeed > speedThreshold)
            {
                active = true;
                if (ps) ps.Play();
            }
        }

        lastPos = pose.position;
    }
}