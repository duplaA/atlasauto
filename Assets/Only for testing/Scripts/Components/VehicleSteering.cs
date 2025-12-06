using UnityEngine;

public class VehicleSteering : MonoBehaviour
{
    public float maxSteerAngle = 30f;
    public float steerAngleAtMaxSpeed = 10f;
    public float maxSpeedForSteering = 35f;

    public float CalculateSteerAngle(float input, float currentSpeed)
    {
        float speedFactor = Mathf.InverseLerp(0, maxSpeedForSteering, Mathf.Abs(currentSpeed));
        float currentRange = Mathf.Lerp(maxSteerAngle, steerAngleAtMaxSpeed, speedFactor);
        return input * currentRange;
    }
    
    // Stub for compatibility with ApplyToWheels call
    public void ApplyToWheels(VehicleWheel[] wheels, float input, float speed)
    {
        float angle = CalculateSteerAngle(input, speed);
        foreach (var w in wheels)
        {
            if (w != null && w.isSteer)
                w.ApplySteer(angle);
        }
    }
}
