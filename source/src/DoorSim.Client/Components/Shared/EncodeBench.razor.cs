namespace DoorSim.Client.Components.Shared;

/// <summary>Encode half of the test bench. All values are computed by the page and passed in.</summary>
public partial class EncodeBench
{
    [Parameter]
    public string CardNumber { get; set; } = "";

    [Parameter]
    public EventCallback<string> CardNumberChanged { get; set; }

    [Parameter]
    public string FacilityCode { get; set; } = "";

    [Parameter]
    public EventCallback<string> FacilityCodeChanged { get; set; }

    [Parameter]
    [EditorRequired]
    public int Bits { get; set; }

    /// <summary>Null when the current mask set cannot encode at all.</summary>
    [Parameter]
    public string? Hex { get; set; }

    [Parameter]
    public ulong Frame { get; set; }

    [Parameter]
    public IReadOnlyList<int> ParityBits { get; set; } = [];

    [Parameter]
    public bool RoundTripOk { get; set; }

    /// <summary>Bit positions in display order — most significant first, matching the bit grid.</summary>
    private IEnumerable<int> BitsHighToLow()
    {
        for (var bit = Bits - 1; bit >= 0; bit--)
            yield return bit;
    }

    private bool IsSet(int bit) =>
        bit < CardFormatEncoder.MaxFrameBits && (Frame & 1UL << bit) != 0;

    private string BitValue(int bit) => IsSet(bit)
        ? "1"
        : "0";

    private string BitTitle(int bit) =>
        ParityBits.Contains(bit)
            ? $"bit {bit} — parity"
            : $"bit {bit}";

    private string StripClass(int bit)
    {
        if (ParityBits.Contains(bit))
            return "strip-bit--parity";

        return IsSet(bit)
            ? "strip-bit--set"
            : "strip-bit--clear";
    }
}
