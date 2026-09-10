namespace DoorSim.Data.Entities;

/// <summary>Single-row table that persists global simulation timing settings.</summary>
public class SimulationSettingsEntity
{
    public int Id { get; set; } = 1;
    public int CardToDoorDelayMs { get; set; } = 500;
    public int DoorOpenMs { get; set; } = 5000;
    public int RexLeadMs { get; set; } = 500;
    public int QuickRexMs { get; set; } = 2000;

    public SimulationTimingSettings ToDto() =>
        new(CardToDoorDelayMs, DoorOpenMs, RexLeadMs, QuickRexMs);
}
