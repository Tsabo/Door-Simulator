namespace DoorSim.Shared.Models;

public record SwipeRequest(int ReaderId, int CardEntryId);

/// <summary>Ad-hoc card send — no library entry required.</summary>
public record RawCardRequest(
    int ReaderId,
    uint CardNumber,
    ushort FacilityCode,
    WiegandFormat Format);

/// <summary>Raw bit-stream card send — transmits literal bits without format calculation.</summary>
public record RawBitsRequest(
    int ReaderId,
    string Bits);
