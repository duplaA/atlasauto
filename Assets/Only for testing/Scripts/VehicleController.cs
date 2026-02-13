using UnityEngine;
using UnityEngine.InputSystem;

// Wheel Speed -> Engine RPM -> Engine Torque -> Transmission -> Wheel Force.
// FH5-style: for best handling use Edit > Project Settings > Time > Fixed Timestep 0.01-0.005 (100-200 Hz).
[RequireComponent(typeof(Rigidbody))]
public class VehicleController : MonoBehaviour
{
    [Header("Components")]
    public VehicleEngine engine;
    public VehicleTransmission transmission;
    public VehicleTyres tyres;
    public VehicleSuspension suspension;
    public VehicleBodyDynamics bodyDynamics;
    public VehicleAerodynamics aerodynamics;
    
    [Header("Drift & Handbrake")]
    public bool enableDriftAssist = true;
    [Tooltip("Sideways friction multiplier when handbrake is pulled (0.2-0.5 typical)")]
    public float handbrakeSlipMultiplier = 0.4f;
    [Tooltip("How much the car naturally wants to rotate when drifting")]
    public float driftSpinFactor = 0.5f;

    // Input state
    public bool isHandbrakePulled { get; private set; }

    [Header("Powertrain")]
    [Tooltip("Master EV toggle. Syncs engine type and transmission behavior.")]
    public bool isElectricVehicle = false;
    
    public enum DrivetrainType { RWD, FWD, AWD }
    [Tooltip("Which wheels receive power: RWD (rear), FWD (front), AWD (all).")]
    public DrivetrainType drivetrain = DrivetrainType.RWD;
    
    [Header("Differential (FH5-style power split)")]
    [Tooltip("RWD: fraction of rear torque under acceleration (0.25-0.6; 1 = full lock).")]
    [Range(0.25f, 1f)] public float differentialAccelSplitRWD = 0.5f;
    [Tooltip("RWD: under decel/coast (0.2-0.45; lower = less entry oversteer).")]
    [Range(0.2f, 0.5f)] public float differentialDecelSplitRWD = 0.35f;
    [Tooltip("FWD: fraction of front torque under acceleration (0.15-0.3).")]
    [Range(0.15f, 0.35f)] public float differentialAccelSplitFWD = 0.22f;
    [Tooltip("AWD: fraction of torque to rear (0.6-0.7).")]
    [Range(0.5f, 0.8f)] public float differentialAWDRearBias = 0.65f;
    
    [Tooltip("Maximum speed the engine can push the car (km/h). Can be exceeded going downhill.")]
    public float topSpeedKMH = 250f;
    
    [Tooltip("Override wheel radius for physics calculations (meters). Use if your model has oversized wheels. 0 = use actual WheelCollider radius.")]

    public float physicsWheelRadius = 0.34f;
    [Tooltip("Visual wheel radius for rotation speed (meters). Lower = faster spin.")]
    public float visualWheelRadius = 0.34f;

    [Header("Handling")]
    [Tooltip("For FH5-like smooth weight transfer use Fixed Timestep 0.01-0.005 (100-200 Hz) in Project Settings > Time.")]
    public float vehicleMass = 1500f;
    public Vector3 centerOfMassOffset = new Vector3(0, -0.5f, 0.1f); 
    public float maxBrakeTorque = 5000f;
    [Tooltip("FH5: 52-53% front typical; rearward for trail-brake.")]
    [Range(0f, 1f)] public float frontBrakeBias = 0.52f;
    [Tooltip("Brake pressure multiplier (100-120%); higher = quicker lock-up.")]
    [Range(1f, 1.2f)] public float brakePressureMultiplier = 1f;
    [Tooltip("Base grip multiplier. Higher = more grip in corners.")]
    public float corneringStiffness = 2.5f; 
    [Tooltip("Downforce factor (FH5: effective above ~150 km/h).")]
    public float downforceFactor = 3.0f;
    [Tooltip("Downforce exponent (FH5: 1.8; was 2.0).")]
    [Range(1.5f, 2.2f)] public float downforceExponent = 1.8f;
    [Tooltip("Drag coefficient for F_drag = 0.5*rho*Cd*A*v^2.")]
    public float dragCoefficient = 0.3f;
    [Tooltip("Frontal area (m²) for drag.")]
    public float frontalArea = 2.2f;
    
