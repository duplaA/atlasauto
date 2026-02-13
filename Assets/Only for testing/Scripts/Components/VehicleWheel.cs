using UnityEngine;

/// <summary>
/// Wheel component that syncs physics with visuals and provides smooth steering.
/// </summary>
public class VehicleWheel : MonoBehaviour
{
    [Header("References")]
    public WheelCollider wheelCollider;
    public Transform wheelVisual;

    [Header("Wheel Type")]
    public bool isFront;
    public bool isSteer;
    public bool isMotor;

    [Header("Steering Animation")]
    [Tooltip("How fast the wheel turns to target angle (degrees/second)")]
    public float steerSpeed = 120f;
    
    [Header("Tire Deformation")]
    [Tooltip("Enable tire deformation (squash/stretch) under load and slip.")]
    public bool enableTireDeformation = true;
    [Tooltip("Maximum tire deformation scale (2-5% typical).")]
    [Range(0f, 0.1f)] public float maxDeformationScale = 0.03f;
    [Tooltip("Slip threshold for visible tire spin overspin (0.15 typical).")]
    [Range(0.05f, 0.3f)] public float slipOverspinThreshold = 0.15f;

    // Internal state
    private float currentSteerAngle = 0f;
    private float visualRotation = 0f;
    private Vector3 originalScale = Vector3.one;

    /// <summary>Normalized suspension deflection 0 (extended) to 1 (compressed). Updated in UpdateVisuals.</summary>
    public float SuspensionDeflection { get; private set; }
    
    /// <summary>Last forward slip value (for external access).</summary>
    public float LastForwardSlip { get; private set; }

    // Removed internal LateUpdate - VehicleController will drive this now for perfect sync
    
    void Awake()
    {
        // Auto-discover WheelCollider if not assigned
        if (wheelCollider == null)
        {
            // Try to find on this GameObject
            wheelCollider = GetComponent<WheelCollider>();
            
            // Try to find in children
            if (wheelCollider == null)
            {
                wheelCollider = GetComponentInChildren<WheelCollider>();
            }
            
            // Try to find in parent (if VehicleWheel script is on visual mesh)
            if (wheelCollider == null)
            {
                wheelCollider = GetComponentInParent<WheelCollider>();
            }
        }
        
        if (wheelVisual == null)
        {
            Debug.LogWarning($"[VehicleWheel] {gameObject.name}: No wheelVisual assigned. Visual sync will be skipped.");
        }
        else
        {
            // Store original scale for tire deformation
            originalScale = wheelVisual.localScale;
        }
    }

