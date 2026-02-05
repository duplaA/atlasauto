using UnityEngine;

/// <summary>
/// FH5-style tyre friction: peak at tiny slip (3-6° lateral, 0.05-0.12 long), sharp drop-off past peak.
/// </summary>
public class VehicleTyres : MonoBehaviour
{
    [Header("Longitudinal (Accel/Brake)")]
    [Tooltip("Slip at peak longitudinal force (FH5: 0.05-0.12; drops sharply past peak)")]
    [Range(0.05f, 0.2f)] public float longitudinalPeakSlip = 0.08f;
    [Tooltip("Peak longitudinal grip multiplier")]
    [Range(1f, 2.5f)] public float longitudinalPeakValue = 1.5f;
    [Tooltip("Grip at high slip (drops sharply past peak)")]
    [Range(0.5f, 1.2f)] public float longitudinalAsymptoteValue = 0.9f;
    [Tooltip("Base stiffness for forward friction")]
    [Range(1f, 3f)] public float longitudinalStiffness = 2f;

    [Header("Lateral (Cornering)")]
    [Tooltip("Peak slip angle in degrees (FH5: 3-6; tiny slip = max lateral grip).")]
    [Range(3f, 8f)] public float peakSlipAngleDeg = 4f;
    [Tooltip("Peak lateral grip multiplier")]
    [Range(1.2f, 2.2f)] public float lateralPeakValue = 1.8f;
    [Tooltip("Lateral grip at high slip (falls gradually)")]
    [Range(0.7f, 1.3f)] public float lateralAsymptoteValue = 1f;
    [Tooltip("Base stiffness for sideways friction")]
    [Range(1.5f, 3.5f)] public float lateralStiffness = 2.5f;

    [Header("RWD Rear Slip Multiplier")]
    [Tooltip("Rear wheel slip/grip multiplier for RWD (1.15-1.35 typical). 1 = no extra.")]
    [Range(1f, 1.5f)] public float rwdRearSlipMultiplier = 1.2f;

    [Header("Drivetrain (set by VehicleController or inspector)")]
    public VehicleController.DrivetrainType drivetrain = VehicleController.DrivetrainType.RWD;

    [Header("Tire Temperature (optional FH5-style)")]
    public bool enableTireTemperature = false;
    [Tooltip("Optimal temp range 75-120 C (170-250 F).")]
    [Range(60f, 140f)] public float optimalTempC = 95f;
    [Range(10f, 40f)] public float ambientTempC = 25f;
    [Tooltip("Heat rate from slip * speed.")]
    public float heatRatePerSlip = 2f;
    [Tooltip("Cooling toward ambient per fixed step.")]
    [Range(0.001f, 0.1f)] public float coolingRate = 0.02f;
    private float[] tireTempC = new float[8];
    private bool tempsInitialized;

    [Header("Tire Pressure (optional FH5-style)")]
    public bool enableTirePressure = false;
    [Tooltip("Optimal warm 32-34 PSI (2.2-2.35 bar).")]
    [Range(24f, 40f)] public float tirePressurePsi = 32f;
    [Tooltip("Grip base = pressure/33 (cap 0.9-1.1).")]
    public float GripBaseMultiplierFromPressure => enableTirePressure ? Mathf.Clamp(tirePressurePsi / 33f, 0.9f, 1.1f) : 1f;
    [Tooltip("Responsiveness = pressure/30 (higher = sharper turn-in).")]
    public float ResponsivenessFromPressure => enableTirePressure ? Mathf.Clamp(tirePressurePsi / 30f, 0.8f, 1.2f) : 1f;

    /// <summary>Convert slip angle (degrees) to approximate slip ratio for curve.</summary>
    public static float SlipAngleToSlipRatio(float degrees)
    {
        return Mathf.Tan(degrees * Mathf.Deg2Rad);
    }

    public void UpdateTemperatures(VehicleWheel[] wheels, float averageSlipRatio, float speedNorm)
    {
        if (!enableTireTemperature || wheels == null) return;
        if (tireTempC.Length < wheels.Length) System.Array.Resize(ref tireTempC, wheels.Length);
        if (!tempsInitialized) { for (int i = 0; i < tireTempC.Length; i++) tireTempC[i] = ambientTempC; tempsInitialized = true; }
        for (int i = 0; i < wheels.Length; i++)
        {
            float heat = averageSlipRatio * speedNorm * heatRatePerSlip * 0.1f;
            tireTempC[i] += heat;
            tireTempC[i] = Mathf.Lerp(tireTempC[i], ambientTempC, coolingRate);
            tireTempC[i] = Mathf.Clamp(tireTempC[i], ambientTempC - 10f, 150f);
        }
    }

    public float GetGripMultiplierFromTemp(int wheelIndex)
    {
        if (!enableTireTemperature || wheelIndex >= tireTempC.Length) return 1f;
        float g = 1f - Mathf.Abs(tireTempC[wheelIndex] - optimalTempC) / 50f * 0.3f;
        return Mathf.Clamp(g, 0.7f, 1f);
    }

    public void UpdateFriction(VehicleWheel[] wheels)
    {
        if (wheels == null) return;

        float lateralExtremumSlip = SlipAngleToSlipRatio(peakSlipAngleDeg);
        bool isRWD = drivetrain == VehicleController.DrivetrainType.RWD;

        for (int i = 0; i < wheels.Length; i++)
        {
            var w = wheels[i];
            if (w.wheelCollider == null) continue;

            float rearMult = (isRWD && !w.isFront) ? rwdRearSlipMultiplier : 1f;
            float tempMult = GetGripMultiplierFromTemp(i);
            float pressureMult = GripBaseMultiplierFromPressure;
            float resp = ResponsivenessFromPressure;

            WheelFrictionCurve fwd = w.wheelCollider.forwardFriction;
            fwd.extremumSlip = longitudinalPeakSlip;
            fwd.extremumValue = (longitudinalPeakValue * rearMult) * tempMult * pressureMult;
            fwd.asymptoteValue = longitudinalAsymptoteValue;
            w.wheelCollider.forwardFriction = fwd;

            WheelFrictionCurve side = w.wheelCollider.sidewaysFriction;
            side.extremumSlip = lateralExtremumSlip;
            side.extremumValue = (lateralPeakValue * rearMult) * tempMult * pressureMult;
            side.asymptoteValue = lateralAsymptoteValue;
            w.wheelCollider.sidewaysFriction = side;
        }
    }
}