    [Header("Dynamic Handling")]
    [Tooltip("How much weight transfers under acceleration/braking (0-1)")]
    [Range(0f, 1f)] public float weightTransferFactor = 0.4f;
    [Tooltip("FH5 weight transfer: average spring rate (N/m) for lateral transfer.")]
    public float springRateAvgNpm = 40000f;
    [Tooltip("How much grip increases with downforce (0-2)")]
    [Range(0f, 2f)] public float loadSensitivity = 0.8f;
    [Tooltip("Assists counter-steering when sliding (0-1)")]
    [Range(0f, 1f)] public float counterSteerAssist = 0.3f;
    [Tooltip("Limits wheelspin under acceleration (0=off, 1=full)")]
    [Range(0f, 1f)] public float tractionControl = 0.2f;
    
    [Header("Shift Kick")]
    [Tooltip("Strength of shift kick impulse (rearward force/torque on upshift).")]
    [Range(0f, 10f)] public float shiftKickStrength = 3f;
    [Tooltip("Minimum throttle to trigger shift kick (0-1).")]
    [Range(0f, 1f)] public float shiftKickMinThrottle = 0.7f;
    [Tooltip("Brief torque spike multiplier for tire chirp on shift (1.0 = no spike).")]
    [Range(1f, 2f)] public float shiftChirpTorqueMultiplier = 1.3f;
    
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
    public float maxSlipRatio;
    public float debugWheelRadius;
    [Tooltip("Average suspension deflection 0-1 for camera/effects.")]
    public float averageSuspensionDeflection;  
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
    private float trackWidthM = 1.5f;
    private float wheelbaseM = 2.5f;
    private VehicleGForceCalculator gForceCalc;
    private float lateralTransferPercentStored;
    private float lateralGStored;
    private float weightShiftPercentStored;
    private float torquePerWheelFrontAWD;
    private float torquePerWheelRearAWD;
    
    // Per-wheel slip storage for visuals
    private float[] wheelForwardSlip;
    private float[] wheelLateralSlip;
    
    // Shift kick state
    private float shiftChirpTimer = 0f;
    private const float shiftChirpDuration = 0.1f; // Brief spike
    
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
    public float WeightShiftPercent => weightShiftPercentStored;

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
        
