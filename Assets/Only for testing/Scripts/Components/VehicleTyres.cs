using UnityEngine;

public class VehicleTyres : MonoBehaviour
{
    public void UpdateFriction(VehicleWheel[] wheels)
    {
        foreach (var w in wheels)
        {
            if (w.wheelCollider == null) continue;
            
            // Forward (Gyorsulás/Fékezés) tapadás
            WheelFrictionCurve fwd = w.wheelCollider.forwardFriction;
            fwd.extremumSlip = 0.3f;
            fwd.extremumValue = 1.5f; // Megemelt tapadás
            fwd.asymptoteValue = 1.0f;
            fwd.stiffness = 2.0f; // Ne csússzon
            w.wheelCollider.forwardFriction = fwd;

            // Sideways (Kanyarodás) tapadás
            WheelFrictionCurve side = w.wheelCollider.sidewaysFriction;
            side.extremumSlip = 0.2f;
            side.extremumValue = 1.8f; // Nagyon tapadjon kanyarban
            side.stiffness = 2.5f;
            w.wheelCollider.sidewaysFriction = side;
        }
    }
}