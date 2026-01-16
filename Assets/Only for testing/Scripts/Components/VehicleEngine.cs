using UnityEngine;

/// <summary>
/// Stateless engine torque calculator.
/// Follows strict causality: Input RPM + Throttle -> Output Torque.
/// Supports both ICE and EV.
/// </summary>
public class VehicleEngine : MonoBehaviour
{
    public enum EngineType { InternalCombustion, Electric }

    [Header("Engine Type")]
    public EngineType engineType = EngineType.InternalCombustion;

    [Header("Power Output")]
    [Tooltip("Peak power in Horsepower (HP). Synced with kW.")]
    public float horsepowerHP = 400f;
    [Tooltip("Peak power in Kilowatts (kW). Auto-calculated from HP.")]
    public float maxPowerKW = 300f;

    [Header("Torque")]
    [Tooltip("Peak torque in Nm (ICE peak or EV constant torque region)")]
    public float peakTorqueNm = 450f;
    [Tooltip("Maximum mechanical RPM")]
    public float maxRPM = 7500f;
    [Tooltip("Engine inertia (kg*m^2). Only affects free-revving response.")]
    public float inertia = 0.2f;

    [Header("Internal Combustion")]
    [Tooltip("RPM where peak torque is reached")]
    public float peakTorqueRPM = 4500f;
    [Tooltip("RPM where peak power is reached (Torque begins to drop significantly)")]
    public float peakPowerRPM = 6000f;
    [Tooltip("Idle RPM")]
    public float idleRPM = 850f;
    
    // Auto-generated curve based on peaks
    private AnimationCurve proceduralTorqueCurve;

    [Header("Electric Vehicle")]
    [Tooltip("RPM where motor transitions from Constant Torque to Constant Power (Base Speed)")]
    public float evBaseRPM = 3000f;

    [Header("Friction")]
    public float frictionTorque = 10f; // Reduced constant drag
    public float brakingTorque = 40f; // Engine braking at 0 throttle

    [Header("State")]
    public float currentRPM;
    public float currentLoad; // 0..1, for UI/Sound

    // Conversion constants
    private const float HP_TO_KW = 0.7457f;
    private const float KW_TO_HP = 1.341f;

    void OnValidate()
    {
        // Sync HP <-> kW (HP is the primary input, kW is derived)
        maxPowerKW = horsepowerHP * HP_TO_KW;
    }
    
    void Awake()
    {
        // Ensure sync
        maxPowerKW = horsepowerHP * HP_TO_KW;
        GenerateTorqueCurve();
    }
    
    void GenerateTorqueCurve()
    {
        // Generate a realistic torque curve that actually reaches peak values
        proceduralTorqueCurve = new AnimationCurve();
        
        // Idle: 80% torque (engines make good torque even at idle)
        proceduralTorqueCurve.AddKey(new Keyframe(0f, 0.8f)); 
        
        // Peak Torque RPM: 100% torque
        float peakTorqueNorm = peakTorqueRPM / maxRPM;
        proceduralTorqueCurve.AddKey(new Keyframe(peakTorqueNorm, 1.0f));
        
        // Peak Power RPM: We want to hit exactly 'horsepowerHP' here
        // RequiredTorque = (PowerKW * 9549) / RPM
        // CurveFactor = RequiredTorque / peakTorqueNm 
        float requiredTorqueForPeakHP = (maxPowerKW * 1000f * 9.549f) / Mathf.Max(peakPowerRPM, 1f);
        float powerPointFactor = requiredTorqueForPeakHP / Mathf.Max(peakTorqueNm, 1f);
        
        // Allow curve to go above 1.0 if the HP/Torque config requires it at peak power RPM
        // This ensures we actually deliver the configured HP
        powerPointFactor = Mathf.Max(powerPointFactor, 0.85f); // At minimum 85% at peak power
        float peakPowerNorm = peakPowerRPM / maxRPM;
        proceduralTorqueCurve.AddKey(new Keyframe(peakPowerNorm, powerPointFactor)); 
        
        // Redline: Maintain decent power (80% for aggressive feel)
        proceduralTorqueCurve.AddKey(new Keyframe(1.0f, 0.8f));
        
        // Smooth tangents for realistic curve
        for (int i = 0; i < proceduralTorqueCurve.length; i++) 
            proceduralTorqueCurve.SmoothTangents(i, 0f);            
        Debug.Log($"[VehicleEngine] Generated Torque Curve. Peak Torque @ {peakTorqueRPM}, Peak Power Factor {powerPointFactor:F2} @ {peakPowerRPM}");
    }


