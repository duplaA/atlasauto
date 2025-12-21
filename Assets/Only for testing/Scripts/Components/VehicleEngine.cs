using UnityEngine;

public class VehicleEngine : MonoBehaviour
{
    public enum EngineType { InternalCombustion, Electric }
    public EngineType engineType = EngineType.InternalCombustion;
    public float peakTorqueNm = 450f;
    public float maxRPM = 7000f;
    public float idleRPM = 800f;
    public float currentRPM;

    public void UpdateEngineRPM(float throttle, float wheelRPM, float totalRatio, float clutch, float brakeInput, float dt)
    {
        float minRPM = (engineType == EngineType.Electric) ? 0 : idleRPM;
        
        // Kiszámoljuk, mennyi lenne az RPM a kerék sebessége alapján
        float physicalRPM = Mathf.Abs(wheelRPM * totalRatio);
        
        // Ha a kuplung zárva (clutch=1), az RPM-nek muszáj a fizikai keréksebességhez igazodnia
        float targetRPM = Mathf.Lerp(currentRPM, physicalRPM, clutch * 10f * dt);
        
        // Ha gázt adunk üresben vagy csúsztatott kuplunggal, felpörög
        if (clutch < 0.9f) {
            float freeRPM = Mathf.Lerp(minRPM, maxRPM, throttle);
            targetRPM = Mathf.Max(targetRPM, freeRPM);
        }

        // Fékezéskor lehúzzuk az RPM-et
        if (brakeInput > 0.5f) targetRPM = Mathf.MoveTowards(targetRPM, minRPM, 3000f * dt);

        currentRPM = Mathf.Clamp(targetRPM, minRPM, maxRPM);
    }

    public float GetTorque(float throttle)
    {
        // Leszabályzás: ha elérjük a max RPM-et, elvesszük a tüzet
        if (currentRPM >= maxRPM - 100f) return 0;
        
        float factor = (engineType == EngineType.Electric) ? 1.0f : Mathf.Clamp01(currentRPM / 3000f);
        return peakTorqueNm * throttle * factor;
    }
}