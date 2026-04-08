using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Management;

public class XRHandsDebug : MonoBehaviour
{
    XRHandSubsystem hands;

    void Update()
    {
        if (hands == null)
        {
            var loader = XRGeneralSettings.Instance?.Manager?.activeLoader;
            hands = loader?.GetLoadedSubsystem<XRHandSubsystem>();
            return;
        }

        Debug.Log("Left tracked: " + hands.leftHand.isTracked +
                  " | Right tracked: " + hands.rightHand.isTracked);
    }
}