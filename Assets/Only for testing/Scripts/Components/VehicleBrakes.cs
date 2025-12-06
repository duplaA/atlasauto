using UnityEngine;

public class VehicleBrakes : MonoBehaviour
{
    public float maxBrakeTorque = 4000f;

    public float GetBrakeTorque(float input, bool isMovingForward, float velocityZ)
    {
        if (Mathf.Abs(input) < 0.01f)
        {
            return maxBrakeTorque * 0.3f; // Rolling resistance / auto-brake
        }
        
        bool brakingForward = velocityZ > 0.5f && input < -0.1f;
        bool brakingReverse = velocityZ < -0.5f && input > 0.1f;

        if (brakingForward || brakingReverse)
        {
            return maxBrakeTorque;
        }

        return 0f;
    }
}
