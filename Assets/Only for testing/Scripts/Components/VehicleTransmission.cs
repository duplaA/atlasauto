using UnityEngine;


/// Transmission with progressive clutch that allows smooth launches.

public class VehicleTransmission : MonoBehaviour
{
    [Header("Gears")]
    public float[] gearRatios = { 3.5f, 2.0f, 1.4f, 1.0f, 0.8f };
    public float reverseRatio = 3.5f;
    public float finalDriveRatio = 3.7f;
    public float efficiency = 0.92f;

    [Header("Shifting")]
    public float upshiftRPM = 5800f;
    public float downshiftRPM = 2000f;
    public float shiftTime = 0.3f;
    public float minSpeedToShift = 3f;

    [Header("Clutch")]
    [Range(0f, 1f)]
    public float clutchPosition = 0f;  // 0 = fully open, 1 = fully locked
    
    [Header("State")]
    public int currentGear = 0;

    private float shiftTimer = 0f;
    private float lastShiftTime = -10f;

    // Properties for compatibility
    public bool clutchEngaged => clutchPosition > 0.9f;
    public float clutchEngagement => clutchPosition;
    public float clutchPressure => clutchPosition;
    public float clutchSlip => 1f - clutchPosition;

    void Start()
    {
        currentGear = 0;
        clutchPosition = 0f;
    }

    public void Configure(float[] gears, float finalDrive, float eff, float upRPM, float downRPM,
                          float maxClutch, float rampRate, float biteMin, float biteMax)
    {
        if (gears != null && gears.Length > 0)
            gearRatios = gears;
        finalDriveRatio = finalDrive;
        efficiency = eff;
        upshiftRPM = upRPM;
        downshiftRPM = downRPM;
    }

    public float GetCurrentRatio()
    {
        if (currentGear > 0 && currentGear <= gearRatios.Length)
            return gearRatios[currentGear - 1];
        if (currentGear == -1)
            return reverseRatio;
        return 0f;
    }

    public float GetTotalRatio()
    {
        float ratio = GetCurrentRatio() * finalDriveRatio;
        if (currentGear == -1) ratio = -ratio;
        return ratio;
    }

    public void HandleInput(float vInput, float speed)
    {
        if (shiftTimer > 0) return;

        bool stopped = Mathf.Abs(speed) < 0.5f;

        if (vInput < -0.1f && stopped && currentGear != -1)
        {
            currentGear = -1;
            shiftTimer = shiftTime;
        }
        else if (vInput > 0.1f && currentGear <= 0)
        {
            currentGear = 1;
            shiftTimer = shiftTime;
        }
    }

    public void UpdateTransmission(float dt, float engineRPM, float speed, float throttle, bool braking)
    {
        if (shiftTimer > 0)
        {
            shiftTimer -= dt;
            // During shift, partially disengage clutch
            clutchPosition = Mathf.MoveTowards(clutchPosition, 0.3f, 8f * dt);
            return;
        }

        float absSpeed = Mathf.Abs(speed);

        // === CLUTCH LOGIC ===
        // Progressive engagement based on speed AND throttle
        float targetClutch;
        
        if (currentGear == 0)
        {
            targetClutch = 0f;  // Neutral = open
        }
        else if (braking && absSpeed < 2f)
        {
            targetClutch = 0.2f;  // Open when braking to stop
        }
        else if (absSpeed < 1f)
        {
            // Standstill: clutch position based on throttle
            // More throttle = more clutch = more torque to wheels
            // This allows engine to rev a bit while still transmitting torque
            targetClutch = 0.3f + throttle * 0.4f;  // 0.3 to 0.7
        }
        else if (absSpeed < 5f)
        {
            // Low speed: progressive lockup
            float speedFactor = Mathf.InverseLerp(1f, 5f, absSpeed);
            float baseClutch = 0.5f + throttle * 0.3f;
            targetClutch = Mathf.Lerp(baseClutch, 1f, speedFactor);
        }
        else
        {
            // Normal driving: fully locked
            targetClutch = 1f;
        }

        // Smooth clutch movement
        float clutchSpeed = 4f;  // Takes ~0.25s to fully engage
        clutchPosition = Mathf.MoveTowards(clutchPosition, targetClutch, clutchSpeed * dt);
        clutchPosition = Mathf.Clamp01(clutchPosition);

        // === AUTO-SHIFTING ===
        if (currentGear >= 1 && !braking && throttle > 0.2f && Time.time - lastShiftTime > 1f)
        {
            if (engineRPM > upshiftRPM && currentGear < gearRatios.Length && absSpeed > minSpeedToShift)
            {
                currentGear++;
                shiftTimer = shiftTime;
                lastShiftTime = Time.time;
            }
            else if (engineRPM < downshiftRPM && currentGear > 1 && absSpeed > 2f)
            {
                float lowerRatio = gearRatios[currentGear - 2];
                float projectedRPM = SpeedToRPM(absSpeed, lowerRatio);
                if (projectedRPM < upshiftRPM - 400f)
                {
                    currentGear--;
                    shiftTimer = shiftTime;
                    lastShiftTime = Time.time;
                }
            }
        }
    }

    float SpeedToRPM(float speed, float gearRatio)
    {
        float wheelRadius = 0.35f;
        float wheelRPM = (speed * 60f) / (2f * Mathf.PI * wheelRadius);
        return wheelRPM * gearRatio * finalDriveRatio;
    }
}
