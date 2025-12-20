using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class VehicleController : MonoBehaviour
{
    public enum VehiclePreset { HeavyVan, DeliveryTruck, FamilySedan, SportSedan, Sportscar, RaceCar, Custom }

    [Header("Vehicle Type")]
    public VehiclePreset vehiclePreset = VehiclePreset.FamilySedan;

    [Header("Components")]
    public VehicleEngine engine;
    public VehicleTransmission transmission;
    public VehicleBrakes brakes;
    public VehicleSteering steering;
    public VehicleSuspension suspension;
    public VehicleTyres tyres;
    public VehicleAerodynamics aerodynamics;

    [Header("Wheels")]
    public VehicleWheel[] wheels;
    
    [Header("Properties")]
    public float mass = 1400f;
    public float cogOffset = -0.35f;
    public float maxReverseSpeed = 10f;

    [Header("Traction Control")]
    public bool enableTC = true;
    public float tcSlipLimit = 0.25f;

    private Rigidbody _rb;
    private VehicleControls carControls;
    private Vector2 input;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        
        if (!engine) engine = GetComponent<VehicleEngine>() ?? gameObject.AddComponent<VehicleEngine>();
        if (!transmission) transmission = GetComponent<VehicleTransmission>() ?? gameObject.AddComponent<VehicleTransmission>();
        if (!brakes) brakes = GetComponent<VehicleBrakes>() ?? gameObject.AddComponent<VehicleBrakes>();
        if (!steering) steering = GetComponent<VehicleSteering>() ?? gameObject.AddComponent<VehicleSteering>();
        if (!suspension) suspension = GetComponent<VehicleSuspension>() ?? gameObject.AddComponent<VehicleSuspension>();
        if (!tyres) tyres = GetComponent<VehicleTyres>() ?? gameObject.AddComponent<VehicleTyres>();
        if (!aerodynamics) aerodynamics = GetComponent<VehicleAerodynamics>() ?? gameObject.AddComponent<VehicleAerodynamics>();

        DiscoverWheels();

        carControls = new VehicleControls();
        carControls.Vehicle.Move.performed += ctx => input = ctx.ReadValue<Vector2>();
        carControls.Vehicle.Move.canceled += ctx => input = Vector2.zero;
        
        ApplyPreset();
    }

    void DiscoverWheels()
    {
        // Only auto-discover if the array is empty or null
        if (wheels != null && wheels.Length > 0) return;
        
        var wheelColliders = GetComponentsInChildren<WheelCollider>();
        if (wheelColliders.Length == 0)
        {
            Debug.LogWarning("[VehicleController] No WheelColliders found in children.");
            return;
        }

        wheels = new VehicleWheel[wheelColliders.Length];
        for (int i = 0; i < wheelColliders.Length; i++)
        {
            var wc = wheelColliders[i];
            var vw = wc.gameObject.GetComponent<VehicleWheel>() ?? wc.gameObject.AddComponent<VehicleWheel>();
            vw.wheelCollider = wc;
            
            if (vw.wheelVisual == null && wc.transform.childCount > 0)
                vw.wheelVisual = wc.transform.GetChild(0);
            
            // Default steering/motor logic based on position relative to root
            // Using a small epsilon to avoid float inaccuracies
            bool isFront = transform.InverseTransformPoint(wc.transform.position).z > 0.1f;
            vw.isSteer = isFront;
            vw.isMotor = !isFront;
            wheels[i] = vw;
        }
    }

    void Start()
    {
        if (_rb != null)
        {
            _rb.mass = mass;
            _rb.centerOfMass = new Vector3(0, cogOffset, 0);
        }
        suspension?.UpdateSuspension(wheels);
    }

    void OnEnable() { carControls?.Enable(); }
    void OnDisable() { carControls?.Disable(); }

    void FixedUpdate()
    {
        if (wheels == null || wheels.Length == 0 || _rb == null) return;
        
        float dt = Time.fixedDeltaTime;
        float throttle = Mathf.Clamp01(input.y);
        float brakeInput = Mathf.Clamp01(-input.y);
        float steerInput = input.x;
        
        Vector3 localVel = transform.InverseTransformDirection(_rb.linearVelocity);
        float forwardSpeed = localVel.z;
        float absSpeed = Mathf.Abs(forwardSpeed);

        // === STEERING ===
        steering?.ApplyToWheels(wheels, steerInput, forwardSpeed);

        // === ANTI-ROLL BARS ===
        suspension?.ApplyAntiRoll(_rb);

        // === BRAKES ===
        float brakeTorque = (brakes != null && brakeInput > 0.01f) ? brakes.maxBrakeTorque * brakeInput : 0f;
        foreach (var w in wheels)
            if (w != null) w.ApplyBrake(brakeTorque);

        // === TRANSMISSION ===
        transmission.HandleInput(input.y, forwardSpeed);
        transmission.UpdateTransmission(dt, engine.currentRPM, forwardSpeed, throttle, brakeInput > 0.1f);

        int gear = transmission.currentGear;
        float gearRatio = transmission.GetCurrentRatio();
        float totalRatio = transmission.GetTotalRatio();
        float clutchPos = transmission.clutchPosition;

        // === ENGINE RPM ===
        float avgWheelRPM = GetDrivenWheelRPM();
        float demandedRPM = Mathf.Abs(avgWheelRPM * gearRatio * transmission.finalDriveRatio);
        demandedRPM = Mathf.Max(demandedRPM, engine.idleRPM);
        
        if (gear == 0)
        {
            engine.FreeRev(throttle, dt);
        }
        else if (clutchPos > 0.9f)
        {
            engine.SetRPMFromWheels(avgWheelRPM, gearRatio, transmission.finalDriveRatio);
        }
        else
        {
            // Partial engagement: blend free rev toward wheel demand
            engine.FreeRev(throttle, dt);
            float currentRPM = engine.currentRPM;
            float targetRPM = Mathf.Lerp(currentRPM, demandedRPM, clutchPos);
            engine.currentRPM = Mathf.Lerp(engine.currentRPM, targetRPM, 10f * dt);
        }

        // === TORQUE ===
        float engineTorque = engine.GetTorque(throttle);
        
        // === APPLY TO WHEELS ===
        int numDriven = 0;
        foreach (var w in wheels) if (w != null && w.isMotor) numDriven++;

        if (numDriven > 0 && gear != 0 && brakeTorque < 50f)
        {
            float totalAvailableTorque = engineTorque * Mathf.Abs(totalRatio) * transmission.efficiency * clutchPos;
            float wheelTorque = totalAvailableTorque / numDriven;
            
            if (gear == -1) wheelTorque = -wheelTorque;
            
            if (gear == -1 && absSpeed > maxReverseSpeed * 0.8f)
                wheelTorque *= Mathf.Max(0f, 1f - (absSpeed - maxReverseSpeed * 0.8f) / (maxReverseSpeed * 0.3f));

            foreach (var w in wheels)
            {
                if (w != null && w.isMotor)
                {
                    float finalTorque = wheelTorque;
                    
                    if (enableTC)
                    {
                        WheelHit hit;
                        if (w.IsGrounded(out hit) && Mathf.Abs(hit.forwardSlip) > tcSlipLimit)
                        {
                            float over = Mathf.Abs(hit.forwardSlip) - tcSlipLimit;
                            finalTorque *= Mathf.Clamp01(1f - over * 3f);
                        }
                    }
                    
                    w.ApplyTorque(finalTorque);
                }
                else if (w != null)
                {
                    w.ApplyTorque(0f);
                }
            }
        }
        else
        {
            foreach (var w in wheels)
                if (w != null) w.ApplyTorque(0f);
        }

        // === TYRES ===
        tyres?.UpdateFriction(wheels, (mass / wheels.Length) * 9.81f);

        // === AERO ===
        aerodynamics?.ApplyAerodynamics(_rb);
    }

    void Update()
    {
        if (wheels == null) return;
        foreach (var w in wheels)
            if (w != null) w.SyncVisuals();
    }

    float GetDrivenWheelRPM()
    {
        float sum = 0f;
        int count = 0;
        foreach (var w in wheels)
        {
            if (w != null && w.isMotor)
            {
                sum += Mathf.Abs(w.GetRPM());
                count++;
            }
        }
        return count > 0 ? sum / count : 0f;
    }

    void ApplyPreset()
    {
        switch (vehiclePreset)
        {
            case VehiclePreset.HeavyVan:
                mass = 2200f; cogOffset = -0.25f; maxReverseSpeed = 8f;
                engine?.Configure(180, 3500, 400, 2000, 700, 5000, 0.3f, 20f);
                transmission?.Configure(new[] { 4.0f, 2.5f, 1.6f, 1.0f, 0.75f }, 4.1f, 0.88f, 4200, 1800, 0, 0, 0, 0);
                if (brakes) brakes.maxBrakeTorque = 4000f;
                if (steering) { steering.maxSteerAngle = 35f; steering.steerAngleAtMaxSpeed = 15f; }
                suspension?.Configure(mass, 0.25f, VehicleSuspension.SpringFrequency.Comfort, VehicleSuspension.DampRatio.Comfort, VehicleSuspension.FrontRearBias.SixtyForty);
                break;
                
            case VehiclePreset.FamilySedan:
                mass = 1400f; cogOffset = -0.35f; maxReverseSpeed = 10f;
                engine?.Configure(160, 5500, 220, 3500, 750, 6500, 0.2f, 12f);
                transmission?.Configure(new[] { 3.5f, 2.0f, 1.4f, 1.0f, 0.8f, 0.65f }, 3.7f, 0.92f, 5800, 2000, 0, 0, 0, 0);
                if (brakes) brakes.maxBrakeTorque = 3000f;
                if (steering) { steering.maxSteerAngle = 32f; steering.steerAngleAtMaxSpeed = 10f; }
                suspension?.Configure(mass, 0.18f, VehicleSuspension.SpringFrequency.Comfort, VehicleSuspension.DampRatio.Comfort, VehicleSuspension.FrontRearBias.SixtyForty);
                break;
                
            case VehiclePreset.Sportscar:
                mass = 1350f; cogOffset = -0.45f; maxReverseSpeed = 12f;
                engine?.Configure(400, 7000, 450, 5000, 900, 8000, 0.15f, 8f);
                transmission?.Configure(new[] { 3.2f, 2.1f, 1.5f, 1.15f, 0.9f, 0.75f }, 3.4f, 0.94f, 7200, 3500, 0, 0, 0, 0);
                if (brakes) brakes.maxBrakeTorque = 4500f;
                if (steering) { steering.maxSteerAngle = 28f; steering.steerAngleAtMaxSpeed = 6f; }
                suspension?.Configure(mass, 0.12f, VehicleSuspension.SpringFrequency.Sport, VehicleSuspension.DampRatio.Sport, VehicleSuspension.FrontRearBias.FortySixty);
                break;
                
            case VehiclePreset.RaceCar:
                mass = 1100f; cogOffset = -0.5f; maxReverseSpeed = 15f;
                engine?.Configure(550, 8000, 550, 6500, 1100, 9000, 0.12f, 6f);
                transmission?.Configure(new[] { 2.9f, 2.0f, 1.5f, 1.2f, 1.0f, 0.85f }, 3.2f, 0.95f, 8200, 4500, 0, 0, 0, 0);
                if (brakes) brakes.maxBrakeTorque = 6000f;
                if (steering) { steering.maxSteerAngle = 24f; steering.steerAngleAtMaxSpeed = 5f; }
                suspension?.Configure(mass, 0.1f, VehicleSuspension.SpringFrequency.Sport, VehicleSuspension.DampRatio.Sport, VehicleSuspension.FrontRearBias.FiftyFifty);
                break;
        }
        
        if (_rb != null)
        {
            _rb.mass = mass;
            _rb.centerOfMass = new Vector3(0, cogOffset, 0);
        }
    }

    void OnGUI()
    {
        if (engine == null || transmission == null || _rb == null) return;
        
        var style = new GUIStyle { fontSize = 16 };
        style.normal.textColor = Color.white;

        float speedKMH = transform.InverseTransformDirection(_rb.linearVelocity).z * 3.6f;
        string gear = transmission.currentGear == -1 ? "R" : transmission.currentGear == 0 ? "N" : transmission.currentGear.ToString();
        int clutchPercent = Mathf.RoundToInt(transmission.clutchPosition * 100f);

        int y = 10;
        GUI.Label(new Rect(10, y, 500, 25), $"RPM: {engine.currentRPM:F0} / {engine.maxRPM:F0}", style); y += 25;
        GUI.Label(new Rect(10, y, 500, 25), $"Gear: {gear}  |  Clutch: {clutchPercent}%", style); y += 25;
        GUI.Label(new Rect(10, y, 500, 25), $"Speed: {speedKMH:F0} km/h", style); y += 25;
    }
}