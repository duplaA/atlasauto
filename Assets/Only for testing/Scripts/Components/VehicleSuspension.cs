using UnityEngine;

public class VehicleSuspension : MonoBehaviour
{
    public enum SpringFrequency { Sport, Comfort }
    public enum DampRatio { Sport, Comfort }
    public enum FrontRearBias { FortySixty, SixtyForty, FiftyFifty }

    [Header("Settings")]
    [Tooltip("FH5: 0.15-0.25 m typical.")]
    public float suspensionDistance = 0.2f;
    public SpringFrequency frequency = SpringFrequency.Comfort;
    public DampRatio damping = DampRatio.Comfort;
    public FrontRearBias bias = FrontRearBias.SixtyForty;
    [Tooltip("FH5: spring rate delta per 1% weight shift (N/m).")]
    public float weightShiftSpringDeltaNpmPerPercent = 500f;
    [Tooltip("Bump = rebound * (2/3); Unity single damper uses average.")]
    [Range(0.5f, 1f)] public float bumpScale = 2f / 3f;
    
    [Header("Squat/Dive Dynamics")]
    [Tooltip("Strength of acceleration-based squat/dive (0-1). Higher = more pronounced suspension compression.")]
    [Range(0f, 1f)] public float squatDiveStrength = 0.6f;
    [Tooltip("Target position delta for rear wheels under acceleration (0.35-0.45 = more compressed).")]
    [Range(0.3f, 0.5f)] public float rearSquatTargetPosition = 0.4f;
    [Tooltip("Target position delta for front wheels under braking (0.35-0.45 = more compressed).")]
    [Range(0.3f, 0.5f)] public float frontDiveTargetPosition = 0.4f;
    [Tooltip("Damper multiplier for faster compression/rebound response.")]
    [Range(0.8f, 1.5f)] public float damperResponseMultiplier = 1.1f;
    
    private float massFront = 420f;
    private float massRear = 280f;

    public void Configure(float mass, float dist, SpringFrequency freq, DampRatio damp, FrontRearBias weightBias)
    {
        suspensionDistance = dist;
        frequency = freq;
        damping = damp;
        bias = weightBias;
        CalculateMassDistribution(mass);
    }

    private void CalculateMassDistribution(float totalMass)
    {
        switch (bias)
        {
            case FrontRearBias.FortySixty:
                massFront = totalMass * 0.4f / 2f;
                massRear = totalMass * 0.6f / 2f;
                break;
            case FrontRearBias.SixtyForty:
                massFront = totalMass * 0.6f / 2f;
                massRear = totalMass * 0.4f / 2f;
                break;
            case FrontRearBias.FiftyFifty:
                massFront = totalMass * 0.5f / 2f;
                massRear = totalMass * 0.5f / 2f;
                break;
        }
    }

    public void UpdateSuspension(VehicleWheel[] wheels)
    {
        UpdateSuspension(wheels, 0f);
    }

    public void UpdateSuspension(VehicleWheel[] wheels, float weightShiftPercent)
    {
        UpdateSuspension(wheels, weightShiftPercent, 0f);
    }

    public void UpdateSuspension(VehicleWheel[] wheels, float weightShiftPercent, float longitudinalG)
    {
        if (wheels == null || wheels.Length == 0) return;
        float dist = Mathf.Clamp(suspensionDistance, 0.15f, 0.25f);
        
        // Calculate acceleration-based target position adjustments
        float rearTargetAdjust = 0f;
        float frontTargetAdjust = 0f;
        
        if (squatDiveStrength > 0f)
        {
            // Positive G (acceleration): rear compresses (lower targetPosition), front extends slightly
            if (longitudinalG > 0.1f)
            {
                float accelFactor = Mathf.Clamp01(longitudinalG / 1.5f);
                rearTargetAdjust = -(0.5f - rearSquatTargetPosition) * accelFactor * squatDiveStrength;
                frontTargetAdjust = 0.05f * accelFactor * squatDiveStrength;
            }
            // Negative G (braking): front compresses (lower targetPosition), rear extends slightly
            else if (longitudinalG < -0.1f)
            {
                float brakeFactor = Mathf.Clamp01(Mathf.Abs(longitudinalG) / 1.5f);
                frontTargetAdjust = -(0.5f - frontDiveTargetPosition) * brakeFactor * squatDiveStrength;
                rearTargetAdjust = 0.05f * brakeFactor * squatDiveStrength;
            }
        }
        
        Transform vehicleTransform = wheels[0].transform.root;
        Vector3 vehicleForward = vehicleTransform.forward;
        Vector3 vehicleCenter = vehicleTransform.position;
        
        foreach (var wheel in wheels)
        {
            if (wheel == null || wheel.wheelCollider == null) continue;

            JointSpring spring = wheel.wheelCollider.suspensionSpring;

            // Front = wheel in front of vehicle center (works for +Z or -Z forward)
            Vector3 toWheel = (wheel.wheelCollider.transform.position - vehicleCenter);
            bool isFront = Vector3.Dot(toWheel, vehicleForward) > 0f;
            float springMass = isFront ? massFront : massRear;
            springMass = Mathf.Max(springMass, 200f);
            
            float freqVal = (frequency == SpringFrequency.Sport)
                ? (isFront ? 2.3f : 1.9f)
                : (isFront ? 1.8f : 1.5f);

            float k = springMass * Mathf.Pow(2 * Mathf.PI * freqVal, 2);
            k += weightShiftPercent * weightShiftSpringDeltaNpmPerPercent;
            k = Mathf.Max(k, 1000f);
            
            float dampRatioVal = (damping == DampRatio.Sport) ? 0.35f : 0.25f;
            float cRebound = 2f * dampRatioVal * Mathf.Sqrt(k * springMass);
            float c = cRebound * (1f + bumpScale) * 0.5f * damperResponseMultiplier; // Faster response

            spring.spring = k;
            spring.damper = c;
            
            // Apply acceleration-based target position
            float baseTarget = 0.5f;
            float targetAdjust = isFront ? frontTargetAdjust : rearTargetAdjust;
            spring.targetPosition = Mathf.Clamp01(baseTarget + targetAdjust);

            wheel.wheelCollider.suspensionSpring = spring;
            wheel.wheelCollider.suspensionDistance = dist;
            wheel.wheelCollider.wheelDampingRate = 0.5f;
        }
    }
    
    // Stub for compatibility - does nothing in simple version
    public void ApplyAntiRoll(Rigidbody rb) { }
}
