namespace DoorSim.Shared.Models;

public record CardEntry(
    int Id,
    string Label,
    ushort FacilityCode,
    uint CardNumber,
    WiegandFormat Format,
    DateTimeOffset CreatedAt,
    int? CustomFormatId = null);
