using UnityEngine;
using System.Collections.Generic;

public class HandInteractor : MonoBehaviour
{
    FanOption current;
    readonly Dictionary<FanOption, int> optionContacts = new Dictionary<FanOption, int>();
    float currentHover;
    float currentPress;

    Vector3 lastHandPos;
    float pressTimer = 0f;

    private Vector3 handAimAxis = Vector3.forward;
    float smoothedHandSpeed = 0f;

    public static HandInteractor activeHand = null;

    // NUEVO: Referencia al collider para saber dónde está exactamente tu palma/dedos desplazados
    private Collider handCollider;
    private float nextDebugLogTime;

    void Start()
    {
        // Cogemos el SphereCollider que tú configuraste con tanto mimo
        handCollider = GetComponent<Collider>();
    }

    void OnTriggerEnter(Collider other)
    {
        var opt = other.GetComponentInParent<FanOption>();
        if (opt)
        {
            if (!optionContacts.ContainsKey(opt))
                optionContacts[opt] = 0;
            optionContacts[opt]++;

            Debug.Log($"[FanMenu][HandInteractor] ENTER hand={name} option={opt.name} other={other.name} contacts={optionContacts[opt]}");

            if (current != null && current != opt) ReleaseButton();
            current = opt;
        }
    }

    void OnTriggerExit(Collider other)
    {
        var opt = other.GetComponentInParent<FanOption>();
        if (opt)
        {
            if (optionContacts.ContainsKey(opt))
            {
                optionContacts[opt] = Mathf.Max(0, optionContacts[opt] - 1);
                Debug.Log($"[FanMenu][HandInteractor] EXIT hand={name} option={opt.name} other={other.name} contacts={optionContacts[opt]}");

                if (optionContacts[opt] > 0)
                    return;

                optionContacts.Remove(opt);
            }

            if (opt == current)
                ReleaseButton();
        }
    }

    void ReleaseButton()
    {
        if (current != null)
        {
            current.SetHover(0);
            current.SetPress(0);
            optionContacts.Remove(current);
        }
        currentHover = 0;
        currentPress = 0;
        pressTimer = 0f;
        current = null;

        if (activeHand == this) activeHand = null;
    }

    void TryAcquireNearbyOption(Vector3 interactionPoint)
    {
        float radius = 0.08f;
        if (handCollider is SphereCollider sphere)
            radius = Mathf.Max(radius, sphere.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z));

        Collider[] hits = Physics.OverlapSphere(interactionPoint, radius, ~0, QueryTriggerInteraction.Collide);
        foreach (Collider hit in hits)
        {
            FanOption opt = hit.GetComponentInParent<FanOption>();
            if (opt == null) continue;

            if (!optionContacts.ContainsKey(opt))
                optionContacts[opt] = 1;

            current = opt;
            Debug.Log($"[FanMenu][HandInteractor] ACQUIRE_OVERLAP hand={name} option={opt.name} hit={hit.name} radius={radius:F3}");
            return;
        }
    }

    void Update()
    {
        // LA MAGIA: Calculamos el punto exacto de interacción basado en tu SphereCollider desplazado.
        // Si por algún motivo no hay collider, usa la muñeca como plan B.
        Vector3 interactionPoint = handCollider != null ? handCollider.bounds.center : transform.position;

        float rawHandSpeed = 0f;
        if (Time.deltaTime > 0)
        {
            // Usamos el interactionPoint para saber a qué velocidad se mueven los dedos
            rawHandSpeed = Vector3.Distance(interactionPoint, lastHandPos) / Time.deltaTime;
        }
        lastHandPos = interactionPoint;
        smoothedHandSpeed = Mathf.Lerp(smoothedHandSpeed, rawHandSpeed, Time.deltaTime * 5f);

        if (!current)
        {
            TryAcquireNearbyOption(interactionPoint);
            if (!current)
            {
                if (activeHand == this) activeHand = null;
                return;
            }
        }

        if (activeHand != null && activeHand != this)
        {
            ReleaseButton();
            return;
        }

        Vector3 buttonAnchorPos = current.GetClosestInteractionPoint(interactionPoint);

        // Medimos contra el punto tocable mas cercano, no contra el centro del modelo.
        float dist = current.GetInteractionDistance(interactionPoint);

        if (dist > 0.40f)
        {
            Debug.Log($"[FanMenu][HandInteractor] RELEASE_DISTANCE hand={name} option={current.name} dist={dist:F3} interaction={interactionPoint} anchor={buttonAnchorPos}");
            ReleaseButton();
            return;
        }

        // Le pasamos al botón la posición de los dedos para que sea atraído hacia ellos
        current.SetHandPosition(interactionPoint);

        float targetHover = 0f;
        float targetPress = 0f;

        if (smoothedHandSpeed > 0.35f)
        {
            targetHover = -1f;
            pressTimer = Mathf.Max(0f, pressTimer - Time.deltaTime * 2f);
        }
        else
        {
            Vector3 handAimDir = transform.TransformDirection(handAimAxis);
            // Calculamos la dirección usando el nuevo punto
            Vector3 dirToButton = (buttonAnchorPos - interactionPoint).normalized;

            float aimDot = Vector3.Dot(handAimDir, dirToButton);
            float aimMultiplier = Mathf.InverseLerp(0.6f, 0.9f, aimDot);

            float baseHover = Mathf.InverseLerp(0.35f, 0.05f, dist);

            if (dist < 0.12f)
            {
                aimMultiplier = 1f;
            }

            targetHover = baseHover * aimMultiplier;

            if (dist < 0.15f)
            {
                pressTimer += Time.deltaTime;
                targetPress = Mathf.Clamp01(pressTimer / 1.5f);
            }
            else if (dist > 0.18f)
            {
                pressTimer = Mathf.Max(0f, pressTimer - Time.deltaTime * 2f);
                targetPress = Mathf.Clamp01(pressTimer / 1.5f);
            }
        }

        if (Mathf.Abs(targetHover) > 0.01f || dist < 0.15f)
        {
            activeHand = this;
        }
        else if (activeHand == this)
        {
            activeHand = null;
        }

        currentHover = Mathf.Lerp(currentHover, targetHover, Time.deltaTime * 8f);
        currentPress = Mathf.Lerp(currentPress, targetPress, Time.deltaTime * 12f);

        current.SetHover(currentHover);
        current.SetPress(currentPress);

        if (Time.time >= nextDebugLogTime && (dist < 0.22f || currentPress > 0.2f))
        {
            nextDebugLogTime = Time.time + 0.5f;
            Debug.Log($"[FanMenu][HandInteractor] TRACK hand={name} option={current.name} dist={dist:F3} hover={currentHover:F2} press={currentPress:F2} targetPress={targetPress:F2} speed={smoothedHandSpeed:F2} active={(activeHand != null ? activeHand.name : "null")}");
        }
    }
}