    /// Updates the visual wheel state (Steering and Spin)
    /// Called by VehicleController.
    /// <param name="targetSteer">Target steering angle in degrees</param>
    /// <param name="driveSpeedMS">Vehicle speed in m/s (controls spin speed)</param>
    /// <param name="wheelRadius">Radius to calculate spin from speed</param>
    /// <param name="forwardSlip">Forward slip ratio (positive = spinning faster than ground)</param>
    /// <param name="lateralSlip">Lateral slip angle (for tire deformation)</param>
    public void UpdateVisuals(float targetSteer, float driveSpeedMS, float wheelRadius, float forwardSlip = 0f, float lateralSlip = 0f)
    {
        if (wheelCollider == null || wheelVisual == null) return;

        // Store slip for external access
        LastForwardSlip = forwardSlip;

        // 1. Position from Physics (Suspension travel)
        Vector3 pos;
        Quaternion rot;
        wheelCollider.GetWorldPose(out pos, out rot);
        wheelVisual.position = pos;

        // Suspension deflection: rest hub (extended) vs current hub along suspension axis
        float dist = wheelCollider.suspensionDistance;
        if (dist > 0.001f)
        {
            Vector3 restHub = wheelCollider.transform.position + wheelCollider.transform.up * (dist * 0.5f);
            float deflection = Vector3.Dot(restHub - pos, wheelCollider.transform.up) / dist;
            SuspensionDeflection = Mathf.Clamp01(deflection);
        }

        // 2. Smooth Steering
        currentSteerAngle = Mathf.MoveTowards(currentSteerAngle, targetSteer, steerSpeed * Time.deltaTime);

        // 3. Spin with slip-based overspin
        float circumference = 2f * Mathf.PI * wheelRadius;
        float speedBasedRPM = (driveSpeedMS * 60f) / circumference;
        
        // Use actual wheel RPM when slip is high (overspin)
        float actualRPM = wheelCollider.rpm;
        float absForwardSlip = Mathf.Abs(forwardSlip);
        float slipBlend = 0f;
        
        if (absForwardSlip > slipOverspinThreshold)
        {
            // Blend to actual RPM when slipping (wheel spins faster than ground)
            slipBlend = Mathf.Clamp01((absForwardSlip - slipOverspinThreshold) / 0.2f); // Full blend at 0.35 slip
            float effectiveRPM = Mathf.Lerp(speedBasedRPM, actualRPM, slipBlend);
            speedBasedRPM = effectiveRPM;
        }
        
        // 1 RPM = 6 degrees/sec
        float spinDegreesPerSec = speedBasedRPM * 6f;
        
        visualRotation += spinDegreesPerSec * Time.deltaTime;

        // 4. Reconstruct Rotation
        Quaternion mountingRot = wheelCollider.transform.rotation;
        Quaternion steerRot = Quaternion.Euler(0, currentSteerAngle, 0);
        Quaternion spinRot = Quaternion.Euler(visualRotation, 0, 0);

        wheelVisual.rotation = mountingRot * steerRot * spinRot;
        
        // 5. Tire Deformation (optional)
        if (enableTireDeformation && originalScale != Vector3.zero)
        {
            ApplyTireDeformation(absForwardSlip, Mathf.Abs(lateralSlip));
        }
    }
    
    void ApplyTireDeformation(float forwardSlip, float lateralSlip)
    {
        // Deformation: Y (vertical) compresses, X/Z stretch slightly under heavy slip
        float forwardDeform = Mathf.Clamp01(forwardSlip / 0.5f); // Max at 0.5 slip
        float lateralDeform = Mathf.Clamp01(lateralSlip / 0.3f); // Max at 0.3 lateral slip
        
        float deformAmount = Mathf.Max(forwardDeform, lateralDeform) * maxDeformationScale;
        
        // Y compresses (squash), X/Z stretch slightly
        Vector3 deformScale = originalScale;
        deformScale.y *= (1f - deformAmount * 0.5f); // Compress vertically
        deformScale.x *= (1f + deformAmount * 0.3f); // Stretch horizontally
        deformScale.z *= (1f + deformAmount * 0.3f); // Stretch forward/back
        
        wheelVisual.localScale = deformScale;
    }

    /// Applies motor torque to the wheel.
    /// Only applies torque if wheel is grounded.
    public void ApplyTorque(float torque)
    {
        if (wheelCollider == null) return;
        
        // Check if wheel is grounded - WheelCollider only applies force when touching ground
        WheelHit hit;
        bool grounded = wheelCollider.GetGroundHit(out hit);
        
        if (grounded)
        {
            wheelCollider.motorTorque = torque;
        }
        else
        {
            // Wheel in air - no resistance, but also no traction
            wheelCollider.motorTorque = 0f;
        }
    }

    /// Returns true if the wheel is touching the ground.
    public bool IsGrounded()
    {
        if (wheelCollider == null) return false;
        WheelHit hit;
        return wheelCollider.GetGroundHit(out hit);
    }

    /// Applies brake torque to the wheel.
    public void ApplyBrake(float brakeTorque)
    {
        if (wheelCollider != null)
        {
            wheelCollider.brakeTorque = brakeTorque;
        }
    }

    /// Returns the current wheel RPM.
    public float GetRPM()
    {
        return wheelCollider != null ? wheelCollider.rpm : 0f;
    }
}