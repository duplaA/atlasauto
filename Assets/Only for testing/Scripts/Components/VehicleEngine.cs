using UnityEngine;


/// Simple, stable engine model.
/// RPM is directly tied to wheel speed when in gear and moving.

public class VehicleEngine : MonoBehaviour
{
    [Header("Specifications")]
    public float peakTorqueNm = 220f;
    public float peakTorqueRPM = 3500f;
    public float peakPowerHP = 160f;
    public float peakPowerRPM = 5500f;
    public float idleRPM = 750f;
    public float maxRPM = 6500f;
    public float inertia = 0.2f;
    public float friction = 12f;

    [Header("State")]
    public float currentRPM;
    public float generatedTorque;

    private AnimationCurve torqueCurve;

    void Awake()
    {
        BuildTorqueCurve();
        currentRPM = idleRPM;
    }

    public void Configure(float powerHP, float powerRPM, float torqueNm, float torqueRPM,
                          float idle, float max, float inertiaVal, float frictionVal)
    {
        peakPowerHP = powerHP;
        peakPowerRPM = powerRPM;
        peakTorqueNm = torqueNm;
        peakTorqueRPM = torqueRPM;
        idleRPM = idle;
        maxRPM = max;
        inertia = Mathf.Max(0.1f, inertiaVal);
        friction = frictionVal;
        BuildTorqueCurve();
        currentRPM = idleRPM;
    }

    void BuildTorqueCurve()
    {
        torqueCurve = new AnimationCurve();
        
        float torqueAtPower = (peakPowerHP * 745.7f * 60f) / (2f * Mathf.PI * peakPowerRPM);
        
        torqueCurve.AddKey(idleRPM * 0.5f, peakTorqueNm * 0.2f);
        torqueCurve.AddKey(idleRPM, peakTorqueNm * 0.45f);
        torqueCurve.AddKey((idleRPM + peakTorqueRPM) * 0.5f, peakTorqueNm * 0.85f);
        torqueCurve.AddKey(peakTorqueRPM, peakTorqueNm);
        torqueCurve.AddKey((peakTorqueRPM + peakPowerRPM) * 0.5f, (peakTorqueNm + torqueAtPower) * 0.5f);
        torqueCurve.AddKey(peakPowerRPM, torqueAtPower);
        torqueCurve.AddKey(maxRPM, torqueAtPower * 0.75f);
        
        for (int i = 0; i < torqueCurve.length; i++)
            torqueCurve.SmoothTangents(i, 0.5f);
    }

    public float GetMaxTorque(float rpm)
    {
        if (torqueCurve == null) BuildTorqueCurve();
        return torqueCurve.Evaluate(Mathf.Clamp(rpm, idleRPM * 0.5f, maxRPM));
    }

    
    /// Simple RPM update: Engine RPM follows wheel speed when clutch is engaged.
    
    public void SetRPMFromWheels(float wheelRPM, float gearRatio, float finalDrive)
    {
        float targetRPM = Mathf.Abs(wheelRPM * gearRatio * finalDrive);
        targetRPM = Mathf.Max(targetRPM, idleRPM);  // Never below idle
        targetRPM = Mathf.Min(targetRPM, maxRPM);   // Never above redline
        
        // Smooth transition (simulates inertia)
        currentRPM = Mathf.Lerp(currentRPM, targetRPM, 15f * Time.fixedDeltaTime);
    }

    
    /// Free rev when clutch is disengaged (standstill or neutral).
    
    public void FreeRev(float throttle, float dt)
    {
        float targetTorque = GetMaxTorque(currentRPM) * throttle;
        float netTorque = targetTorque - friction;
        
        float alpha = netTorque / inertia;
        float deltaRPM = alpha * (30f / Mathf.PI) * dt;
        
        currentRPM += deltaRPM;
        
        // Idle hold
        if (throttle < 0.05f && currentRPM < idleRPM)
            currentRPM = Mathf.Lerp(currentRPM, idleRPM, 5f * dt);
        
        currentRPM = Mathf.Clamp(currentRPM, idleRPM * 0.7f, maxRPM);
    }

    
    /// Calculate torque at current RPM and throttle.
    
    public float GetTorque(float throttle)
    {
        generatedTorque = GetMaxTorque(currentRPM) * Mathf.Clamp01(throttle);
        return generatedTorque;
    }
}
