using UnityEngine;

/// <summary>
/// Applies exaggerated pitch/roll/squat dynamics to the vehicle body for visceral visual feedback.
/// Works with VehicleGForceCalculator to drive body motion from G-forces and throttle/brake inputs.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class VehicleBodyDynamics : MonoBehaviour
{
    [Header("References")]
    private Rigidbody rb;
    private VehicleGForceCalculator gForceCalc;
    private VehicleController controller;

    [Header("Pitch Dynamics (Squat/Dive)")]
    [Tooltip("Strength of pitch torque from longitudinal G. Lower = more stable (e.g. 0.2-0.5 for heavy EVs).")]
    [Range(0f, 1f)] public float pitchTorqueGain = 0.35f;
    [Tooltip("Additional pitch from throttle/brake input. Keep low to avoid rear lifting or front slamming.")]
    [Range(0f, 0.5f)] public float pitchInputMultiplier = 0.2f;
    [Tooltip("Vertical force couple (down at rear on accel, down at front on brake). 0 = disable. Use low values for heavy cars.")]
    [Range(0f, 2000f)] public float verticalForceCoupleStrength = 400f;
    [Tooltip("Distance from center to apply vertical forces (wheelbase fraction).")]
    [Range(0.3f, 0.7f)] public float forceCoupleDistance = 0.5f;

    [Header("Roll Dynamics")]
    [Tooltip("Strength of roll torque from lateral G (0-1). Higher = more pronounced body roll.")]
    [Range(0f, 2f)] public float rollTorqueGain = 1.2f;
    [Tooltip("Speed factor for roll (roll increases with speed).")]
    [Range(0f, 1f)] public float rollSpeedFactor = 0.3f;
    [Tooltip("Minimum speed (km/h) before roll torque applies.")]
    public float rollMinSpeedKMH = 10f;

    [Header("Vehicle Personality")]
    [Tooltip("Auto-tune based on drivetrain: RWD gets more pitch/weave, AWD less weave.")]
    public bool autoTuneByDrivetrain = true;
    [Tooltip("Manual override: Squat/dive strength multiplier.")]
    [Range(0.5f, 2f)] public float squatDiveStrength = 1f;
    [Tooltip("Manual override: Roll intensity multiplier.")]
    [Range(0.3f, 1.5f)] public float rollIntensity = 1f;

    private float wheelbaseM = 2.5f;
    private float lastThrottle = 0f;
    private float lastBrake = 0f;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        gForceCalc = GetComponent<VehicleGForceCalculator>();
        controller = GetComponent<VehicleController>();
        
        if (gForceCalc == null)
        {
            gForceCalc = gameObject.AddComponent<VehicleGForceCalculator>();
        }
    }

    void Start()
    {
        // Auto-tune based on drivetrain if enabled
        if (autoTuneByDrivetrain && controller != null)
        {
            ApplyDrivetrainTuning();
        }
        
        // Estimate wheelbase from WheelColliders on this vehicle
        WheelCollider[] colliders = GetComponentsInChildren<WheelCollider>();
        if (colliders != null && colliders.Length >= 2)
        {
            float minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (var wc in colliders)
            {
                if (wc == null) continue;
                Vector3 local = transform.InverseTransformPoint(wc.transform.position);
                minZ = Mathf.Min(minZ, local.z);
                maxZ = Mathf.Max(maxZ, local.z);
            }
            if (maxZ > minZ) wheelbaseM = maxZ - minZ;
        }
    }

    void FixedUpdate()
    {
        if (rb == null || gForceCalc == null) return;
        
        float dt = Time.fixedDeltaTime;
        float longitudinalG = gForceCalc.LongitudinalG;
        float lateralG = gForceCalc.LateralG;
        
        // Get throttle/brake from controller
        float throttle = 0f;
        float brake = 0f;
        if (controller != null)
        {
            throttle = controller.currentThrottle;
            brake = controller.currentBrake;
        }
        
        // Apply pitch torque (squat on accel, dive on brake)
        ApplyPitchDynamics(longitudinalG, throttle, brake, dt);
        
        // Apply roll torque (body lean into corners)
        ApplyRollDynamics(lateralG, dt);
        
        lastThrottle = throttle;
        lastBrake = brake;
    }

    void ApplyPitchDynamics(float longitudinalG, float throttle, float brake, float dt)
    {
        // Pitch torque: positive G (accel) = squat (rear down, nose up); negative G (brake) = dive (nose down, rear up).
        // Unity: positive torque around right = nose up, rear down.
        float pitchTorque = longitudinalG * pitchTorqueGain * squatDiveStrength;
        
        // Input-based pitch (subtle, same sign convention)
        float inputPitch = (throttle - brake) * pitchInputMultiplier * squatDiveStrength;
        pitchTorque += inputPitch;
        
        // Scale torque by mass and a moderate factor so heavy EVs don't flip
        float torqueMagnitude = Mathf.Clamp(pitchTorque * rb.mass * 25f, -8000f, 8000f);
        rb.AddTorque(transform.right * torqueMagnitude, ForceMode.Force);
        
        // Vertical force couple: subtle so rear doesn't go airborne
        if (verticalForceCoupleStrength > 0f && Mathf.Abs(longitudinalG) > 0.08f)
        {
            float forceMagnitude = Mathf.Abs(longitudinalG) * verticalForceCoupleStrength * squatDiveStrength;
            forceMagnitude = Mathf.Min(forceMagnitude, rb.mass * 2f); // Cap to avoid lifting
            Vector3 rearForcePoint = transform.position - transform.forward * (wheelbaseM * forceCoupleDistance * 0.5f);
            Vector3 frontForcePoint = transform.position + transform.forward * (wheelbaseM * forceCoupleDistance * 0.5f);
            
            if (longitudinalG > 0f) // Accelerating: down at rear, up at front (squat)
            {
                rb.AddForceAtPosition(-transform.up * forceMagnitude, rearForcePoint, ForceMode.Force);
                rb.AddForceAtPosition(transform.up * forceMagnitude * 0.6f, frontForcePoint, ForceMode.Force);
            }
            else // Braking: down at front, up at rear (dive)
            {
                rb.AddForceAtPosition(-transform.up * forceMagnitude, frontForcePoint, ForceMode.Force);
                rb.AddForceAtPosition(transform.up * forceMagnitude * 0.6f, rearForcePoint, ForceMode.Force);
            }
        }
    }

    void ApplyRollDynamics(float lateralG, float dt)
    {
        if (controller == null) return;
        
        float speedKMH = controller.speedKMH;
        if (speedKMH < rollMinSpeedKMH) return;
        
        // Roll torque: negative lateral G (left turn) = roll left, positive (right turn) = roll right
        // Roll = rotation around forward axis (Z axis in local space)
        float speedFactor = 1f + (speedKMH / 200f) * rollSpeedFactor;
        float rollTorque = -lateralG * rollTorqueGain * rollIntensity * speedFactor;
        
        rb.AddTorque(transform.forward * rollTorque * rb.mass * 100f, ForceMode.Force);
    }

    void ApplyDrivetrainTuning()
    {
        if (controller == null) return;
        
        switch (controller.drivetrain)
        {
            case VehicleController.DrivetrainType.RWD:
                pitchTorqueGain *= 1.15f;
                rollTorqueGain *= 1.1f;
                verticalForceCoupleStrength *= 1.1f;
                break;
            case VehicleController.DrivetrainType.FWD:
                pitchTorqueGain *= 0.9f;
                verticalForceCoupleStrength *= 0.85f;
                break;
            case VehicleController.DrivetrainType.AWD:
                // EVs / heavy AWD: keep forces moderate so car stays planted
                pitchTorqueGain *= 0.85f;
                rollTorqueGain *= 0.9f;
                verticalForceCoupleStrength *= 0.8f;
                break;
        }
    }

    /// <summary>
    /// Apply a shift kick impulse (rearward force/pitch torque).
    /// Called by VehicleController when shift completes.
    /// </summary>
    public void ApplyShiftKick(float kickStrength)
    {
        if (rb == null) return;
        
        // Rearward force (subtle push, not a crash)
        rb.AddForce(transform.forward * kickStrength * rb.mass * 0.08f, ForceMode.Impulse);
        
        // Pitch torque backward (rear down) — reduced to prevent rear lifting off ground
        rb.AddTorque(transform.right * kickStrength * rb.mass * 5f, ForceMode.Impulse);
    }
}
