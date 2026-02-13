using UnityEngine;
using System.Collections.Generic;

public class VehicleAerodynamics : MonoBehaviour
{
    [Header("Basic Aero")]
    public float dragCoefficient = 0.3f;
    public float downforceCoefficient = 0.1f;
    public float frontalArea = 2.2f;
    
    [Header("High-Speed Settling")]
    [Tooltip("Speed threshold (km/h) where high-speed downforce ramp begins.")]
    public float highSpeedThresholdKMH = 150f;
    [Tooltip("Speed (km/h) where high-speed downforce reaches full effect.")]
    public float highSpeedFullKMH = 200f;
    [Tooltip("Additional downforce multiplier at high speed (1.0 = no extra, 1.3 = 30% more).")]
    [Range(1f, 2f)] public float highSpeedDownforceMultiplier = 1.3f;
    
    [Header("Body Weave (RWD Instability)")]
    [Tooltip("Enable subtle body weave at high speed for RWD vehicles.")]
    public bool enableBodyWeave = true;
    [Tooltip("Minimum speed (km/h) for weave to activate.")]
    public float weaveMinSpeedKMH = 200f;
    [Tooltip("Weave torque strength (subtle oscillation).")]
    [Range(0f, 500f)] public float weaveTorqueStrength = 150f;
    [Tooltip("Weave frequency (oscillations per second).")]
    [Range(0.5f, 3f)] public float weaveFrequency = 1.5f;
    
    [Header("Active Aero (Optional)")]
    [Tooltip("Enable active aero elements (spoilers, diffusers).")]
    public bool enableActiveAero = false;
    [Tooltip("Transform references for active aero elements (spoilers, diffusers).")]
    public Transform[] activeAeroElements;
    [Tooltip("Maximum rotation angle for active aero (degrees).")]
    [Range(0f, 30f)] public float maxAeroAngle = 15f;
    [Tooltip("Axis for aero rotation (0=X, 1=Y, 2=Z).")]
    [Range(0, 2)] public int aeroRotationAxis = 0;
    
    private Rigidbody rb;
    private VehicleController controller;
    private VehicleGForceCalculator gForceCalc;
    private float weaveTime = 0f;
    private Vector3[] originalAeroRotations;
    
    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        controller = GetComponent<VehicleController>();
        gForceCalc = GetComponent<VehicleGForceCalculator>();
        
        // Store original aero rotations
        if (activeAeroElements != null && activeAeroElements.Length > 0)
        {
            originalAeroRotations = new Vector3[activeAeroElements.Length];
            for (int i = 0; i < activeAeroElements.Length; i++)
            {
                if (activeAeroElements[i] != null)
                {
                    originalAeroRotations[i] = activeAeroElements[i].localEulerAngles;
                }
            }
        }
    }
    
    void FixedUpdate()
    {
        if (rb == null) return;
        
        float dt = Time.fixedDeltaTime;
        weaveTime += dt;
        
        // Apply aerodynamics
        ApplyAerodynamics(rb);
        
        // Apply body weave if enabled
        if (enableBodyWeave)
        {
            ApplyBodyWeave(dt);
        }
        
        // Update active aero visuals
        if (enableActiveAero)
        {
            UpdateActiveAero();
        }
    }
    
    public void ApplyAerodynamics(Rigidbody rb)
    {
        if (rb == null) return;
        
        float speed = rb.linearVelocity.magnitude;
        float speedKMH = speed * 3.6f;
        float rho = 1.225f; // Air density
        float dynamicPressure = 0.5f * rho * speed * speed;
        
        // Drag
        Vector3 velocityDir = rb.linearVelocity.normalized;
        Vector3 dragForce = -velocityDir * dynamicPressure * dragCoefficient * frontalArea;
        rb.AddForce(dragForce);
        
        // Base downforce
        float downforceMagnitude = dynamicPressure * downforceCoefficient * frontalArea;
        
        // High-speed downforce ramp (car settles lower at high speed)
        float highSpeedFactor = 1f;
        if (speedKMH > highSpeedThresholdKMH)
        {
            float ramp = Mathf.InverseLerp(highSpeedThresholdKMH, highSpeedFullKMH, speedKMH);
            ramp = Mathf.Clamp01(ramp);
            highSpeedFactor = Mathf.Lerp(1f, highSpeedDownforceMultiplier, ramp);
        }
        
        Vector3 downforce = -transform.up * downforceMagnitude * highSpeedFactor;
        rb.AddForce(downforce);
    }
    
    void ApplyBodyWeave(float dt)
    {
        if (controller == null || gForceCalc == null) return;
        
        float speedKMH = controller.speedKMH;
        if (speedKMH < weaveMinSpeedKMH) return;
        
        // Only apply weave to RWD vehicles
        if (controller.drivetrain != VehicleController.DrivetrainType.RWD) return;
        
        // Subtle yaw oscillation (sinusoidal)
        float lateralG = gForceCalc.LateralG;
        float speedFactor = Mathf.InverseLerp(weaveMinSpeedKMH, 300f, speedKMH);
        float weaveAmount = Mathf.Sin(weaveTime * weaveFrequency * 2f * Mathf.PI) * weaveTorqueStrength * speedFactor;
        
        // Scale by lateral slip (more unstable when sliding)
        float slipFactor = 1f + Mathf.Abs(lateralG) * 0.5f;
        weaveAmount *= slipFactor;
        
        // Apply subtle yaw torque
        rb.AddTorque(transform.up * weaveAmount * dt, ForceMode.Force);
    }
    
    void UpdateActiveAero()
    {
        if (activeAeroElements == null || activeAeroElements.Length == 0) return;
        if (controller == null) return;
        
        float speedKMH = controller.speedKMH;
        float speedFactor = Mathf.InverseLerp(80f, 200f, speedKMH); // Ramp from 80 to 200 km/h
        speedFactor = Mathf.Clamp01(speedFactor);
        
        float targetAngle = speedFactor * maxAeroAngle;
        
        for (int i = 0; i < activeAeroElements.Length; i++)
        {
            if (activeAeroElements[i] == null) continue;
            
            Vector3 baseRotation = originalAeroRotations != null && i < originalAeroRotations.Length 
                ? originalAeroRotations[i] 
                : Vector3.zero;
            
            Vector3 newRotation = baseRotation;
            newRotation[aeroRotationAxis] = baseRotation[aeroRotationAxis] + targetAngle;
            
            activeAeroElements[i].localEulerAngles = Vector3.Lerp(
                activeAeroElements[i].localEulerAngles,
                newRotation,
                Time.fixedDeltaTime * 5f // Smooth rotation
            );
        }
    }
}
