using MudBlazor;

namespace DoorSim.Client.Components.Shared;

/// <summary>Display helpers shared by the pages that list <see cref="SimulationEventRecord" /> rows.</summary>
public static class SimulationEventFormatting
{
    public static string CredentialLabel(SimulationEventRecord record)
    {
        if (record.RawBits is not null)
            return $"{record.RawBits.Length} raw bits";

        return record.CardNumber.HasValue
            ? $"#{record.CardNumber} FC:{record.FacilityCode} ({record.Format})"
            : "—";
    }

    public static Color OutcomeColor(SimulationEventOutcome outcome) => outcome switch
    {
        SimulationEventOutcome.Success => Color.Success,
        SimulationEventOutcome.Cancelled => Color.Warning,
        var _ => Color.Error,
    };

    public static string SplitPascal(string value) =>
        string.Concat(value.Select((p, i) => i > 0 && char.IsUpper(p)
            ? " " + p
            : p.ToString()));

    /// <summary>
    /// Only credential sends can be replayed from the transmit log; egress and the
    /// door/REX primitives carry no payload worth editing.
    /// </summary>
    public static bool IsResendable(SimulationEventRecord record) => record.Kind switch
    {
        SimulationEventKind.CardReadOnly or SimulationEventKind.AccessCycle => record.CardNumber.HasValue && record.Format.HasValue,
        SimulationEventKind.RawBits => !string.IsNullOrEmpty(record.RawBits),
        var _ => false,
    };
}
