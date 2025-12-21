using UnityEngine;

public class VehicleBrakes : MonoBehaviour
{
    public float maxBrakeTorque = 6000f;

    public float GetBrakeTorque(float input, float velocityZ)
    {
        if (Mathf.Abs(input) < 0.01f) return maxBrakeTorque * 0.1f; // Motorfék

        bool brakingForward = velocityZ > 0.5f && input < -0.1f;
        bool brakingReverse = velocityZ < -0.5f && input > 0.1f;

        return (brakingForward || brakingReverse) ? maxBrakeTorque : 0f;
    }
}