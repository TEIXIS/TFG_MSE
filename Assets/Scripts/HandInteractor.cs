using UnityEngine;

public class HandInteractor : MonoBehaviour
{
    FanOption current;
    float currentHover;
    float currentPress;

    Vector3 lastHandPos;
    float pressTimer = 0f;

    private Vector3 handAimAxis = Vector3.forward;
    float smoothedHandSpeed = 0f;

    public static HandInteractor activeHand = null;

    // NUEVO: Referencia al collider para saber dónde está exactamente tu palma/dedos desplazados
    private Collider handCollider;

    void Start()
    {
        // Cogemos el SphereCollider que tú configuraste con tanto mimo
        handCollider = GetComponent<Collider>();
    }

    void OnTriggerEnter(Collider other)
    {
        var opt = other.GetComponent<FanOption>();
        if (opt)
        {
            if (current != null && current != opt) ReleaseButton();
            current = opt;
        }
    }

    void OnTriggerExit(Collider other)
    {
        var opt = other.GetComponent<FanOption>();
        if (opt)
        {
            opt.SetHover(0);
            opt.SetPress(0);
            if (opt == current) ReleaseButton();
        }
    }

    void ReleaseButton()
    {
        if (current != null)
        {
            current.SetHover(0);
            current.SetPress(0);
        }
        currentHover = 0;
        currentPress = 0;
        pressTimer = 0f;
        current = null;

        if (activeHand == this) activeHand = null;
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
            if (activeHand == this) activeHand = null;
            return;
        }

        if (activeHand != null && activeHand != this)
        {
            ReleaseButton();
            return;
        }

        Vector3 buttonAnchorPos = current.GetBaseWorldPosition();

        // LA CORRECCIÓN: Medimos la distancia desde el centro del Collider (palma/dedos), NO desde la muñeca
        float dist = Vector3.Distance(interactionPoint, buttonAnchorPos);

        if (dist > 0.40f)
        {
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
    }
}


