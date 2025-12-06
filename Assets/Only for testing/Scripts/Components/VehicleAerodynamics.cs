using UnityEngine;

public class VehicleAerodynamics : MonoBehaviour
{
    // Placeholder for now as original code didn't have explicit aero logic other than maybe drag (handled by RB)
    // But user asked for it.
    
    public float dragCoefficient = 0.3f;
    public float downforceCoefficient = 0.1f;
    public float frontalArea = 2.2f;
    
    public void ApplyAerodynamics(Rigidbody rb)
    {
        // Simple aero model
        // F_drag = 0.5 * rho * Cd * A * v^2
        // F_down = 0.5 * rho * Cl * A * v^2
        
        float rho = 1.225f; // Air density
        float speed = rb.linearVelocity.magnitude; // Unity 6 uses linearVelocity
        float dynamicPressure = 0.5f * rho * speed * speed;
        
        Vector3 velocityDir = rb.linearVelocity.normalized;
        Vector3 dragForce = -velocityDir * dynamicPressure * dragCoefficient * frontalArea;
        Vector3 downforce = -transform.up * dynamicPressure * downforceCoefficient * frontalArea;
        
        rb.AddForce(dragForce);
        rb.AddForce(downforce);
    }
}
