using UnityEngine;

/// <summary>
/// Centralized speed normalization and intensity manager for sense-of-speed effects.
/// Calculates speedNorm (0-1) used by FOV, blur, shake, and other intensity curves.
/// </summary>
[RequireComponent(typeof(VehicleController))]
public class VehicleSpeedSense : MonoBehaviour
{
    private VehicleController vc;

    [Header("Normalization")]
    [Tooltip("Use vehicle top speed for normalization (0-1). If false, uses fixed 300 km/h cap.")]
    public bool useVehicleTopSpeed = true;
    [Tooltip("Fixed cap in km/h when not using vehicle top speed (e.g. 300).")]
    public float speedCapKMH = 300f;

    /// <summary>Normalized speed 0-1 driving most intensity curves.</summary>
    public float SpeedNorm { get; private set; }

    void Awake()
    {
        vc = GetComponent<VehicleController>();
        if (vc == null) vc = GetComponentInParent<VehicleController>();
    }

    void FixedUpdate()
    {
        if (vc == null) return;
        float speedKMH = vc.speedKMH;
        if (useVehicleTopSpeed && vc.topSpeedKMH > 0.01f)
            SpeedNorm = Mathf.Clamp01(speedKMH / vc.topSpeedKMH);
        else
            SpeedNorm = Mathf.Clamp(speedKMH / Mathf.Max(speedCapKMH, 1f), 0f, 1f);
    }

    void OnValidate()
    {
        if (vc == null) vc = GetComponent<VehicleController>();
    }
}
