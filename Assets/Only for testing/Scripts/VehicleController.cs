using UnityEngine;
using UnityEngine.InputSystem;

// Wheel Speed -> Engine RPM -> Engine Torque -> Transmission -> Wheel Force.
[RequireComponent(typeof(Rigidbody))]
public class VehicleController : MonoBehaviour
{
    [Header("Components")]
    public VehicleEngine engine;
    public VehicleTransmission transmission;

    [Header("Powertrain")]
    [Tooltip("Master EV toggle. Syncs engine type and transmission behavior.")]
    public bool isElectricVehicle = false;
    
    public enum DrivetrainType { RWD, FWD, AWD }
    [Tooltip("Which wheels receive power: RWD (rear), FWD (front), AWD (all).")]
    public DrivetrainType drivetrain = DrivetrainType.RWD;
    
    [Tooltip("Maximum speed the engine can push the car (km/h). Can be exceeded going downhill.")]
    public float topSpeedKMH = 250f;
    
    [Tooltip("Override wheel radius for physics calculations (meters). Use if your model has oversized wheels. 0 = use actual WheelCollider radius.")]
    public float physicsWheelRadius = 0.34f;

    [Header("Handling")]
    public float vehicleMass = 1500f;
    public Vector3 centerOfMassOffset = new Vector3(0, -0.5f, 0.1f); 
    public float maxBrakeTorque = 5000f; 
    [Range(0f, 1f)] public float frontBrakeBias = 0.65f;
    [Tooltip("Base grip multiplier. Higher = more grip in corners.")]
    public float corneringStiffness = 2.5f; 
    [Tooltip("Downforce at high speed (N per km/h squared)")]
    public float downforceFactor = 3.0f;
    
    [Header("Dynamic Handling")]
    [Tooltip("How much weight transfers under acceleration/braking (0-1)")]
    [Range(0f, 1f)] public float weightTransferFactor = 0.4f;
    [Tooltip("How much grip increases with downforce (0-2)")]
    [Range(0f, 2f)] public float loadSensitivity = 0.8f;
    [Tooltip("Assists counter-steering when sliding (0-1)")]
    [Range(0f, 1f)] public float counterSteerAssist = 0.3f;
    [Tooltip("Limits wheelspin under acceleration (0=off, 1=full)")]
    [Range(0f, 1f)] public float tractionControl = 0.2f;
    
    [Header("Steering")]
    public float maxSteerAngle = 35f;
    [Tooltip("Steering response curve (lower = more progressive)")]
    [Range(0.5f, 3f)] public float steeringResponse = 1.5f;
    [Tooltip("How fast steering returns to center")]
    public float steeringReturnSpeed = 8f;
    public bool speedSensitiveSteering = true;

    [Header("Debug")]
    public float speedKMH;
    public float speedMS;
    public float engineTorque;
    public float driveTorque;
    
    // Physics causality debug
    [Header("Physics Validation")]
    public float expectedSpeedKMH;  
    public float slipRatio;         
    public float debugWheelRadius;  
    private bool isBraking;
    
    // Dynamic handling state
    private float currentSteerAngle = 0f;
    private float frontGripMultiplier = 1f;
    private float rearGripMultiplier = 1f;
    private Vector3 lastVelocity;
    private float lateralSlip = 0f;
    
    // Private
    private Vector2 moveInput;
    private float lastInputY = 0f;
    private Rigidbody rb;
    private VehicleWheel[] wheels;
    private PlayerInput playerInput;
    
    // Gear Logic
    private bool isInputReleaseRequired = false; // For stop-and-switch logic

    // Input Override (for AI/Self-driving)
    private Vector2 moveInputOverride;
    private bool useOverrideInput = false;

    // Exposed Input States
    public float currentThrottle { get; private set; }
    public float currentBrake { get; private set; }
    public float currentSteer { get; private set; }
    public bool isCurrentlyBraking => isBraking;

