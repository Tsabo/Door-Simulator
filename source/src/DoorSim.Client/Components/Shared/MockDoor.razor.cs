namespace DoorSim.Client.Components.Shared;

public partial class MockDoor
{
    private readonly string _uid = Guid.NewGuid().ToString("N")[..8];

    [Parameter]
    [EditorRequired]
    public bool HasDps { get; set; }

    [Parameter]
    public bool HasRex { get; set; }

    [Parameter]
    public bool DoorIsOpen { get; set; }

    [Parameter]
    public bool RexIsActive { get; set; }

    [Parameter]
    public bool DpsNormallyOpen { get; set; }

    [Parameter]
    public bool RexNormallyOpen { get; set; }

    [Parameter]
    public bool Busy { get; set; }

    [Parameter]
    public EventCallback OnDoorToggle { get; set; }

    [Parameter]
    public EventCallback OnRexTrigger { get; set; }

    private Task HandleDoorToggle()
    {
        return OnDoorToggle.HasDelegate
            ? OnDoorToggle.InvokeAsync()
            : Task.CompletedTask;
    }

    private Task HandleRexTrigger()
    {
        return OnRexTrigger.HasDelegate
            ? OnRexTrigger.InvokeAsync()
            : Task.CompletedTask;
    }
}
