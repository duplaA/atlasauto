using UnityEngine;

/// <summary>
/// Anti-roll bar simulation to prevent excessive body roll and rollovers during cornering.
/// Attach to the vehicle root alongside VehicleController.
/// </summary>
public class AntiRollBar : MonoBehaviour
{
    [Header("Axle Configuration")]
    [Tooltip("Left wheel collider of the axle")]
    public WheelCollider wheelL;
    [Tooltip("Right wheel collider of the axle")]
    public WheelCollider wheelR;

    [Header("Anti-Roll Settings")]
    [Tooltip("Anti-roll force strength. Higher = less body roll.")]
    [Range(0f, 50000f)]
    public float antiRollForce = 15000f;
    [Tooltip("FH5: scale ARB by 1 + weightShiftPercent * 0.5.")]
    [Range(0f, 1f)] public float weightShiftARBScale = 0.5f;

    private Rigidbody rb;
    private VehicleController vc;

    void Start()
    {
        rb = GetComponentInParent<Rigidbody>();
        vc = GetComponentInParent<VehicleController>();
        if (rb == null)
        {
            Debug.LogError("[AntiRollBar] No Rigidbody found in parent hierarchy.");
        }
    }

    void FixedUpdate()
    {
        if (wheelL == null || wheelR == null || rb == null) return;

        ApplyAntiRoll();
    }

    void ApplyAntiRoll()
    {
        float travelL = GetWheelTravel(wheelL);
        float travelR = GetWheelTravel(wheelR);
        float weightShiftPercent = (vc != null) ? vc.WeightShiftPercent : 0f;
        float effectiveARB = antiRollForce * (1f + weightShiftPercent * weightShiftARBScale);
        float antiRollForceMagnitude = (travelL - travelR) * effectiveARB;

        // Apply forces at wheel positions
        if (wheelL.isGrounded)
        {
            rb.AddForceAtPosition(wheelL.transform.up * -antiRollForceMagnitude, wheelL.transform.position);
        }
        if (wheelR.isGrounded)
        {
            rb.AddForceAtPosition(wheelR.transform.up * antiRollForceMagnitude, wheelR.transform.position);
        }
    }

    /// <summary>
    /// Returns normalized suspension travel (0 = fully compressed, 1 = fully extended).
    /// </summary>
    float GetWheelTravel(WheelCollider wc)
    {
        WheelHit hit;
        bool grounded = wc.GetGroundHit(out hit);

        if (grounded)
        {
            // Calculate how compressed the suspension is
            float fullTravel = wc.suspensionDistance;
            float currentCompression = (-wc.transform.InverseTransformPoint(hit.point).y - wc.radius) / fullTravel;
            return Mathf.Clamp01(currentCompression);
        }
        else
        {
            return 1f; // Fully extended when not grounded
        }
    }
}
