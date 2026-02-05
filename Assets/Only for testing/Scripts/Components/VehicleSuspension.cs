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
        if (wheels == null) return;
        float dist = Mathf.Clamp(suspensionDistance, 0.15f, 0.25f);
        foreach (var wheel in wheels)
        {
            if (wheel == null || wheel.wheelCollider == null) continue;

            JointSpring spring = wheel.wheelCollider.suspensionSpring;

            bool isFront = wheel.transform.localPosition.z > 0;
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
            float c = cRebound * (1f + bumpScale) * 0.5f;

            spring.spring = k;
            spring.damper = c;
            spring.targetPosition = 0.5f;

            wheel.wheelCollider.suspensionSpring = spring;
            wheel.wheelCollider.suspensionDistance = dist;
            wheel.wheelCollider.wheelDampingRate = 0.5f;
        }
    }
    
    // Stub for compatibility - does nothing in simple version
    public void ApplyAntiRoll(Rigidbody rb) { }
}
