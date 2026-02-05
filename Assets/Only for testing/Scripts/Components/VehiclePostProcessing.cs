using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Drives motion blur and depth of field from vehicle speed (TDU-style).
/// Assign a Volume that has Motion Blur and Depth of Field in its profile (URP).
/// </summary>
public class VehiclePostProcessing : MonoBehaviour
{
    [Header("Volume")]
    [Tooltip("Volume with Motion Blur and Depth of Field. If null, uses first global Volume in scene.")]
    public Volume volume;

    [Header("Motion Blur")]
    [Tooltip("Speed (km/h) above which blur starts")]
    public float blurStartSpeedKMH = 80f;
    [Tooltip("Speed range for full blur (e.g. 350)")]
    public float blurFullSpeedKMH = 350f;
    [Tooltip("Max blur intensity (0-1.2)")]
    [Range(0f, 1.5f)] public float maxBlurIntensity = 1.2f;
    [Tooltip("Wheel blur multiplier (1.8-2.5) - for reference; per-object blur needs pipeline support")]
    public float wheelBlurMultiplier = 2f;
    [Tooltip("Environment blur scale (0.3-0.6) - scales main intensity")]
    [Range(0.2f, 1f)] public float environmentBlurScale = 0.5f;

    [Header("Depth of Field")]
    [Tooltip("Base focus distance at rest")]
    public float focusDistanceBase = 8f;
    [Tooltip("Focus distance add per km/h (focus = base + speed/25)")]
    public float focusDistancePerSpeed = 1f / 25f;
    [Tooltip("Aperture at rest (less blur) / at max speed (more blur). Lower = more blur.")]
    public float apertureAtRest = 22f;
    public float apertureAtMaxSpeed = 2.5f;

    private VehicleDataLink dataLink;
    private VehicleSpeedSense speedSense;
    private MotionBlur motionBlur;
    private DepthOfField depthOfField;
    private bool cached;

    void Awake()
    {
        dataLink = GetComponent<VehicleDataLink>();
        if (dataLink == null) dataLink = GetComponentInParent<VehicleDataLink>();
        speedSense = GetComponent<VehicleSpeedSense>();
        if (speedSense == null) speedSense = GetComponentInParent<VehicleSpeedSense>();
    }

    void Start()
    {
        CacheVolumeComponents();
    }

    void CacheVolumeComponents()
    {
        if (cached) return;
        if (volume == null)
            volume = FindFirstObjectByType<Volume>();
        if (volume == null || volume.profile == null) return;
        volume.profile.TryGet(out motionBlur);
        volume.profile.TryGet(out depthOfField);
        cached = true;
    }

    void Update()
    {
        CacheVolumeComponents();
        float speedKMH = dataLink != null ? dataLink.SpeedKMH : 0f;
        float speedNorm = speedSense != null ? speedSense.SpeedNorm : (dataLink != null && dataLink.TopSpeedKMH > 0.01f ? Mathf.Clamp01(speedKMH / dataLink.TopSpeedKMH) : 0f);

        // Motion blur: strength = clamp((speed - 80) / 350, 0, 1.2)
        float blurStrength = 0f;
        if (speedKMH > blurStartSpeedKMH && blurFullSpeedKMH > blurStartSpeedKMH)
            blurStrength = Mathf.Clamp((speedKMH - blurStartSpeedKMH) / (blurFullSpeedKMH - blurStartSpeedKMH), 0f, 1f) * maxBlurIntensity;
        blurStrength *= environmentBlurScale;

        if (motionBlur != null)
        {
            motionBlur.active = blurStrength > 0.001f;
            if (!motionBlur.intensity.overrideState) motionBlur.intensity.overrideState = true;
            motionBlur.intensity.value = blurStrength;
        }

        if (depthOfField != null)
        {
            float focusDist = focusDistanceBase + (speedKMH * focusDistancePerSpeed);
            float aperture = Mathf.Lerp(apertureAtRest, apertureAtMaxSpeed, speedNorm);
            depthOfField.active = speedNorm > 0.05f;
            if (!depthOfField.focusDistance.overrideState) depthOfField.focusDistance.overrideState = true;
            depthOfField.focusDistance.value = focusDist;
            if (!depthOfField.aperture.overrideState) depthOfField.aperture.overrideState = true;
            depthOfField.aperture.value = Mathf.Clamp(aperture, 0.5f, 32f);
        }
    }
}
