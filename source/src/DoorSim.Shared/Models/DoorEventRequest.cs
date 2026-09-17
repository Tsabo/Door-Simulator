using System.ComponentModel;

namespace DoorSim.Shared.Models;

public record DoorEventRequest(int ReaderId, DoorEventType EventType, int? CardEntryId = null);

/// <summary>DoorEventRequest with raw card values — no library lookup.</summary>
public record RawDoorEventRequest(
    int ReaderId,
    DoorEventType EventType,
    uint CardNumber,
    ushort FacilityCode,
    WiegandFormat Format,
    int? CustomFormatId = null);

[Description("What kind of event to simulate.")]
public enum DoorEventType
{
    [Description("Wiegand card send only — no door movement.")]
    CardReadOnly,

    [Description("Card read followed by a door open + door close cycle.")]
    AccessCycle,

    [Description("REX trip followed by a door open + door close cycle, then REX reset.")]
    EgressCycle
}
