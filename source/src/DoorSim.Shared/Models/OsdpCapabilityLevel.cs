namespace DoorSim.Shared.Models;

/// <summary>
/// One legal compliance-level value for an osdp_CAP function, paired with the specification's
/// description of what that value means.
/// </summary>
/// <remarks>
/// The authoritative source for these values is the XML documentation on OSDP.Net's
/// <c>CapabilityFunction</c> enum, which the Blazor client cannot reference. Transcribing them
/// into this shared type once means the UI selects and the server-side validator are driven by
/// the same tables and cannot disagree about what is legal.
/// </remarks>
public readonly record struct OsdpCapabilityLevel(byte Value, string Label);
