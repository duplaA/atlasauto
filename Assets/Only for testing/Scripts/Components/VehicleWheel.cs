using UnityEngine;

public class VehicleWheel : MonoBehaviour
{
    public WheelCollider wheelCollider;
    public Transform wheelVisual;
    public bool isSteer;
    public bool isMotor;

    public void SyncVisuals() {
        if (wheelCollider == null || wheelVisual == null) return;
        Vector3 pos; Quaternion rot;
        wheelCollider.GetWorldPose(out pos, out rot);
        wheelVisual.position = pos;
        wheelVisual.rotation = rot;
    }

    public void ApplyTorque(float t) { if(wheelCollider) wheelCollider.motorTorque = t; }
    public void ApplyBrake(float b) { if(wheelCollider) wheelCollider.brakeTorque = b; }
    public void ApplySteer(float a) { if(wheelCollider) wheelCollider.steerAngle = a; }
    public float GetRPM() { return wheelCollider ? wheelCollider.rpm : 0; }
}