    public Vector2 EffectiveInput => useOverrideInput ? moveInputOverride : moveInput;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.mass = vehicleMass;
        rb.centerOfMass = centerOfMassOffset;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        DiscoverWheels();
        AutoLinkComponents();
        CheckPlayerInput();
    }

    void AutoLinkComponents()
    {
        if (engine == null) engine = GetComponent<VehicleEngine>();
        if (engine == null) engine = gameObject.AddComponent<VehicleEngine>();

        if (transmission == null) transmission = GetComponent<VehicleTransmission>();
        if (transmission == null) transmission = gameObject.AddComponent<VehicleTransmission>();

        // New Component: VehicleDataLink
        if (GetComponent<VehicleDataLink>() == null) gameObject.AddComponent<VehicleDataLink>();

        SyncPowertrainSettings();
    }

    void SyncPowertrainSettings()
    {
        if (engine != null)
        {
            engine.engineType = isElectricVehicle
                ? VehicleEngine.EngineType.Electric
                : VehicleEngine.EngineType.InternalCombustion;
        }
        if (transmission != null)
        {
            transmission.isElectric = isElectricVehicle;
        }
    }

    void OnValidate()
    {
        SyncPowertrainSettings();
        
        if (wheels != null && wheels.Length > 0)
        {
            ApplyDrivetrainConfig();
        }
    }

    void CheckPlayerInput()
    {
        playerInput = GetComponent<PlayerInput>();
        if (playerInput == null) Debug.LogError("[VehicleController] MISSING PlayerInput component!");
    }

    void DiscoverWheels()
    {
        VehicleWheel[] allWheels = GetComponentsInChildren<VehicleWheel>();
        // Get colliders from local children first, then fallback to global search
        WheelCollider[] childColliders = GetComponentsInChildren<WheelCollider>();
        WheelCollider[] allColliders = childColliders;
        
        System.Collections.Generic.List<VehicleWheel> uniqueWheelList = new System.Collections.Generic.List<VehicleWheel>();
        System.Collections.Generic.HashSet<WheelCollider> seenColliders = new System.Collections.Generic.HashSet<WheelCollider>();
                
        foreach (var w in allWheels)
        {
            // SMART LINKER: Search locally first, then globally
            if (w.wheelCollider == null)
            {
                string targetName = w.gameObject.name;
                
                // Try child colliders first
                foreach (var col in childColliders)
                {
                    if (col.gameObject.name.Contains(targetName))
                    {
                        w.wheelCollider = col;
                        break;
                    }
                }

                // NUCLEAR FALLBACK: Search ENTIRE scene by name if still null
                if (w.wheelCollider == null)
                {
                    WheelCollider[] globalColliders = UnityEngine.Object.FindObjectsByType<WheelCollider>(FindObjectsSortMode.None);
                    foreach (var col in globalColliders)
                    {
                        if (col.gameObject.name.Contains(targetName))
                        {
                            w.wheelCollider = col;
                            break;
                        }
                    }
                }
            }

            // SAFETY: Skip wheels with unassigned WheelCollider
            if (w.wheelCollider == null)
            {
                continue;
            }
            
            if (seenColliders.Contains(w.wheelCollider))
            {
                continue;
            }
            
            seenColliders.Add(w.wheelCollider);
            uniqueWheelList.Add(w);
        }
        
        wheels = uniqueWheelList.ToArray();
        
        foreach (var w in wheels)
        {
            if (w.wheelCollider == null) continue;

            if (w.isFront && !w.isSteer)
            {
                w.isSteer = true;
            }
        }
        
        if (wheels.Length > 0 && wheels[0] != null && wheels[0].wheelCollider != null)
        {
            debugWheelRadius = wheels[0].wheelCollider.radius;
        }
        
        ApplyDrivetrainConfig();
    }

    private string GetHierarchyPath(Transform t)
    {
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }
    
    void ApplyDrivetrainConfig()
    {
        if (wheels == null) return;
        
        foreach (var w in wheels)
        {
            switch (drivetrain)
            {
                case DrivetrainType.RWD:
                    w.isMotor = !w.isFront;
                    break;
                case DrivetrainType.FWD:
                    w.isMotor = w.isFront;
                    break;
                case DrivetrainType.AWD:
                    w.isMotor = true;
                    break;
            }
        }
    }

    public void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
    }

    public void OnShiftUp(InputValue value)
    {
        if (transmission == null) return;
        if (transmission.mode != VehicleTransmission.TransmissionMode.Manual) return;
        if (transmission.isElectric) return; // EVs don't shift
        
        int nextGear = transmission.currentGear + 1;
        if (nextGear > transmission.gearRatios.Length) return; // Already at top gear
        
        transmission.ShiftTo(nextGear);
    }

    public void OnShiftDown(InputValue value)
    {
        if (transmission == null) return;
        if (transmission.mode != VehicleTransmission.TransmissionMode.Manual) return;
        if (transmission.isElectric) return; // EVs don't shift
        
        int nextGear = transmission.currentGear - 1;
        if (nextGear < -1) return; // Already at reverse
        
        transmission.ShiftTo(nextGear);
    }

    void LateUpdate()
    {
        Vector2 input = EffectiveInput;
        if (wheels != null)
        {
            float steerAngle = CalculateSteerAngle(input.x);
            currentSteer = input.x;
            float visualCalcRadius = physicsWheelRadius > 0.01f ? physicsWheelRadius : 0.34f;

            foreach (var w in wheels) 
            {
               float wheelSteer = 0f;
               if (w.isSteer) wheelSteer = steerAngle;
               
               w.UpdateVisuals(wheelSteer, speedMS, visualCalcRadius);
            }
        }
    }

    void FixedUpdate()
    {
        if (wheels == null || wheels.Length == 0) return;
        float dt = Time.fixedDeltaTime;

        speedMS = rb.linearVelocity.magnitude;
        speedKMH = speedMS * 3.6f;
        
        if (speedKMH > topSpeedKMH)
        {
            float maxMS = topSpeedKMH / 3.6f;
            rb.linearVelocity = rb.linearVelocity.normalized * maxMS;
            speedMS = maxMS;
            speedKMH = topSpeedKMH;
        }

        float localVelZ = transform.InverseTransformDirection(rb.linearVelocity).z;

        float inputThrottle = 0f;
        float inputBrake = 0f;
        Vector2 input = EffectiveInput;
        float rawY = input.y;
        
        bool isFreshInput = Mathf.Abs(rawY) > 0.05f && Mathf.Abs(lastInputY) < 0.05f;
        
        if (isInputReleaseRequired)
        {
            if (Mathf.Abs(rawY) < 0.1f)
            {
                isInputReleaseRequired = false;
            }
        }
        else
        {
            if (transmission.currentGear == 0)
            {
                if (rawY > 0.1f) { transmission.SetDrive(); inputThrottle = rawY; }
                else if (rawY < -0.1f) { transmission.SetReverse(); inputThrottle = Mathf.Abs(rawY); }
            }
            else if (transmission.currentGear > 0)
            {
                if (rawY > 0) inputThrottle = rawY; // Accelerate
                else if (rawY < 0) // Brake/Reverse?
                {
                    if (speedKMH > 1.0f) 
                    {
                        inputBrake = Mathf.Abs(rawY); // Standard braking
                    }
                    else 
                    {
                        if (isFreshInput)
                        {
                            transmission.SetReverse();
                            // Do NOT throttle immediately to be safe? Or valid?
                            inputThrottle = Mathf.Abs(rawY);
                        }
                        else
                        {
                            // Continuous Hold -> Lock
                            isInputReleaseRequired = true;
                        }
                    }
                }
            }
            else if (transmission.currentGear == -1)
            {
                if (rawY < 0) inputThrottle = Mathf.Abs(rawY);
                else if (rawY > 0) // Brake/Forward?
                {
                    if (speedKMH > 1.0f) 
                    {
                        inputBrake = Mathf.Abs(rawY);
                    }
                    else 
                    {
                        if (isFreshInput)
                        {
                            transmission.SetDrive();
                            inputThrottle = rawY;
                        }
                        else
                        {
                            // Continuous Hold -> Lock
                            isInputReleaseRequired = true;
                        }
                    }
                }
            }
        }
        
        lastInputY = rawY;
        currentThrottle = inputThrottle;
        currentBrake = inputBrake;
        
        if (isInputReleaseRequired)
        {
            inputThrottle = 0f;
            inputBrake = 1f; // Max brake
            if (speedKMH < 0.5f) 
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        transmission.UpdateGearLogic(engine.currentRPM, engine.maxRPM, inputThrottle, dt);

        // Calculate acceleration for weight transfer
        Vector3 acceleration = (rb.linearVelocity - lastVelocity) / Mathf.Max(dt, 0.001f);
        lastVelocity = rb.linearVelocity;
        float longitudinalAccel = Vector3.Dot(acceleration, transform.forward);
        
        // Weight Transfer: shifts grip between front and rear
        // Positive accel = weight to rear, negative (braking) = weight to front
        float normalizedAccel = Mathf.Clamp(longitudinalAccel / 15f, -1f, 1f); // ~15 m/s² max
        float weightShift = normalizedAccel * weightTransferFactor;
        
        // Front loses grip under acceleration, gains under braking
        frontGripMultiplier = Mathf.Lerp(frontGripMultiplier, 1f - weightShift, dt * 8f);
        // Rear gains grip under acceleration, loses under braking  
        rearGripMultiplier = Mathf.Lerp(rearGripMultiplier, 1f + weightShift, dt * 8f);
        
        // Calculate lateral slip for counter-steer and drift detection
        Vector3 localVel = transform.InverseTransformDirection(rb.linearVelocity);
        lateralSlip = Mathf.Abs(localVel.x) / Mathf.Max(speedMS, 1f);
        
        // Downforce: scales with speed squared (realistic aerodynamics)
        if (isGroundedAny() && speedKMH > 20f)
        {
            float speedSquared = (speedKMH / 100f) * (speedKMH / 100f);
            float downforce = downforceFactor * vehicleMass * speedSquared;
            rb.AddForce(-transform.up * downforce, ForceMode.Force);
            
            // Downforce also increases grip via load sensitivity
            float loadBonus = 1f + (speedSquared * loadSensitivity * 0.3f);
            frontGripMultiplier *= loadBonus;
            rearGripMultiplier *= loadBonus;
        }
        
        float drivenWheelRPM = GetAverageDrivenRPM();
        float totalRatio = transmission.GetTotalRatio();
        
        // Determine if drivetrain is connected
        bool inNeutral = transmission.currentGear == 0;
        bool clutchDisengaged = transmission.clutchEngagement < 0.5f;
        bool isConnected = !inNeutral && !clutchDisengaged && Mathf.Abs(totalRatio) > 0.01f;
        
        if (isConnected)
        {
            
            // Calculate what wheel RPM SHOULD be for current vehicle speed
            float normalizedWheelRadius = 0.34f; // Standard car wheel
            float normalizedWheelRPM = (speedMS * 60f) / (2f * Mathf.PI * normalizedWheelRadius);
            
            // Apply gear ratio to get engine RPM
            float speedDerivedEngineRPM = normalizedWheelRPM * Mathf.Abs(totalRatio);
            
            // Smooth transition to target RPM
            float targetRPM = Mathf.Max(speedDerivedEngineRPM, isElectricVehicle ? 0f : engine.idleRPM);
            engine.currentRPM = Mathf.Lerp(engine.currentRPM, targetRPM, 25f * dt);
        }
        else
        {
            // FREE REVVING (Neutral or Clutch Out)
            float freeTorque = engine.CalculateTorque(engine.currentRPM, inputThrottle);
            // Lower inertia for free revving to make it snappy
            float effectiveInertia = Mathf.Max(engine.inertia * 0.3f, 0.05f); 
            float alpha = freeTorque / effectiveInertia;
            engine.currentRPM += alpha * dt;
            
            // Decay to idle if no throttle
            if (inputThrottle < 0.05f)
            {
                float targetIdle = isElectricVehicle ? 0f : engine.idleRPM;
                // Faster return to idle
                engine.currentRPM = Mathf.Lerp(engine.currentRPM, targetIdle, 5f * dt);
            }
        }
        
        // Clamp RPM
        float minRPM = isElectricVehicle ? 0f : engine.idleRPM;
        engine.currentRPM = Mathf.Clamp(engine.currentRPM, minRPM, engine.maxRPM);


        // B. Engine RPM + Throttle -> Engine Torque
        engineTorque = engine.CalculateTorque(engine.currentRPM, inputThrottle);
        engine.currentLoad = inputThrottle; // Simplified load for now


        // C. Transmission -> Drive Torque
        driveTorque = transmission.GetDriveTorque(engineTorque);


        // D. Power Limiting
        // At low speeds, use torque. At high speeds, limit by power.
        float targetPhysicsRadius = physicsWheelRadius > 0.01f ? physicsWheelRadius : 0.34f;
        
        // Calculate scale multiplier: Actual / Target
        float actualRadius = (wheels.Length > 0 && wheels[0].wheelCollider != null) ? wheels[0].wheelCollider.radius : 0.34f;
        float scaleMultiplier = actualRadius / targetPhysicsRadius;
        
        float torqueBasedForce = driveTorque / targetPhysicsRadius;
        
        // Power Limit: F = P / v (only matters at higher speeds)
        // At low speeds, torque is king. At high speeds, power limits acceleration.
        float effectiveForce;
        if (speedMS < 5f)
        {
            // Below 5 m/s (~18 km/h): Pure torque-based acceleration
            effectiveForce = torqueBasedForce;
        }
        else
        {
            // Above 5 m/s: Apply power limit
            float powerLimitedForce = (engine.maxPowerKW * 1000f) / speedMS;
            effectiveForce = Mathf.Min(torqueBasedForce, powerLimitedForce);
        }
        
        // Convert back to Torque for WheelCollider
        float finalWheelTorque = effectiveForce * actualRadius; 

        int driveCount = GetMotorWheelCount();
        
        // If brakes are applied, cut drive torque
        bool isBraking = inputBrake > 0.1f;
        float torquePerWheel = 0f;
        
        if (!isBraking && driveCount > 0)
        {
            float speedRatio = speedKMH / topSpeedKMH;
            float topSpeedMultiplier = 1f;
            
            if (speedRatio > 0.8f)
            {
                topSpeedMultiplier = Mathf.Clamp01(1f - ((speedRatio - 0.8f) / 0.2f));
            }
            
            // Hard Cutoff if exceeding top speed
            if (speedKMH > topSpeedKMH) topSpeedMultiplier = 0f;
            
            // Distribute torque
            torquePerWheel = (finalWheelTorque / driveCount) * topSpeedMultiplier;
        }

        // Apply to wheels with STABILIZED dynamic handling
        float targetSteerAngle = CalculateSteerAngle(input.x);
        
        // 1. Smooth Steering (Low Pass Filter)
        // More smoothing at high speeds to prevent twitchiness
        float steerSmoothness = Mathf.Lerp(15f, 5f, speedKMH / 100f); 
        currentSteerAngle = Mathf.Lerp(currentSteerAngle, targetSteerAngle, steerSmoothness * dt);
        
        // 2. Stable Counter-Steer Assist
        // Only apply if significant slip is detected and we are moving fast enough
        // Deadzone included to prevent micro-corrections (wiggling)
        if (counterSteerAssist > 0f && speedKMH > 30f)
        {
            Vector3 assistLocalVel = transform.InverseTransformDirection(rb.linearVelocity);
            float rawSlip = assistLocalVel.x / Mathf.Max(speedMS, 1.0f); // Normalized slip
            
            // Hysteresis/Deadzone
            if (Mathf.Abs(rawSlip) > 0.15f) 
            {
                float slideDirection = Mathf.Sign(rawSlip);
                // Dampened assist
                float assistAngle = slideDirection * (Mathf.Abs(rawSlip) - 0.15f) * maxSteerAngle * counterSteerAssist;
                // Clamp assist to avoid overriding user input completely
                assistAngle = Mathf.Clamp(assistAngle, -15f, 15f); 
                currentSteerAngle += assistAngle;
            }
        }

        float totalSlip = 0f;
        int slipCount = 0;
        
        // Grip Multiplier Clamping (Safety)
        // Prevent multipliers from going too wild which causes physics explosions
        frontGripMultiplier = Mathf.Clamp(frontGripMultiplier, 0.6f, 1.4f);
        rearGripMultiplier = Mathf.Clamp(rearGripMultiplier, 0.6f, 1.4f);

        foreach (var w in wheels)
        {
            if (w.wheelCollider == null) continue;
            
            // Apply dynamic grip based on weight transfer
            float gripMultiplier = w.isFront ? frontGripMultiplier : rearGripMultiplier;
            
            // Soft update of friction to prevent physics snapping
            WheelFrictionCurve sideFriction = w.wheelCollider.sidewaysFriction;
            sideFriction.stiffness = Mathf.Lerp(sideFriction.stiffness, corneringStiffness * gripMultiplier, dt * 10f); // Interpolate changes!
            w.wheelCollider.sidewaysFriction = sideFriction;
            
            WheelFrictionCurve forwardFriction = w.wheelCollider.forwardFriction;
            forwardFriction.stiffness = Mathf.Lerp(forwardFriction.stiffness, 1.5f * gripMultiplier, dt * 10f);
            w.wheelCollider.forwardFriction = forwardFriction;
            
            if (w.isSteer)
            {
                w.wheelCollider.steerAngle = currentSteerAngle;
            }
            
            // Get wheel contact info
            WheelHit hit;
            bool isGrounded = w.wheelCollider.GetGroundHit(out hit);
            
            // Apply motor torque with SAFE traction control
            if (w.isMotor && !isBraking)
            {
                float appliedTorque = torquePerWheel;
                
                // Traction control: Smooth intervention
                if (tractionControl > 0f && isGrounded)
                {
                    float wheelSlipRatio = Mathf.Abs(hit.forwardSlip);
                    // Higher threshold for intervention to prevent cutting power too early
                    if (wheelSlipRatio > 0.25f) 
                    {
                        float tcReduction = Mathf.Clamp01((wheelSlipRatio - 0.25f) / 0.5f);
                        appliedTorque *= 1f - (tcReduction * tractionControl);
                    }
                }
                
                w.wheelCollider.motorTorque = appliedTorque;
            }
            else
            {
                w.wheelCollider.motorTorque = 0f;
            }
            
            // Braking logic
            if (isBraking)
            {
                float brakeTorque;
                if (w.isFront)
                    brakeTorque = inputBrake * maxBrakeTorque * frontBrakeBias * scaleMultiplier;
                else
                    brakeTorque = inputBrake * maxBrakeTorque * (1f - frontBrakeBias) * scaleMultiplier;
                
                w.wheelCollider.brakeTorque = brakeTorque;
            }
            else if (Mathf.Abs(input.y) < 0.1f && speedKMH < 2f && !isInputReleaseRequired)
            {
                // Auto-Park
                w.wheelCollider.brakeTorque = maxBrakeTorque * scaleMultiplier; 
                rb.angularDamping = 2.0f; 
            }
            else
            {
                w.wheelCollider.brakeTorque = 0f;
                rb.angularDamping = 0.05f; // Return to normal drag
            }
            
            // Calculate slip for grounded wheels
            if (isGrounded)
            {
                Vector3 velocityAtWheel = rb.GetPointVelocity(hit.point);
                
                float arcadeRPM = (velocityAtWheel.magnitude * 60f) / (2f * Mathf.PI * targetPhysicsRadius);
                
                // "perfect" RPM for slip calculation
                float wheelSpeed = arcadeRPM * 2f * Mathf.PI * targetPhysicsRadius / 60f;
                // Note: wheelSpeed == velocityAtWheel.magnitude technically
                
                float groundSpeed = Vector3.Dot(velocityAtWheel, w.wheelCollider.transform.forward);
                float slipDenominator = Mathf.Max(Mathf.Abs(wheelSpeed), Mathf.Abs(groundSpeed), 0.1f);
                float wheelSlip = (wheelSpeed - groundSpeed) / slipDenominator;
                totalSlip += Mathf.Abs(wheelSlip);
                slipCount++;
            }
        }
        
        this.isBraking = isBraking; 
        
        // Force expected speed to be actual speed
        expectedSpeedKMH = speedKMH;
        float expectedSpeedMS = speedMS;
        
        // Fix slip ratio display
        if (slipCount == 0) slipRatio = 0f;
        else slipRatio = totalSlip / slipCount;
    }

    public void SetInputOverride(Vector2 input, bool active)
    {
        if (active && (Mathf.Abs(input.x) > 0.01f || Mathf.Abs(input.y) > 0.01f))
        {
            if (Time.frameCount % 60 == 0) 
                Debug.Log($"[Vehicle] Received AI Override: Steer={input.x:F2}, Throttle={input.y:F2}");
        }
        moveInputOverride = input;
        useOverrideInput = active;
    }

    public void SetDrivetrain(DrivetrainType type)
    {
        drivetrain = type;
        ApplyDrivetrainConfig();
    }

    float GetAverageDrivenRPM()
    {
        float total = 0;
        int count = 0;
        foreach(var w in wheels)
        {
            if (w.isMotor)
            {
                total += w.wheelCollider.rpm;
                count++;
            }
        }
        return count > 0 ? total / count : 0f;
    }

    int GetMotorWheelCount()
    {
        int c = 0;
        foreach (var w in wheels) if (w.isMotor) c++;
        return c;
    }
    
    bool isGroundedAny()
    {
        if (wheels == null) return false;
        foreach (var w in wheels) if (w.IsGrounded()) return true;
        return false;
    }

    float CalculateSteerAngle(float input)
    {
        if (useOverrideInput && Mathf.Abs(input) > 0.01f)
        {
            if (Time.frameCount % 60 == 0) Debug.Log($"[Vehicle] AI Steer Angle Calculation: Input={input:F2}, MaxAngle={maxSteerAngle}");
        }
        if (!speedSensitiveSteering) return input * maxSteerAngle;
        float speedFactor = Mathf.InverseLerp(10f, 120f, speedKMH);
        return input * Mathf.Lerp(maxSteerAngle, maxSteerAngle * 0.7f, speedFactor);
    }
}
