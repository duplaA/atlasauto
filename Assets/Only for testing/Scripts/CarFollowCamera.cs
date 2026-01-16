using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Dynamic racing camera inspired by Driveclub and Assetto Corsa.
/// Features speed-based FOV, camera shake, look-ahead, and dynamic positioning.
/// Right-click drag to orbit around the car.
/// </summary>
public class CarFollowCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform target;
    private VehicleDataLink dataLink;
    private Camera cam;

    [Header("Orbit Position")]
    [Tooltip("Base distance from the car")]
    public float baseDistance = 10.0f;
    [Tooltip("Minimum orbit distance")]
    public float minDistance = 4f;
    [Tooltip("Maximum orbit distance")]
    public float maxDistance = 25f;
    [Tooltip("Height of the look-at point above car center")]
    public float lookAtHeight = 1.5f;

    [Header("Orbit Control")]
    public float sensitivityX = 5f;
    public float sensitivityY = 3f;
    public float minVerticalAngle = -20f;
    public float maxVerticalAngle = 60f;
    
    // Orbit angles
    private float orbitX = 0f;   // Horizontal angle (yaw)
    private float orbitY = 25f;  // Vertical angle (pitch)

    [Header("Speed Effects")]
    [Tooltip("Additional distance at max speed")]
    public float speedDistanceAdd = 4.0f;
    [Tooltip("Base FOV at standstill")]
    public float baseFOV = 60f;
    [Tooltip("Maximum FOV at top speed")]
    public float maxFOV = 80f;
    [Tooltip("Speed at which max effects are reached (km/h)")]
    public float effectMaxSpeed = 200f;

    [Header("Camera Lag")]
    [Tooltip("How smoothly the camera follows (higher = faster)")]
    public float followSmoothness = 10f;

    [Header("Acceleration Effects")]
    [Tooltip("Camera pitch down when accelerating (degrees)")]
    public float accelerationPitch = 2f;
    [Tooltip("Camera pitch up when braking (degrees)")]
    public float brakingPitch = 3f;

    [Header("Camera Shake")]
    public bool enableShake = true;
    [Tooltip("Maximum shake intensity at high speed")]
    public float maxShakeIntensity = 0.08f;
    public float shakeFrequency = 20f;

    // Internal state
    private float currentDistance;
    private float currentFOV;
    private float currentPitch = 0f;
    private Vector3 smoothedPosition;
    private float shakeTime = 0f;
    private float prevSpeed = 0f;

    void Start()
    {
        cam = GetComponent<Camera>();
        if (cam == null) cam = Camera.main;
        
        currentDistance = baseDistance;
        currentFOV = baseFOV;
        
        // Initialize position behind the car
        if (target != null)
        {
            // Start behind the car
            orbitX = target.eulerAngles.y;
            smoothedPosition = CalculateOrbitPosition();
            transform.position = smoothedPosition;
            transform.LookAt(target.position + Vector3.up * lookAtHeight);
        }
        
        FindDataLink();
    }

    void FindDataLink()
    {
        if (target != null && dataLink == null)
        {
            dataLink = target.GetComponent<VehicleDataLink>();
            if (dataLink == null)
                dataLink = target.GetComponentInParent<VehicleDataLink>();
        }
    }

    void LateUpdate()
    {
        if (target == null) return;
        FindDataLink();
        
        float dt = Time.deltaTime;
        
        // Get vehicle data
        float speedKMH = dataLink != null ? dataLink.SpeedKMH : 0f;
        float speedRatio = Mathf.Clamp01(speedKMH / effectMaxSpeed);
        float throttle = dataLink != null ? dataLink.ThrottleInput : 0f;
        float brake = dataLink != null ? dataLink.BrakeInput : 0f;
        bool isBraking = dataLink != null ? dataLink.IsBraking : false;
        float slip = dataLink != null ? dataLink.SlipRatio : 0f;
        
        // Handle orbit input (right mouse button)
        HandleOrbitInput(dt);
        
        // Handle zoom (scroll wheel)
        HandleZoom();
        
        // Calculate dynamic distance based on speed
        float targetDistance = baseDistance + (speedDistanceAdd * speedRatio);
        currentDistance = Mathf.Lerp(currentDistance, targetDistance, dt * 5f);
        
        // Calculate FOV based on speed (exponential curve for more impact at high speed)
        float targetFOV = Mathf.Lerp(baseFOV, maxFOV, speedRatio * speedRatio);
        currentFOV = Mathf.Lerp(currentFOV, targetFOV, dt * 6f);
        
        if (cam != null)
        {
            cam.fieldOfView = currentFOV;
        }
        
        // Calculate pitch based on acceleration/braking
        float targetPitch = 0f;
        if (isBraking && speedKMH > 5f)
        {
            targetPitch = brakingPitch * brake;
        }
        else if (throttle > 0.3f && speedKMH > 10f)
        {
            targetPitch = -accelerationPitch * throttle * speedRatio;
        }
        currentPitch = Mathf.Lerp(currentPitch, targetPitch, dt * 5f);
        
        // Calculate orbit position
        Vector3 idealPosition = CalculateOrbitPosition();
        
        // Smooth follow
        smoothedPosition = Vector3.Lerp(smoothedPosition, idealPosition, dt * followSmoothness);
        
        // Apply shake
        Vector3 shakeOffset = Vector3.zero;
        if (enableShake && speedKMH > 40f)
        {
            shakeTime += dt * shakeFrequency;
            float shakeAmount = maxShakeIntensity * speedRatio;
            
            // Extra shake from wheel slip
            if (slip > 0.15f)
            {
                shakeAmount += maxShakeIntensity * slip * 1.5f;
            }
            
            shakeOffset = new Vector3(
                (Mathf.PerlinNoise(shakeTime, 0f) - 0.5f) * shakeAmount,
                (Mathf.PerlinNoise(0f, shakeTime) - 0.5f) * shakeAmount,
                0f
            );
        }
        
        // Apply final position
        transform.position = smoothedPosition + shakeOffset;
        
        // Look at target with pitch offset
        Vector3 lookTarget = target.position + Vector3.up * lookAtHeight;
        transform.LookAt(lookTarget);
        
        // Apply pitch
        transform.rotation *= Quaternion.Euler(currentPitch, 0f, 0f);
        
        prevSpeed = speedKMH;
    }

    Vector3 CalculateOrbitPosition()
    {
        // Convert orbit angles to position around target
        Quaternion rotation = Quaternion.Euler(orbitY, orbitX, 0f);
        Vector3 offset = rotation * new Vector3(0f, 0f, -currentDistance);
        return target.position + offset + Vector3.up * lookAtHeight * 0.5f;
    }

    void HandleOrbitInput(float dt)
    {
        if (Mouse.current == null) return;
        
        if (Mouse.current.rightButton.isPressed)
        {
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();
            orbitX += mouseDelta.x * sensitivityX * dt * 10f;
            orbitY -= mouseDelta.y * sensitivityY * dt * 10f;
            orbitY = Mathf.Clamp(orbitY, minVerticalAngle, maxVerticalAngle);
        }
        else
        {
            // Gradually return to behind the car when not orbiting
            float targetOrbitX = target.eulerAngles.y;
            
            // Handle angle wrapping
            float angleDiff = Mathf.DeltaAngle(orbitX, targetOrbitX);
            if (Mathf.Abs(angleDiff) > 1f)
            {
                orbitX = Mathf.LerpAngle(orbitX, targetOrbitX, dt * 2f);
            }
            
            // Return vertical angle to default
            orbitY = Mathf.Lerp(orbitY, 25f, dt * 2f);
        }
    }

    void HandleZoom()
    {
        if (Mouse.current == null) return;
        
        float scroll = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) > 0.01f)
        {
            baseDistance -= scroll * 0.5f;
            baseDistance = Mathf.Clamp(baseDistance, minDistance, maxDistance);
        }
    }
}