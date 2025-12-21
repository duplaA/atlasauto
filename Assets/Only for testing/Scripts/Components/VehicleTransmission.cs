using UnityEngine;

public class VehicleTransmission : MonoBehaviour
{
    public bool isElectric = false;
    public float[] gearRatios = { 3.5f, 2.1f, 1.4f, 1.0f, 0.75f };
    public float reverseRatio = 3.2f;
    public float electricFixedRatio = 8.5f;
    public float finalDriveRatio = 3.4f;
    public int currentGear = 1; 
    public float clutchPosition;

    private float shiftTimer = 0f; // Megakadályozza a sorozatváltást
    public float shiftDelay = 0.7f; // Ennyi másodpercig NEM válthat újra

    public void UpdateTransmission(float speedKMH, float engineRPM, float dt)
    {
        if (shiftTimer > 0) shiftTimer -= dt;
        if (isElectric) { clutchPosition = 1f; return; }

        if (currentGear > 0 && shiftTimer <= 0)
        {
            // MEGEMELT KÜSZÖB: 1-esben legalább 40km/h kell a váltáshoz
            float upshiftSpeedThreshold = currentGear * 40f; 

            if (engineRPM > 5800 && currentGear < gearRatios.Length && speedKMH > upshiftSpeedThreshold)
            {
                currentGear++;
                shiftTimer = shiftDelay; // VÁRNI KELL a következő váltásig
            }
            else if (engineRPM < 2200 && currentGear > 1)
            {
                currentGear--;
                shiftTimer = shiftDelay;
            }
        }
        
        clutchPosition = (speedKMH < 10f && currentGear != 0) ? 0.4f : 1f;
    }

    public float GetTotalRatio()
    {
        if (currentGear == 0) return 0;
        int gearIndex = Mathf.Clamp(currentGear - 1, 0, gearRatios.Length - 1);
        float ratio = (currentGear == -1) ? -reverseRatio : gearRatios[gearIndex];
        return ratio * finalDriveRatio;
    }
}