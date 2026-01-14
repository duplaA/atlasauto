using System;

/// <summary>
/// Types of buttons that can be registered on the Driver HUD.
/// </summary>
public enum HUDButtonType
{
    /// <summary>One-time click button.</summary>
    Normal,
    
    /// <summary>Toggle button with on/off state.</summary>
    Toggle,
    
    /// <summary>Radio button - only one in a group can be active.</summary>
    Radio,

    /// <summary>Toggle button that behaves like a Radio button (mutually exclusive in a group).</summary>
    RadioToggle
}

/// <summary>
/// Configuration for a custom HUD button.
/// </summary>
public class HUDButtonConfig
{
    /// <summary>Unique identifier for this button.</summary>
    public string Id;
    
    /// <summary>Display label for the button.</summary>
    public string Label;
    
    /// <summary>Optional icon (emoji or unicode character).</summary>
    public string Icon = "";
    
    /// <summary>Type of button behavior.</summary>
    public HUDButtonType Type = HUDButtonType.Normal;
    
    /// <summary>For Radio buttons, the group name. All radios in same group are mutually exclusive.</summary>
    public string RadioGroup = "";
    
    /// <summary>Initial state for Toggle buttons.</summary>
    public bool InitialState = false;
    
    /// <summary>Callback for Normal buttons. Called when clicked.</summary>
    public Action OnClick;
    
    /// <summary>Callback for Toggle buttons. Called with new state when toggled.</summary>
    public Action<bool> OnToggle;
    
    /// <summary>Callback for Radio buttons. Called when this radio is selected.</summary>
    public Action OnSelected;
}