    /// Calculates the instantaneous torque available at the flywheel.
    /// Pure function: depends only on current state, does not modify state.
    public float CalculateTorque(float currentRPM, float throttle)
    {
        currentRPM = Mathf.Abs(currentRPM); // Handle reverse RPM naturally
        float availableTorque = 0f;

        if (engineType == EngineType.InternalCombustion)
        {
            // ICE Calculation: Procedural Curve based
            if (proceduralTorqueCurve == null) GenerateTorqueCurve();
            
            float effectiveRPM = Mathf.Max(currentRPM, idleRPM);
            float normalizedRPM = Mathf.Clamp01(effectiveRPM / maxRPM);
            
            float curveFactor = proceduralTorqueCurve.Evaluate(normalizedRPM);
            availableTorque = peakTorqueNm * curveFactor * throttle;
        }
        else
        {
            // EV Calculation: Dynamic Crossover
            // We calculate the natural RPM where Peak Torque intersects Peak Power.
            // CrossoverRPM = (PowerKW * 9549) / TorqueNm
            float crossoverRPM = (maxPowerKW * 1000f * 9.549f) / Mathf.Max(peakTorqueNm, 1f);
            
            // Update the debug value if needed (optional, or just ignore the field)
            // evBaseRPM = crossoverRPM; 

            if (currentRPM < crossoverRPM)
            {
                // Constant Torque Region
                availableTorque = peakTorqueNm * throttle;
            }
            else
            {
                // Constant Power Region
                // Torque = Power / RPM
                float powerLimitTorque = (maxPowerKW * 1000f * 9.549f) / Mathf.Max(currentRPM, 1f);
                availableTorque = powerLimitTorque * throttle;
            }
        }

        // Apply friction/pumping losses
        // Realism: Pumping losses increase with RPM squared
        float rpmFactor = currentRPM / maxRPM;
        float pumpingLoss = frictionTorque * (1f + (rpmFactor * rpmFactor * 2f));
        
        // Engine Braking: Stronger at high RPM, zero at idle
        // Only applies when off-throttle
        float offThrottleBraking = 0f;
        if (throttle < 0.05f)
        {
            offThrottleBraking = brakingTorque * rpmFactor * 1.5f; 
        }

        float drag = pumpingLoss + offThrottleBraking;
        float netTorque = availableTorque - drag;
        
        // If ON throttle, we shouldn't be fighting "brakingTorque", only friction
        // But friction should always exist.
        if (throttle > 0.1f) 
        {
            netTorque = Mathf.Max(netTorque, -pumpingLoss); // allow small negative for friction
        }
        
        // REV LIMITER: Cut torque when approaching redline
        // This prevents acceleration past maxRPM in manual mode
        float revLimiterThreshold = 0.95f; // Start limiting at 95% of maxRPM
        float rpmRatio = currentRPM / maxRPM;
        if (rpmRatio > revLimiterThreshold)
        {
            // Linear dropoff from 95% to 100% RPM
            float limiterFactor = 1f - ((rpmRatio - revLimiterThreshold) / (1f - revLimiterThreshold));
            limiterFactor = Mathf.Clamp01(limiterFactor);
            netTorque *= limiterFactor;
        }
        
        return netTorque;
    }

    /// Returns the maximum power (kW) currently being produced.
    /// Used for UI/Telemetry.
    public float GetCurrentPowerKW(float torqueNm, float currentRPM)
    {
        return (torqueNm * currentRPM) / 9549f;
    }
}