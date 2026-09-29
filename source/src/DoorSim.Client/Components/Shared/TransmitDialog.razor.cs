using MudBlazor;

namespace DoorSim.Client.Components.Shared;

public partial class TransmitDialog
{
    private string _bits = string.Empty;
    private uint _cardNumber;
    private int? _customFormatId;
    private string? _error;
    private ushort _facilityCode;
    private WiegandFormat _format;
    private bool _sending;
    private IReadOnlyCollection<int> _targets = [];

    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    /// <summary>The logged simulation being replayed. Must be one where <see cref="SimulationEventFormatting.IsResendable" /> is true.</summary>
    [Parameter]
    public SimulationEventRecord Record { get; set; } = null!;

    [Parameter]
    public DoorConfiguration[] Doors { get; set; } = [];

    [Parameter]
    public CustomCardFormat[] CustomFormats { get; set; } = [];

    private bool IsRawBits => Record.Kind == SimulationEventKind.RawBits;

    private bool OriginalDoorExists => Doors.Any(p => p.Id == Record.DoorId);

    private string OriginalLabel => string.IsNullOrEmpty(Record.Label)
        ? $"Door {Record.DoorId}"
        : Record.Label;

    private string? BitsError => _bits.Length == 0
        ? "Enter at least one bit"
        : _bits.Any(c => c is not ('0' or '1'))
            ? "Bits may only contain 0 and 1"
            : null;

    private bool PayloadValid => IsRawBits
        ? BitsError is null
        : _format != WiegandFormat.Custom || _customFormatId is not null;

    private bool CanTransmit => !_sending && _targets.Count > 0 && PayloadValid;

    protected override void OnInitialized() => Reset();

    private void Reset()
    {
        _bits = Record.RawBits ?? string.Empty;
        _cardNumber = Record.CardNumber ?? 0;
        _facilityCode = Record.FacilityCode ?? 0;
        _format = Record.Format ?? WiegandFormat.Wiegand26;
        _customFormatId = Record.CustomFormatId;
        _targets = OriginalDoorExists
            ? [Record.DoorId]
            : [];
        _error = null;
    }

    private string DoorLabel(int doorId) =>
        Doors.FirstOrDefault(p => p.Id == doorId)?.Label ?? $"Door {doorId}";

    private async Task TransmitAsync()
    {
        _sending = true;
        _error = null;

        var targets = _targets.ToArray();
        var failures = new List<string>();

        // Sequential on purpose: each call only enqueues, so this is quick, and it keeps
        // the per-reader error messages in a stable order.
        foreach (var readerId in targets)
        {
            try
            {
                await SendAsync(readerId);
            }
            catch (Exception ex)
            {
                failures.Add($"{DoorLabel(readerId)}: {ex.Message}");
            }
        }

        _sending = false;

        if (failures.Count == 0)
        {
            Snackbar.Add(targets.Length == 1
                    ? $"Sent to {DoorLabel(targets[0])}"
                    : $"Sent to {targets.Length} readers",
                Severity.Success);

            MudDialog.Close(DialogResult.Ok(true));
            return;
        }

        var sent = targets.Length - failures.Count;
        _error = sent == 0
            ? string.Join(" · ", failures)
            : $"Sent to {sent} of {targets.Length} — {string.Join(" · ", failures)}";

        if (sent > 0)
            Snackbar.Add($"Sent to {sent} of {targets.Length} readers", Severity.Warning);
    }

    private Task SendAsync(int readerId)
    {
        if (IsRawBits)
            return Sim.SendBitsAsync(new RawBitsRequest(readerId, _bits));

        var customFormatId = _format == WiegandFormat.Custom
            ? _customFormatId
            : null;

        return Record.Kind == SimulationEventKind.AccessCycle
            ? Sim.RunRawEventAsync(new RawDoorEventRequest(readerId, DoorEventType.AccessCycle, _cardNumber, _facilityCode, _format, customFormatId))
            : Sim.SendCardAsync(new RawCardRequest(readerId, _cardNumber, _facilityCode, _format, customFormatId));
    }

    private void Cancel() => MudDialog.Cancel();
}
