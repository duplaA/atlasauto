using UnityEngine;

/// <summary>
/// Calculates lateral and longitudinal G-forces from vehicle acceleration for camera and effects.
/// Values are in G units (1 = 9.81 m/s²).
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class VehicleGForceCalculator : MonoBehaviour
{
    private Rigidbody rb;
    private Vector3 lastVelocity;

    [Header("Smoothing")]
    [Tooltip("Smoothing of G-force values to avoid jitter.")]
    [Range(0.01f, 1f)] public float smoothing = 0.15f;

    /// <summary>Lateral G (positive = right). In G units.</summary>
    public float LateralG { get; private set; }
    /// <summary>Longitudinal G (positive = forward). In G units.</summary>
    public float LongitudinalG { get; private set; }

    /// <summary>Raw acceleration in m/s² (world space).</summary>
    public Vector3 Acceleration { get; private set; }

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        lastVelocity = rb != null ? rb.linearVelocity : Vector3.zero;
    }

    void FixedUpdate()
    {
        if (rb == null) return;
        float dt = Time.fixedDeltaTime;
        if (dt < 0.0001f) return;

        Vector3 accel = (rb.linearVelocity - lastVelocity) / dt;
        lastVelocity = rb.linearVelocity;
        Acceleration = accel;

        float lateral = Vector3.Dot(accel, transform.right) / 9.81f;
        float longitudinal = Vector3.Dot(accel, transform.forward) / 9.81f;

        LateralG = Mathf.Lerp(LateralG, lateral, 1f - smoothing);
        LongitudinalG = Mathf.Lerp(LongitudinalG, longitudinal, 1f - smoothing);
    }
}
