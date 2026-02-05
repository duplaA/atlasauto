using UnityEngine;

/// <summary>
/// Digital twin/Telemetry hub for the vehicle.
/// Provides a unified interface for external systems to read state and send commands.
/// Automatically added to any vehicle with a VehicleController.
/// </summary>
public class VehicleDataLink : MonoBehaviour
{
    private VehicleController vc;
    private VehicleEngine engine;
    private VehicleTransmission transmission;
    private VehicleSpeedSense speedSense;
    private VehicleGForceCalculator gForce;

    void Awake()
    {
        Initialize();
    }

    private void Initialize()
    {
        if (vc == null) vc = GetComponent<VehicleController>();
        if (vc != null)
        {
            engine = vc.engine;
            transmission = vc.transmission;
        }
        if (speedSense == null) speedSense = GetComponent<VehicleSpeedSense>();
        if (speedSense == null && vc != null) speedSense = vc.GetComponent<VehicleSpeedSense>();
        if (gForce == null) gForce = GetComponent<VehicleGForceCalculator>();
        if (gForce == null && vc != null) gForce = vc.GetComponent<VehicleGForceCalculator>();
    }

    #region Telemetry (Read-Only)

    // Dynamics
    public float SpeedKMH => vc != null ? vc.speedKMH : 0f;
    public float SpeedMS => vc != null ? vc.speedMS : 0f;
    public bool IsBraking => vc != null ? vc.isCurrentlyBraking : false;
    public float SlipRatio => vc != null ? vc.slipRatio : 0f;
    public float MaxSlipRatio => vc != null ? vc.maxSlipRatio : 0f;
    public float SpeedNorm => speedSense != null ? speedSense.SpeedNorm : (vc != null && vc.topSpeedKMH > 0.01f ? Mathf.Clamp01(vc.speedKMH / vc.topSpeedKMH) : 0f);
    public float LateralG => gForce != null ? gForce.LateralG : 0f;
    public float LongitudinalG => gForce != null ? gForce.LongitudinalG : 0f;
    public float AverageSuspensionDeflection => vc != null ? vc.averageSuspensionDeflection : 0f;

    // Engine
    public float EngineRPM => engine != null ? engine.currentRPM : 0f;
    public float EngineMaxRPM => engine != null ? engine.maxRPM : 0f;
    public float EngineTorqueNm => vc != null ? vc.engineTorque : 0f;
    public float HorsepowerHP => engine != null ? (engine.GetCurrentPowerKW(vc.engineTorque, engine.currentRPM) * 1.341f) : 0f;
    public bool IsElectric => vc != null ? vc.isElectricVehicle : false;

    // Transmission
    public int CurrentGear => transmission != null ? transmission.currentGear : 0;
    public string GearDisplay => transmission != null ? transmission.GetGearDisplayString() : "N";
    public float ClutchEngagement => transmission != null ? transmission.clutchEngagement : 0f;

    // Input States
    public float ThrottleInput => vc != null ? vc.currentThrottle : 0f;
    public float BrakeInput => vc != null ? vc.currentBrake : 0f;
    public float SteerInput => vc != null ? vc.currentSteer : 0f;

    #endregion

    #region Configuration & Control

    // Drivetrain
    public VehicleController.DrivetrainType Drivetrain => vc != null ? vc.drivetrain : VehicleController.DrivetrainType.RWD;
    
    public void SetDrivetrain(VehicleController.DrivetrainType type)
    {
        if (vc != null) vc.SetDrivetrain(type);
    }

    // Performance limits
    public float TopSpeedKMH => vc != null ? vc.topSpeedKMH : 0f;
    public void SetTopSpeed(float kmh) { if (vc != null) vc.topSpeedKMH = kmh; }

    public float MaxSteerAngle => vc != null ? vc.maxSteerAngle : 0f;
    public void SetMaxSteerAngle(float angle) { if (vc != null) vc.maxSteerAngle = angle; }

    // Physical state
    public float VehicleMass => vc != null ? vc.vehicleMass : 0f;
    public void SetMass(float mass) 
    { 
        if (vc != null) 
        {
            vc.vehicleMass = mass;
            Rigidbody rb = vc.GetComponent<Rigidbody>();
            if (rb != null) rb.mass = mass;
        }
    }

    /// <summary>
    /// Overrides standard player input (Keyboard/Controller) with custom values.
    /// </summary>
    /// <param name="input">Vector2 where x = steer (-1..1), y = throttle/brake (-1..1)</param>
    /// <param name="active">Whether the override is active</param>
    public void SetExternalControl(Vector2 input, bool active)
    {
        if (vc != null) vc.SetInputOverride(input, active);
    }

    #endregion

    void OnValidate()
    {
        Initialize();
    }

    void Start()
    {
        Initialize();
    }
}
