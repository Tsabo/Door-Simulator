namespace DoorSim.Shared.Models;

public record DoorEventRequest(int ReaderId, DoorEventType EventType, int? CardEntryId = null);

/// <summary>DoorEventRequest with raw card values — no library lookup.</summary>
public record RawDoorEventRequest(
    int ReaderId,
    DoorEventType EventType,
    uint CardNumber,
    ushort FacilityCode,
    WiegandFormat Format);

public enum DoorEventType
{
    CardReadOnly, // Wiegand send only — no door movement
    AccessCycle, // Card read + door open + door close
    EgressCycle // REX trip + door open + door close + REX reset
}
