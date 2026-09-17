namespace DoorSim.Shared.Models;

/// <summary>
/// A user-defined Wiegand-style bit-frame format, described by a card mask and up to three
/// parity masks (see <c>CardFormatEncoder</c> for the mask syntax). Selected via
/// <see cref="WiegandFormat.Custom" /> plus the matching format's <see cref="Id" />.
/// </summary>
public record CustomCardFormat(
    int Id,
    string Name,
    string CardMask,
    string? Parity1Mask,
    string? Parity2Mask,
    string? Parity3Mask,
    DateTimeOffset CreatedAt);
