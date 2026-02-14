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
    
    public enum RollIntensityPreset { Soft, Medium, Stiff }
    
    [Header("Roll Intensity")]
    [Tooltip("Preset roll intensity (Soft = more roll, Stiff = less roll).")]
    public RollIntensityPreset rollIntensityPreset = RollIntensityPreset.Medium;
    [Tooltip("Manual roll intensity multiplier (0.3-1.0). Lower = more pronounced body roll. Overrides preset if not Medium.")]
    [Range(0.3f, 1f)] public float rollIntensityMultiplier = 1f;
    [Tooltip("Use preset instead of manual multiplier.")]
    public bool usePreset = true;

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
        
        // Calculate roll intensity multiplier
        float intensityMult = 1f;
        if (usePreset)
        {
            switch (rollIntensityPreset)
            {
                case RollIntensityPreset.Soft:
                    intensityMult = 0.4f; // More roll
                    break;
                case RollIntensityPreset.Medium:
                    intensityMult = 0.7f; // Balanced
                    break;
                case RollIntensityPreset.Stiff:
                    intensityMult = 1f; // Less roll
                    break;
            }
        }
        else
        {
            intensityMult = rollIntensityMultiplier;
        }
        
        float effectiveARB = antiRollForce * (1f + weightShiftPercent * weightShiftARBScale) * intensityMult;
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
            // Calculate how compressed the suspension is (0 = Full Compression, 1 = Full Extension)
            // Note: We avoid clamping strictly to 0-1 to allow for transient physics spikes to be handled by the force
            float fullTravel = wc.suspensionDistance;
            float currentExtension = (-wc.transform.InverseTransformPoint(hit.point).y - wc.radius) / fullTravel;
            return currentExtension;
        }
        else
        {
            return 1.0f; // Fully extended when not grounded
        }
    }
}
