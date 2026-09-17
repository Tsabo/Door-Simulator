using MudBlazor;

namespace DoorSim.Client.Components.Shared;

/// <summary>What decoding a credential produced, or null when the input isn't usable hex.</summary>
public sealed record DecodeResult(
    ulong CardNumber,
    ulong FacilityCode,
    IReadOnlyList<ParityResult> Parity)
{
    public bool ParityOk => Parity.All(p => p.Ok);
}

/// <summary>Decode half of the test bench.</summary>
public partial class DecodeBench
{
    [Parameter]
    public string Hex { get; set; } = "";

    [Parameter]
    public EventCallback<string> HexChanged { get; set; }

    /// <summary>Null when <see cref="Hex" /> isn't valid hex, or the mask set can't decode.</summary>
    [Parameter]
    public DecodeResult? Result { get; set; }

    private bool IsEmpty => string.IsNullOrWhiteSpace(Hex);

    private Color StatusColor => Result is null
        ? IsEmpty
            ? Color.Default
            : Color.Warning
        : Result.ParityOk
            ? Color.Success
            : Color.Error;

    private string StatusLabel => Result is null
        ? IsEmpty
            ? "no input"
            : "not hex"
        : Result.ParityOk
            ? "parity ok"
            : "parity failed";

    private static string Describe(ParityResult rule)
    {
        var position = rule.BitIndex < 0
            ? "no bit"
            : $"bit {rule.BitIndex}";

        return $"{position} · {(rule.IsEven ? "even" : "odd")} · {rule.BitsSet} bits set";
    }
}
