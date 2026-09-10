namespace DoorSim.Shared.Models;

public record SwipeRequest(int ReaderId, int CardEntryId);

/// <summary>Ad-hoc card send — no library entry required.</summary>
public record RawCardRequest(
    int ReaderId,
    uint CardNumber,
    ushort FacilityCode,
    WiegandFormat Format);
