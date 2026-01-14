using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Supports run-time scaling and window expansion when menu opens.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class DriverHUD : MonoBehaviour
{
    [Header("Vehicle Reference")]
    public VehicleDataLink vehicle;

    [Header("Settings")]
    public bool autoFindVehicle = true;
    public float collapsedHeight = 180f;
    public float expandedHeight = 280f;
    [Range(0.5f, 1.5f)] public float uiScale = 1.0f;

    // UI References
    private UIDocument uiDocument;
    private VisualElement root;
    private VisualElement hudContainer;

    // Core UI Elements
    private Label speedValue;
    private Label gearDisplay;
    private Label drivetrainLabel;
    private Label hpValue;
    private Label torqueValue;
    private Label tripDistance;
    private Label tripTime;
    private Label rpmValue;
    private VisualElement throttleFill;
    private VisualElement brakeFill;
    private VisualElement rpmFill;
    
    // Menu Elements
    private VisualElement menuContainer;
    private VisualElement viewDrive, viewSystem, viewCustom;
    private VisualElement extensionsGrid;
    private Button burgerBtn;
    private Button tabDrive, tabSystem, tabCustom;
    private Slider scaleSlider;

    // Drivetrain Radio Buttons
    private Button radioRWD, radioFWD, radioAWD;

    // Toggle Buttons
    private Button toggleHeadlights, toggleTraction, toggleABS;
    
    // State
    private bool isMenuOpen = false;
    private float tripDistanceKM = 0f;
    private float tripTimeSeconds = 0f;
    private float tripStartTime;
    private Vector3 lastPosition;

    // Toggle States
    private bool headlightsOn = false;
    private bool tractionControlOn = true;
    private bool absOn = true;

    // Registered Custom Buttons
    private List<HUDButtonConfig> registeredButtons = new List<HUDButtonConfig>();
    private Dictionary<string, Button> buttonElements = new Dictionary<string, Button>();
    private Dictionary<string, List<string>> radioGroups = new Dictionary<string, List<string>>();

    void Awake()
    {
        uiDocument = GetComponent<UIDocument>();
        
        if (uiDocument.panelSettings != null)
        {
            uiDocument.panelSettings.clearColor = false; 
            uiDocument.panelSettings.colorClearValue = new Color(0, 0, 0, 0);
            
            uiDocument.panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            uiDocument.panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            uiDocument.panelSettings.match = 0.5f;

            Debug.Log("[DriverHUD] Fixed: Set PanelSettings.clearColor = false (Transparency Enabled) & Forcing ScaleWithScreenSize.");
        }
        else
        {
            Debug.LogError("[DriverHUD] CRITICAL: No PanelSettings assigned to UIDocument! UI will not render correctly.");
        }
    }

    void Start()
    {
        BindUIElements();
        
        // Try initial find
        TryFindVehicle();

        SetupBuiltInButtons();
        CreateRegisteredButtons();
        ApplyScale(uiScale);
        
        // Start nuclear monitor
        monitorStartTime = Time.time;
    }

    private float monitorStartTime;
    private bool hasLoggedUIDoc = false;
    private void MonitorCameras()
    {
        // For the first 10 seconds of play, aggressively clear target textures
        if (Time.time - monitorStartTime > 10f) return;

        if (!hasLoggedUIDoc && uiDocument != null)
        {
            hasLoggedUIDoc = true;
            if (uiDocument.panelSettings != null)
                Debug.Log($"[DriverHUD] UI Panel Settings: {uiDocument.panelSettings.name} | Scale Mode: {uiDocument.panelSettings.scaleMode}");
            else
                Debug.LogWarning("[DriverHUD] Warning: UIDocument has NO PanelSettings assigned!");
        }

        // Use FindObjectsByType to catch EVERYTHING, even disabled/hidden cameras
        Camera[] allCams = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var cam in allCams)
        {
            if (cam == null) continue;

            bool isMain = cam.CompareTag("MainCamera") || cam.name.ToLower().Contains("main");
            
            if (isMain)
            {
                // RESCUE MAIN CAMERA
                if (cam.targetTexture != null)
                {
                    Debug.LogWarning($"[DriverHUD] TARGETED FIX: Clearing broken targetTexture on MAIN camera '{cam.name}'");
                    cam.targetTexture = null;
                }

                // If Main Camera is cleared to solid color blue, maybe it's accidental
                if (cam.clearFlags == CameraClearFlags.SolidColor || cam.clearFlags == CameraClearFlags.Color)
                {
                    Debug.LogWarning($"[DriverHUD] Detected Main Camera '{cam.name}' clearing to SolidColor. Forcing Skybox for rescue to fix Blue Screen.");
                    cam.clearFlags = CameraClearFlags.Skybox;
                }

                if (!cam.gameObject.activeInHierarchy)
                {
                    Debug.LogWarning($"[DriverHUD] Main Camera '{cam.name}' is INACTIVE. Forcing active.");
                    cam.gameObject.SetActive(true);
                    cam.enabled = true;
                }
            }
            else if (cam.targetTexture != null)
            {
                // For secondary cameras, WE DON'T CLEAR THEM
                if (Time.frameCount % 600 == 0)
                {
                    Debug.Log($"[DriverHUD] Info: Found secondary camera '{cam.name}' rendering to texture '{cam.targetTexture.name}'.");
                }
            }
        }
    }

    private string GetHierarchyPath(Transform t)
    {
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }

    void BindUIElements()
    {
        root = uiDocument.rootVisualElement;
        hudContainer = root.Q<VisualElement>("hud-root");

        // Core displays
        speedValue = root.Q<Label>("speed-value");
        gearDisplay = root.Q<Label>("gear-display");
        drivetrainLabel = root.Q<Label>("drivetrain-label");
        hpValue = root.Q<Label>("hp-value");
        torqueValue = root.Q<Label>("torque-value");
        tripDistance = root.Q<Label>("trip-distance");
        tripTime = root.Q<Label>("trip-time");
        rpmValue = root.Q<Label>("rpm-value");

        // Input bars
        throttleFill = root.Q<VisualElement>("throttle-fill");
        brakeFill = root.Q<VisualElement>("brake-fill");
        rpmFill = root.Q<VisualElement>("rpm-fill");

        // Menu Containers
        menuContainer = root.Q<VisualElement>("menu-container");
        viewDrive = root.Q<VisualElement>("view-drive");
        viewSystem = root.Q<VisualElement>("view-system");
        viewCustom = root.Q<VisualElement>("view-custom");
        extensionsGrid = root.Q<VisualElement>("extensions-grid");

        // Tabs
        tabDrive = root.Q<Button>("tab-drive");
        tabSystem = root.Q<Button>("tab-system");
        tabCustom = root.Q<Button>("tab-custom");

        tabDrive?.RegisterCallback<ClickEvent>(e => SwitchTab(0));
        tabSystem?.RegisterCallback<ClickEvent>(e => SwitchTab(1));
        tabCustom?.RegisterCallback<ClickEvent>(e => SwitchTab(2));

        // Burger Button
        burgerBtn = root.Q<Button>("burger-btn");
        burgerBtn?.RegisterCallback<ClickEvent>(e => ToggleMenu());

        // Settings Controls
        scaleSlider = root.Q<Slider>("scale-slider");
        if (scaleSlider != null)
        {
            scaleSlider.value = uiScale;
            scaleSlider.RegisterValueChangedCallback(evt => {
                uiScale = evt.newValue;
                ApplyScale(uiScale);
            });
        }

        // Toggles & Drivetrain
        radioRWD = root.Q<Button>("radio-rwd");
        radioFWD = root.Q<Button>("radio-fwd");
        radioAWD = root.Q<Button>("radio-awd");
        toggleHeadlights = root.Q<Button>("toggle-headlights");
        toggleTraction = root.Q<Button>("toggle-traction");
        toggleABS = root.Q<Button>("toggle-abs");

        root.Q<Button>("btn-reset-trip")?.RegisterCallback<ClickEvent>(e => ResetTrip());
    }

    void SetupBuiltInButtons()
    {
        radioRWD?.RegisterCallback<ClickEvent>(e => SelectDrivetrain(VehicleController.DrivetrainType.RWD));
        radioFWD?.RegisterCallback<ClickEvent>(e => SelectDrivetrain(VehicleController.DrivetrainType.FWD));
        radioAWD?.RegisterCallback<ClickEvent>(e => SelectDrivetrain(VehicleController.DrivetrainType.AWD));

        UpdateDrivetrainRadios();

        toggleHeadlights?.RegisterCallback<ClickEvent>(e => ToggleHeadlights());
        toggleTraction?.RegisterCallback<ClickEvent>(e => ToggleTractionControl());
        toggleABS?.RegisterCallback<ClickEvent>(e => ToggleABS());

        UpdateToggleVisual(toggleHeadlights, headlightsOn);
        UpdateToggleVisual(toggleTraction, tractionControlOn);
        UpdateToggleVisual(toggleABS, absOn);
    }

    void Update()
    {
        MonitorCameras();

        // Lazy finding logic
        if (vehicle == null && autoFindVehicle)
        {
            TryFindVehicle();
            if (vehicle == null) return;
        }

        UpdateTelemetry();
        UpdateTripData();
        
        KeepUIOnScreen();
    }

    void KeepUIOnScreen()
    {
        if (hudContainer == null || root == null) return;
        
        // Ensure layout is calculated
        if (float.IsNaN(hudContainer.layout.width)) return;

        // Check if bottom edge is off-screen
        // root.layout.height is the bottom of the screen in Panel space
        float screenBottom = root.layout.height;
        float currentYMax = hudContainer.worldBound.yMax;
        
        float overlap = currentYMax - screenBottom;

        // If overlapping off bottom
        if (overlap > 1f)
        {
            float resultBottom = hudContainer.resolvedStyle.bottom + overlap;
            hudContainer.style.bottom = resultBottom;
        }
    }

    void TryFindVehicle()
    {
        if (vehicle != null) return;

        // Try finding component directly in scene
        vehicle = FindFirstObjectByType<VehicleDataLink>();
        if (vehicle != null)
        {
            Debug.Log($"[DriverHUD] Found VehicleDataLink via 'FindFirstObjectByType' on object: {vehicle.gameObject.name}");
        }
        else
        {
            // Try finding by tag "Car"
            GameObject carObj = GameObject.FindWithTag("Car");
            if (carObj != null) 
            {
                Debug.Log($"[DriverHUD] Found object with tag 'Car': {carObj.name}. Checking for VehicleDataLink...");
                vehicle = carObj.GetComponent<VehicleDataLink>();
                if (vehicle == null) vehicle = carObj.GetComponentInChildren<VehicleDataLink>();
                
                if (vehicle == null) Debug.LogWarning($"[DriverHUD] Object with tag 'Car' found ({carObj.name}), but NO VehicleDataLink component was found on it or its children.");
            }
            else
            {
                // Fallback: Search for common names
                GameObject namedVehicle = GameObject.Find("Vehicle") ?? GameObject.Find("Car");
                if (namedVehicle != null)
                {
                    Debug.Log($"[DriverHUD] Found object by name '{namedVehicle.name}'. Checking for VehicleDataLink...");
                    vehicle = namedVehicle.GetComponent<VehicleDataLink>();
                    if (vehicle == null) vehicle = namedVehicle.GetComponentInChildren<VehicleDataLink>();
                }
            }
        }
        
        if (vehicle != null)
        {
            Debug.Log($"[DriverHUD] SUCCESS: Linked to vehicle '{vehicle.gameObject.name}'");
            tripStartTime = Time.time;
            lastPosition = vehicle.transform.position;
            
            // Re-sync drivetrain state
            UpdateDrivetrainRadios();
        }
    }

    void UpdateTelemetry()
    {
        if (vehicle == null) return;

        if (speedValue != null) speedValue.text = Mathf.RoundToInt(vehicle.SpeedKMH).ToString();
        if (gearDisplay != null) gearDisplay.text = vehicle.GearDisplay;
        if (drivetrainLabel != null) drivetrainLabel.text = vehicle.Drivetrain.ToString();
        if (hpValue != null) hpValue.text = $"{Mathf.RoundToInt(vehicle.HorsepowerHP)} HP";
        if (torqueValue != null) torqueValue.text = $"{Mathf.RoundToInt(vehicle.EngineTorqueNm)} Nm";
        if (rpmValue != null) rpmValue.text = Mathf.RoundToInt(vehicle.EngineRPM).ToString();

        if (throttleFill != null) throttleFill.style.height = Length.Percent(vehicle.ThrottleInput * 100f);
        if (brakeFill != null) brakeFill.style.height = Length.Percent(vehicle.BrakeInput * 100f);

        if (rpmFill != null)
        {
            float maxRPM = vehicle.EngineMaxRPM > 0 ? vehicle.EngineMaxRPM : 6000f;
            float rpm = Mathf.Clamp(vehicle.EngineRPM, 0f, maxRPM);
            float rpmPercent = rpm / maxRPM;
            
            rpmFill.style.width = Length.Percent(rpmPercent * 100f);
            rpmFill.RemoveFromClassList("high");
            rpmFill.RemoveFromClassList("redline");
            if (rpmPercent > 0.9f) rpmFill.AddToClassList("redline");
            else if (rpmPercent > 0.75f) rpmFill.AddToClassList("high");
        }
    }

    void UpdateTripData()
    {
        if (vehicle == null) return;
        Vector3 currentPos = vehicle.transform.position;
        float delta = Vector3.Distance(currentPos, lastPosition);
        tripDistanceKM += delta / 1000f;
        lastPosition = currentPos;
        tripTimeSeconds = Time.time - tripStartTime;

        if (tripDistance != null) tripDistance.text = $"{tripDistanceKM:F1} km";
        if (tripTime != null)
        {
            int minutes = Mathf.FloorToInt(tripTimeSeconds / 60f);
            int seconds = Mathf.FloorToInt(tripTimeSeconds % 60f);
            tripTime.text = $"{minutes:D2}:{seconds:D2}";
        }
    }

    #region Scaling & View Control

    void ApplyScale(float scale)
    {
        if (hudContainer != null)
        {
            // Apply scale transform
            hudContainer.style.scale = new Scale(new Vector3(scale, scale, 1f));
            
            // With transform.scale, layout space remains same, visual scales.
        }
    }

    void ToggleMenu()
    {
        isMenuOpen = !isMenuOpen;
        
        if (menuContainer != null)
        {
            if (isMenuOpen) menuContainer.AddToClassList("visible");
            else menuContainer.RemoveFromClassList("visible");
        }

        // Expand/Collapse Window logic
        if (hudContainer != null)
        {
            hudContainer.style.height = isMenuOpen ? expandedHeight : collapsedHeight;
        }

        if (burgerBtn != null) burgerBtn.text = isMenuOpen ? "✕" : "⋮";
    }

    void SwitchTab(int tabIndex)
    {
        if (viewDrive != null) viewDrive.style.display = (tabIndex == 0) ? DisplayStyle.Flex : DisplayStyle.None;
        if (viewSystem != null) viewSystem.style.display = (tabIndex == 1) ? DisplayStyle.Flex : DisplayStyle.None;
        if (viewCustom != null) viewCustom.style.display = (tabIndex == 2) ? DisplayStyle.Flex : DisplayStyle.None;

        if (tabDrive != null) ToggleClass(tabDrive, "active", tabIndex == 0);
        if (tabSystem != null) ToggleClass(tabSystem, "active", tabIndex == 1);
        if (tabCustom != null) ToggleClass(tabCustom, "active", tabIndex == 2);
    }

    void ToggleClass(VisualElement el, string className, bool on)
    {
        if (on) el.AddToClassList(className);
        else el.RemoveFromClassList(className);
    }

    #endregion

    #region Features Logic

    void SelectDrivetrain(VehicleController.DrivetrainType type)
    {
        vehicle?.SetDrivetrain(type);
        UpdateDrivetrainRadios();
    }

    void UpdateDrivetrainRadios()
    {
        if (vehicle == null) return;
        radioRWD?.RemoveFromClassList("selected");
        radioFWD?.RemoveFromClassList("selected");
        radioAWD?.RemoveFromClassList("selected");

        switch (vehicle.Drivetrain)
        {
            case VehicleController.DrivetrainType.RWD: radioRWD?.AddToClassList("selected"); break;
            case VehicleController.DrivetrainType.FWD: radioFWD?.AddToClassList("selected"); break;
            case VehicleController.DrivetrainType.AWD: radioAWD?.AddToClassList("selected"); break;
        }
    }

    void ToggleHeadlights() { headlightsOn = !headlightsOn; UpdateToggleVisual(toggleHeadlights, headlightsOn); }
    void ToggleTractionControl() { tractionControlOn = !tractionControlOn; UpdateToggleVisual(toggleTraction, tractionControlOn); }
    void ToggleABS() { absOn = !absOn; UpdateToggleVisual(toggleABS, absOn); }

    void UpdateToggleVisual(Button btn, bool isOn)
    {
        if (btn == null) return;
        if (isOn) btn.AddToClassList("on");
        else btn.RemoveFromClassList("on");
    }

    void ResetTrip()
    {
        tripDistanceKM = 0f;
        tripStartTime = Time.time;
        tripTimeSeconds = 0f;
    }

    #endregion

    #region Extensible API

    public void RegisterButton(HUDButtonConfig config)
    {
        if (config == null || string.IsNullOrEmpty(config.Id)) return;
        
        registeredButtons.Add(config);

        // Track radio groups
        if ((config.Type == HUDButtonType.Radio || config.Type == HUDButtonType.RadioToggle) && !string.IsNullOrEmpty(config.RadioGroup))
        {
            if (!radioGroups.ContainsKey(config.RadioGroup))
                radioGroups[config.RadioGroup] = new List<string>();
            radioGroups[config.RadioGroup].Add(config.Id);
        }

        if (extensionsGrid != null) CreateButton(config);
    }

    void CreateRegisteredButtons()
    {
        if (extensionsGrid == null) return;
        foreach (var config in registeredButtons) CreateButton(config);
    }

    void CreateButton(HUDButtonConfig config)
    {
        // Wrapper container (App Item)
        VisualElement item = new VisualElement();
        item.AddToClassList("app-item");

        // Circle Button (Icon only)
        Button btn = new Button();
        btn.name = $"ext-{config.Id}";
        // Use Icon if available, else first letter of label
        string iconText = !string.IsNullOrEmpty(config.Icon) ? config.Icon : 
                         (!string.IsNullOrEmpty(config.Label) && config.Label.Length > 0 ? config.Label.Substring(0, 1) : "?");
        btn.text = iconText;
        btn.AddToClassList("app-icon-btn");

        // Label below
        Label label = new Label();
        label.text = config.Label;
        label.AddToClassList("app-label");

        buttonElements[config.Id] = btn;

        switch (config.Type)
        {
            case HUDButtonType.Normal:
                btn.clicked += () => config.OnClick?.Invoke();
                break;
            case HUDButtonType.Toggle:
                bool state = config.InitialState;
                if (state) btn.AddToClassList("active");
                // Local toggle visual
                btn.clicked += () => {
                    state = !state;
                    ToggleClass(btn, "active", state);
                    config.OnToggle?.Invoke(state);
                };
                // Note: If external state changes, we might need a way to update this UI.
                break;
            case HUDButtonType.Radio:
                btn.clicked += () => config.OnSelected?.Invoke();
                break;
            case HUDButtonType.RadioToggle:
                bool initial = config.InitialState;
                if (initial) btn.AddToClassList("active");
                
                btn.clicked += () => {
                   SelectRadioToggle(config);
                };
                break;
        }

        item.Add(btn);
        item.Add(label);
        extensionsGrid.Add(item);
    }

    void SelectRadioToggle(HUDButtonConfig config)
    {
        if (string.IsNullOrEmpty(config.RadioGroup) || !radioGroups.ContainsKey(config.RadioGroup)) return;

        bool wasActive = false;
        if (buttonElements.TryGetValue(config.Id, out Button targetBtn))
        {
            wasActive = targetBtn.ClassListContains("active");
        }

        // Deselect all others or toggle off current
        foreach (var id in radioGroups[config.RadioGroup])
        {
            if (buttonElements.TryGetValue(id, out Button btn))
            {
                if (wasActive)
                {
                    // Clicked an active button - turn everything off
                    ToggleClass(btn, "active", false);
                }
                else
                {
                    // Selecting new button
                    bool isTarget = (id == config.Id);
                    ToggleClass(btn, "active", isTarget);
                }
            }
        }
        
        config.OnSelected?.Invoke();
    }

    #endregion
}
