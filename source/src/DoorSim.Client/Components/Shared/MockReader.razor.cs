namespace DoorSim.Client.Components.Shared;

public partial class MockReader
{
    // Unique suffix so multiple readers on one page don't share the same SVG filter ID.
    private readonly string _uid = Guid.NewGuid().ToString("N")[..8];

    [Parameter]
    [EditorRequired]
    public string Label { get; set; } = string.Empty;

    [Parameter]
    [EditorRequired]
    public ProtocolType Protocol { get; set; }

    [Parameter]
    public bool IsConnected { get; set; }

    [Parameter]
    public ReaderLedState? LedState { get; set; }

    [Parameter]
    public SimulationStatus SimStatus { get; set; }

    // -------------------------------------------------------------------------
    // Effective LED color
    // -------------------------------------------------------------------------

    // For OSDP: use what the panel commanded.
    // For Wiegand: derive a colour from simulation status so there is still
    //              a visual cue even though there is no back-channel.
    private OsdpLedColor EffectiveColor => Protocol == ProtocolType.Osdp && LedState is not null
        ? LedState.Color
        : SimStatus switch
        {
            SimulationStatus.Running => OsdpLedColor.Amber,
            SimulationStatus.Success => OsdpLedColor.Green,
            SimulationStatus.Error => OsdpLedColor.Red,
            var _ => OsdpLedColor.Off,
        };

    private bool EffectiveBlink => Protocol == ProtocolType.Osdp
        ? LedState?.Blink ?? false
        : SimStatus == SimulationStatus.Running;

    private string LedCssColor => EffectiveColor switch
    {
        OsdpLedColor.Red => "#ef4444",
        OsdpLedColor.Green => "#22c55e",
        OsdpLedColor.Amber => "#f59e0b",
        OsdpLedColor.Blue => "#3b82f6",
        OsdpLedColor.Magenta => "#d946ef",
        OsdpLedColor.Cyan => "#06b6d4",
        OsdpLedColor.White => "#f8fafc",
        var _ => "#2a2a2c", // Off — matches body colour
    };

    private string LedColorLabel => EffectiveColor switch
    {
        OsdpLedColor.Off => "Off",
        OsdpLedColor.Red => "Red",
        OsdpLedColor.Green => "Green",
        OsdpLedColor.Amber => "Amber",
        OsdpLedColor.Blue => "Blue",
        OsdpLedColor.Magenta => "Magenta",
        OsdpLedColor.Cyan => "Cyan",
        OsdpLedColor.White => "White",
        var _ => "Off",
    };

    private string LedClass => EffectiveBlink
        ? "led-blink"
        : string.Empty;

    // Tap icon follows the LED color when lit, otherwise a neutral dim grey.
    private string TapIconColor => EffectiveColor == OsdpLedColor.Off
        ? "#444"
        : LedCssColor;

    private string ProtocolBadgeFill => Protocol == ProtocolType.Osdp
        ? "#2563eb"
        : "#78716c";
}
