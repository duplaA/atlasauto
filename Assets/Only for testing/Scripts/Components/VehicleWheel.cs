using UnityEngine;

/// <summary>
/// Wrapper for WheelCollider that handles motor torque, braking, and steering.
/// </summary>
public class VehicleWheel : MonoBehaviour
{
    public WheelCollider wheelCollider;
    public Transform wheelVisual;
    public bool isSteer;
    public bool isMotor;
    public Vector3 visualRotationOffset = Vector3.zero;

    private Vector3 worldPos;
    private Quaternion worldRot;

    void Start()
    {
        if (wheelCollider == null)
            wheelCollider = GetComponent<WheelCollider>();
    }

    /// <summary>
    /// Sync the visual wheel mesh with the physics wheel.
    /// </summary>
    public void SyncVisuals()
    {
        if (wheelCollider == null || wheelVisual == null) return;

        wheelCollider.GetWorldPose(out worldPos, out worldRot);
        wheelVisual.position = worldPos;
        wheelVisual.rotation = worldRot * Quaternion.Euler(visualRotationOffset);
    }

    /// <summary>
    /// Apply motor torque to the wheel.
    /// </summary>
    public void ApplyTorque(float motorTorque)
    {
        if (wheelCollider == null) return;
        wheelCollider.motorTorque = motorTorque;
    }

    /// <summary>
    /// Apply brake torque to the wheel.
    /// </summary>
    public void ApplyBrake(float brakeTorque)
    {
        if (wheelCollider == null) return;
        wheelCollider.brakeTorque = brakeTorque;
    }

    /// <summary>
    /// Apply steering angle to the wheel.
    /// </summary>
    public void ApplySteer(float angle)
    {
        if (wheelCollider == null) return;
        wheelCollider.steerAngle = angle;
    }

    /// <summary>
    /// Get the current wheel RPM.
    /// </summary>
    public float GetRPM()
    {
        if (wheelCollider == null) return 0f;
        return wheelCollider.rpm;
    }

    /// <summary>
    /// Check if the wheel is touching the ground.
    /// </summary>
    public bool IsGrounded(out WheelHit hit)
    {
        if (wheelCollider == null)
        {
            hit = new WheelHit();
            return false;
        }
        return wheelCollider.GetGroundHit(out hit);
    }

    /// <summary>
    /// Get the wheel radius.
    /// </summary>
    public float GetRadius()
    {
        if (wheelCollider == null) return 0.3f;
        return wheelCollider.radius;
    }
}
