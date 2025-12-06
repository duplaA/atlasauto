using UnityEngine;

public class VehicleTyres : MonoBehaviour
{
    [Header("Friction Settings")]
    public float gripMultiplier = 1.0f;
    public float stiffness = 1.0f;

    public void Configure(float mu, float loadSens, float alpha, float camber, float grip,
                          float stiff, float fwdExSlip, float fwdExVal, float fwdAsSlip, float fwdAsVal,
                          float sideExSlip, float sideExVal, float sideAsSlip, float sideAsVal)
    {
        gripMultiplier = grip;
        stiffness = stiff;
    }

    public void UpdateFriction(VehicleWheel[] wheels, float nominalFz)
    {
        if (wheels == null) return;
        
        foreach (var wheel in wheels)
        {
            if (wheel == null || wheel.wheelCollider == null) continue;

            // Simple friction setup - just ensure reasonable defaults
            WheelFrictionCurve fwdCurve = wheel.wheelCollider.forwardFriction;
            fwdCurve.extremumSlip = 0.4f;
            fwdCurve.extremumValue = 1.0f * gripMultiplier;
            fwdCurve.asymptoteSlip = 0.8f;
            fwdCurve.asymptoteValue = 0.6f * gripMultiplier;
            fwdCurve.stiffness = stiffness;
            wheel.wheelCollider.forwardFriction = fwdCurve;

            WheelFrictionCurve sideCurve = wheel.wheelCollider.sidewaysFriction;
            sideCurve.extremumSlip = 0.2f;
            sideCurve.extremumValue = 1.0f * gripMultiplier;
            sideCurve.asymptoteSlip = 0.5f;
            sideCurve.asymptoteValue = 0.7f * gripMultiplier;
            sideCurve.stiffness = stiffness;
            wheel.wheelCollider.sidewaysFriction = sideCurve;
        }
    }
}
