using UnityEngine;

/// <summary>
/// Hud extension example
/// </summary>
public class HUDExtensionExample : MonoBehaviour
{
    [Header("Reference")]
    public DriverHUD hud;

    [Header("Control States")]
    public bool speedControlActive = false;
    public int steeringTarget = 0; // -1 = Left, 0 = Center, 1 = Right

    private float targetSpeedKMH = 60f;
    private float smoothSteer = 0f;
    private float steerVelocity = 0f;

    void Start()
    {
        if (hud == null) hud = FindFirstObjectByType<DriverHUD>();
        if (hud != null) RegisterCustomButtons();
    }

    void Update()
    {
        // Check for HUD/Vehicle link
        if (Time.frameCount % 120 == 0)
        {
            if (hud == null) hud = FindFirstObjectByType<DriverHUD>();
            if (hud != null && hud.vehicle == null)
            {
                VehicleDataLink v = FindFirstObjectByType<VehicleDataLink>();
                if (v != null) hud.vehicle = v;
            }
        }
    }

    void FixedUpdate()
    {
        if (hud == null || hud.vehicle == null) return;

        bool anyActive = speedControlActive || steeringTarget != 0;
        if (!anyActive) return;

        float inputThrottle = 0f;
        float inputSteer = 0f;

        // Speed Control (Maintain ~60 km/h)
        if (speedControlActive)
        {
            float speed = hud.vehicle.SpeedKMH;
            if (speed < targetSpeedKMH) inputThrottle = 0.6f;
            else if (speed > targetSpeedKMH + 2f) inputThrottle = 0.1f;
        }

        // Steering Control
        float targetAngle = 0f;
        if (steeringTarget == -1) targetAngle = -0.5f;
        else if (steeringTarget == 1) targetAngle = 0.5f;

        smoothSteer = Mathf.SmoothDamp(smoothSteer, targetAngle, ref steerVelocity, 0.2f);
        inputSteer = smoothSteer;

        // Apply Control
        hud.vehicle.SetExternalControl(new Vector2(inputSteer, inputThrottle), true);
    }

    void RegisterCustomButtons()
    {
        // Speed Toggle
        hud.RegisterButton(new HUDButtonConfig
        {
            Id = "speed_control",
            Label = "Speed",
            Icon = "V",
            Type = HUDButtonType.Toggle,
            InitialState = speedControlActive,
            OnToggle = (isOn) => {
                speedControlActive = isOn;
                Debug.Log($"[HUDExtension] Speed Control: {(isOn ? "ON (60km/h)" : "OFF")}");
                if (!isOn && steeringTarget == 0) hud.vehicle.SetExternalControl(Vector2.zero, false);
            }
        });

        // Steer Left (Radio)
        hud.RegisterButton(new HUDButtonConfig
        {
            Id = "steer_left",
            Label = "Left",
            Icon = "L",
            Type = HUDButtonType.RadioToggle,
            RadioGroup = "steer",
            OnSelected = () => {
                if (steeringTarget == -1) {
                    steeringTarget = 0;
                    Debug.Log("[HUDExtension] Steering: Center");
                } else {
                    steeringTarget = -1;
                    Debug.Log("[HUDExtension] Steering: Left");
                }
                UpdateOverrideState();
            }
        });

        // Steer Right (Radio)
        hud.RegisterButton(new HUDButtonConfig
        {
            Id = "steer_right",
            Label = "Right",
            Icon = "R",
            Type = HUDButtonType.RadioToggle,
            RadioGroup = "steer",
            OnSelected = () => {
                if (steeringTarget == 1) {
                    steeringTarget = 0;
                    Debug.Log("[HUDExtension] Steering: Center");
                } else {
                    steeringTarget = 1;
                    Debug.Log("[HUDExtension] Steering: Right");
                }
                UpdateOverrideState();
            }
        });

        // Transmission Mode Toggle (Standard Button)
        hud.RegisterButton(new HUDButtonConfig
        {
            Id = "trans_mode",
            Label = "Mode",
            Icon = "M",
            Type = HUDButtonType.Normal,
            OnClick = () => {
                var transmission = hud.vehicle.GetComponent<VehicleTransmission>();
                if (transmission != null)
                {
                    if (transmission.mode == VehicleTransmission.TransmissionMode.Automatic)
                    {
                        transmission.mode = VehicleTransmission.TransmissionMode.Manual;
                        Debug.Log("[HUDExtension] Transmission set to MANUAL");
                    }
                    else
                    {
                        transmission.mode = VehicleTransmission.TransmissionMode.Automatic;
                        Debug.Log("[HUDExtension] Transmission set to AUTOMATIC");
                    }
                }
            }
        });
    }

    void UpdateOverrideState()
    {
        if (!speedControlActive && steeringTarget == 0)
        {
             if (hud.vehicle != null) hud.vehicle.SetExternalControl(Vector2.zero, false);
        }
    }
}