        // Initialize per-wheel slip arrays
        if (wheels != null && wheels.Length > 0)
        {
            wheelForwardSlip = new float[wheels.Length];
            wheelLateralSlip = new float[wheels.Length];
        }
    }

    void Start()
    {
        if (tyres == null) tyres = GetComponent<VehicleTyres>();
        if (tyres != null && wheels != null && wheels.Length > 0)
        {
            tyres.drivetrain = drivetrain;
            tyres.UpdateFriction(wheels);
        }
        if (suspension == null) suspension = GetComponent<VehicleSuspension>();
    }

    void AutoLinkComponents()
    {
        if (engine == null) engine = GetComponent<VehicleEngine>();
        if (engine == null) engine = gameObject.AddComponent<VehicleEngine>();

        if (transmission == null) transmission = GetComponent<VehicleTransmission>();
        if (transmission == null) transmission = gameObject.AddComponent<VehicleTransmission>();

        if (GetComponent<VehicleDataLink>() == null) gameObject.AddComponent<VehicleDataLink>();
        if (GetComponent<VehicleSpeedSense>() == null) gameObject.AddComponent<VehicleSpeedSense>();
        if (GetComponent<VehicleGForceCalculator>() == null) gameObject.AddComponent<VehicleGForceCalculator>();
        gForceCalc = GetComponent<VehicleGForceCalculator>();
        
        if (bodyDynamics == null) bodyDynamics = GetComponent<VehicleBodyDynamics>();
        if (bodyDynamics == null) bodyDynamics = gameObject.AddComponent<VehicleBodyDynamics>();
        
        if (aerodynamics == null) aerodynamics = GetComponent<VehicleAerodynamics>();
        if (aerodynamics == null) aerodynamics = gameObject.AddComponent<VehicleAerodynamics>();

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
        ComputeTrackAndWheelbase();
        ApplyDrivetrainConfig();
    }
    
    void ComputeTrackAndWheelbase()
    {
        if (wheels == null || wheels.Length < 2) return;
        float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
        foreach (var w in wheels)
        {
            if (w.wheelCollider == null) continue;
            Vector3 local = transform.InverseTransformPoint(w.wheelCollider.transform.position);
            minX = Mathf.Min(minX, local.x); maxX = Mathf.Max(maxX, local.x);
            minZ = Mathf.Min(minZ, local.z); maxZ = Mathf.Max(maxZ, local.z);
        }
        trackWidthM = maxX > minX ? (maxX - minX) : 1.5f;
        wheelbaseM = maxZ > minZ ? (maxZ - minZ) : 2.5f;
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

    public void OnHandbrake(InputValue value)
    {
        isHandbrakePulled = value.isPressed;
    }

    void LateUpdate()
    {
        Vector2 input = EffectiveInput;
        if (wheels != null)
        {
            float steerAngle = CalculateSteerAngle(input.x);
            currentSteer = input.x;
            // Use specific visual radius if set, otherwise fallback to physics radius or default
            float visualCalcRadius = visualWheelRadius > 0.01f ? visualWheelRadius : (physicsWheelRadius > 0.01f ? physicsWheelRadius : 0.34f);

            for (int i = 0; i < wheels.Length; i++)
            {
                var w = wheels[i];
                if (w == null) continue;
                
                float wheelSteer = 0f;
                if (w.isSteer) wheelSteer = steerAngle;
                
                // Pass per-wheel slip to visuals
                float forwardSlip = (wheelForwardSlip != null && i < wheelForwardSlip.Length) ? wheelForwardSlip[i] : 0f;
                float lateralSlip = (wheelLateralSlip != null && i < wheelLateralSlip.Length) ? wheelLateralSlip[i] : 0f;
                
                w.UpdateVisuals(wheelSteer, speedMS, visualCalcRadius, forwardSlip, lateralSlip);
            }

            float sum = 0f;
            foreach (var w in wheels) sum += w.SuspensionDeflection;
            averageSuspensionDeflection = wheels.Length > 0 ? sum / wheels.Length : 0f;
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
        
        // Detect shift completion and apply shift kick
        if (transmission != null && transmission.JustCompletedShift && inputThrottle > shiftKickMinThrottle)
        {
            // Apply shift kick via body dynamics
            if (bodyDynamics != null)
            {
                bodyDynamics.ApplyShiftKick(shiftKickStrength);
            }
            
            // Brief torque spike for tire chirp (if TC is low/off)
            if (tractionControl < 0.5f)
            {
                shiftChirpTimer = shiftChirpDuration;
            }
        }
        
        // Update shift chirp timer
        if (shiftChirpTimer > 0f)
        {
            shiftChirpTimer -= dt;
        }

        // Calculate acceleration for weight transfer (FH5 formulas)
        Vector3 acceleration = (rb.linearVelocity - lastVelocity) / Mathf.Max(dt, 0.001f);
        lastVelocity = rb.linearVelocity;
        float longitudinalAccel = Vector3.Dot(acceleration, transform.forward);
        float lateralAccel = Vector3.Dot(acceleration, transform.right);
        float lateralG = lateralAccel / 9.81f;
        float longitudinalG = longitudinalAccel / 9.81f;
        
        // Longitudinal: brake = more front load, accel = more rear load
        float longTransferBrake = frontBrakeBias * Mathf.Max(0f, -longitudinalG) * (wheelbaseM * 0.5f);
        float longTransferAccel = (1f - frontBrakeBias) * Mathf.Max(0f, longitudinalG) * (wheelbaseM * 0.5f);
        float norm = vehicleMass * 9.81f * (wheelbaseM * 0.5f);
        float brakeShift = (norm > 0.01f) ? (longTransferBrake / norm) * weightTransferFactor : 0f;
        float accelShift = (norm > 0.01f) ? (longTransferAccel / norm) * weightTransferFactor : 0f;
        float weightShift = accelShift - brakeShift;
        
        // Lateral transfer: reduce inside-wheel grip
        float lateralTransferPercent = (Mathf.Abs(lateralG) * trackWidthM * 0.5f) / Mathf.Max(springRateAvgNpm, 1000f);
        lateralTransferPercent = Mathf.Clamp(lateralTransferPercent, 0f, 0.15f);
        
        frontGripMultiplier = Mathf.Lerp(frontGripMultiplier, 1f - weightShift, dt * 8f);
        rearGripMultiplier = Mathf.Lerp(rearGripMultiplier, 1f + weightShift, dt * 8f);
        lateralTransferPercentStored = lateralTransferPercent;
        lateralGStored = lateralG;
        weightShiftPercentStored = weightShift;
        if (suspension == null) suspension = GetComponent<VehicleSuspension>();
        if (suspension != null) suspension.UpdateSuspension(wheels, weightShiftPercentStored, longitudinalG);
        
        float driftFactor = 1f;
        if (isHandbrakePulled) driftFactor = handbrakeSlipMultiplier;
        
        if (tyres != null) tyres.UpdateFriction(wheels, driftFactor, wheelForwardSlip);
        
        // Calculate lateral slip for counter-steer and drift detection
        Vector3 localVel = transform.InverseTransformDirection(rb.linearVelocity);
        lateralSlip = Mathf.Abs(localVel.x) / Mathf.Max(speedMS, 1f);
        
        // Apply aerodynamics (unified aero system with high-speed settling)
        if (aerodynamics != null)
        {
            aerodynamics.ApplyAerodynamics(rb);
            
            // Apply grip bonus from downforce
            if (isGroundedAny() && speedKMH > 20f)
            {
                float speedNorm = speedKMH / 100f;
                float downforceScale = Mathf.Clamp01(Mathf.InverseLerp(80f, 150f, speedKMH));
                float loadNorm = Mathf.Pow(speedNorm, downforceExponent) * downforceScale;
                float loadBonus = 1f + (loadNorm * loadSensitivity * 0.3f);
                frontGripMultiplier *= loadBonus;
                rearGripMultiplier *= loadBonus;
            }
        }
        else
        {
            // Fallback: inline aero if component missing
            if (isGroundedAny() && speedKMH > 20f)
            {
                float speedNorm = speedKMH / 100f;
                float downforceMagnitude = downforceFactor * vehicleMass * Mathf.Pow(speedNorm, downforceExponent);
                float downforceScale = Mathf.Clamp01(Mathf.InverseLerp(80f, 150f, speedKMH));
                rb.AddForce(-transform.up * (downforceMagnitude * downforceScale), ForceMode.Force);
                float loadNorm = Mathf.Pow(speedNorm, downforceExponent) * downforceScale;
                float loadBonus = 1f + (loadNorm * loadSensitivity * 0.3f);
                frontGripMultiplier *= loadBonus;
                rearGripMultiplier *= loadBonus;
            }
            float rho = 1.225f;
            Vector3 vel = rb.linearVelocity;
            float speedSq = vel.sqrMagnitude;
            if (speedSq > 0.01f)
                rb.AddForce(-vel.normalized * (0.5f * rho * dragCoefficient * frontalArea * speedSq), ForceMode.Force);
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
            if (speedRatio > 0.8f) topSpeedMultiplier = Mathf.Clamp01(1f - ((speedRatio - 0.8f) / 0.2f));
            if (speedKMH > topSpeedKMH) topSpeedMultiplier = 0f;
            float totalTorque = finalWheelTorque * topSpeedMultiplier;
            if (drivetrain == DrivetrainType.RWD)
            {
                float diffAccel = inputThrottle > 0.05f ? differentialAccelSplitRWD : (speedKMH > 1f ? differentialDecelSplitRWD : 1f);
                torquePerWheel = (totalTorque / driveCount) * diffAccel;
            }
            else if (drivetrain == DrivetrainType.FWD)
            {
                float diffAccel = inputThrottle > 0.05f ? differentialAccelSplitFWD : 1f;
                torquePerWheel = (totalTorque / driveCount) * diffAccel;
            }
            else
            {
                int frontDrive = 0, rearDrive = 0;
                foreach (var w in wheels) if (w.isMotor) { if (w.isFront) frontDrive++; else rearDrive++; }
                float frontTorque = totalTorque * (1f - differentialAWDRearBias);
                float rearTorque = totalTorque * differentialAWDRearBias;
                torquePerWheelFrontAWD = frontDrive > 0 ? frontTorque / frontDrive : 0f;
                torquePerWheelRearAWD = rearDrive > 0 ? rearTorque / rearDrive : 0f;
                torquePerWheel = 0f;
            }
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
        if (counterSteerAssist > 0f && (speedKMH > 30f || isHandbrakePulled))
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
        float maxSlip = 0f;
        
        frontGripMultiplier = Mathf.Clamp(frontGripMultiplier, 0.6f, 1.4f);
        rearGripMultiplier = Mathf.Clamp(rearGripMultiplier, 0.6f, 1.4f);

        int wheelIndex = 0;
        foreach (var w in wheels)
        {
            if (w.wheelCollider == null) continue;
            
            // Apply dynamic grip: front/rear + lateral (inside wheel loses grip)
            float gripMultiplier = w.isFront ? frontGripMultiplier : rearGripMultiplier;
            bool isLeft = transform.InverseTransformPoint(w.wheelCollider.transform.position).x < 0f;
            bool insideInTurn = (lateralGStored > 0f && isLeft) || (lateralGStored < 0f && !isLeft);
            float sideMod = insideInTurn ? (1f - lateralTransferPercentStored) : (1f + lateralTransferPercentStored);
            gripMultiplier *= sideMod;
            float tempMult = (tyres != null) ? tyres.GetGripMultiplierFromTemp(wheelIndex) : 1f;
            gripMultiplier *= tempMult;
            float baseLateral = tyres != null ? (tyres.lateralStiffness * tyres.ResponsivenessFromPressure) : corneringStiffness;
            float baseForward = tyres != null ? (tyres.longitudinalStiffness * tyres.ResponsivenessFromPressure) : 1.5f;
            
            // Soft update of friction to prevent physics snapping
            WheelFrictionCurve sideFriction = w.wheelCollider.sidewaysFriction;
            sideFriction.stiffness = Mathf.Lerp(sideFriction.stiffness, baseLateral * gripMultiplier, dt * 10f);
            w.wheelCollider.sidewaysFriction = sideFriction;
            
            WheelFrictionCurve forwardFriction = w.wheelCollider.forwardFriction;
            forwardFriction.stiffness = Mathf.Lerp(forwardFriction.stiffness, baseForward * gripMultiplier, dt * 10f);
            w.wheelCollider.forwardFriction = forwardFriction;
            
            if (w.isSteer)
            {
                w.wheelCollider.steerAngle = currentSteerAngle;
            }
            
            // Get wheel contact info
            WheelHit hit;
            bool isGrounded = w.wheelCollider.GetGroundHit(out hit);
            
            // Apply motor torque with SAFE traction control (differential applied above)
            if (w.isMotor && !isBraking)
            {
                float appliedTorque = (drivetrain == DrivetrainType.AWD) ? (w.isFront ? torquePerWheelFrontAWD : torquePerWheelRearAWD) : torquePerWheel;
                
                // Shift chirp: brief torque spike
                if (shiftChirpTimer > 0f)
                {
                    appliedTorque *= shiftChirpTorqueMultiplier;
                }
                
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
            
            // Braking logic (FH5: bias 52-53%, pressure multiplier, ABS at slip 0.15)
            if (isBraking)
            {
                float brakeTorque;
                if (w.isFront)
                    brakeTorque = inputBrake * maxBrakeTorque * frontBrakeBias * scaleMultiplier;
                else
                    brakeTorque = inputBrake * maxBrakeTorque * (1f - frontBrakeBias) * scaleMultiplier;
                brakeTorque *= brakePressureMultiplier;
                if (maxSlipRatio > 0.15f)
                    brakeTorque *= Mathf.Clamp01(0.15f / maxSlipRatio);
                w.wheelCollider.brakeTorque = brakeTorque;
            }
            else if (isHandbrakePulled && !w.isFront)
            {
                 // Handbrake locks rear wheels
                 w.wheelCollider.brakeTorque = maxBrakeTorque * brakePressureMultiplier * 1.5f;
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
                float absSlip = Mathf.Abs(wheelSlip);
                totalSlip += absSlip;
                if (absSlip > maxSlip) maxSlip = absSlip;
                slipCount++;
                
                // Store per-wheel slip for visuals
                if (wheelForwardSlip != null && wheelIndex < wheelForwardSlip.Length)
                {
                    wheelForwardSlip[wheelIndex] = hit.forwardSlip;
                    wheelLateralSlip[wheelIndex] = hit.sidewaysSlip;
                }
            }
            else
            {
                // No ground contact - zero slip
                if (wheelForwardSlip != null && wheelIndex < wheelForwardSlip.Length)
                {
                    wheelForwardSlip[wheelIndex] = 0f;
                    wheelLateralSlip[wheelIndex] = 0f;
                }
            }
            wheelIndex++;
        }
        
        maxSlipRatio = maxSlip;
        if (slipCount == 0) slipRatio = 0f;
        else slipRatio = totalSlip / slipCount;
        
        if (tyres != null)
        {
            float speedNorm = topSpeedKMH > 0.01f ? Mathf.Clamp01(speedKMH / topSpeedKMH) : 0f;
            tyres.UpdateTemperatures(wheels, slipRatio, speedNorm);
        }
        
        this.isBraking = isBraking; 
        expectedSpeedKMH = speedKMH;
        float expectedSpeedMS = speedMS;
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
