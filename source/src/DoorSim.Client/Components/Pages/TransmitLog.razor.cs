using DoorSim.Client.Components.Shared;
using MudBlazor;

namespace DoorSim.Client.Components.Pages;

public partial class TransmitLog : IDisposable
{
    private const string AllKinds = "all";
    private const string ResendableKinds = "resendable";

    private static readonly int[] Limits = [100, 250, 500, 1000];

    private static readonly DialogOptions DialogOptions = new()
    {
        MaxWidth = MaxWidth.Small,
        FullWidth = true,
        CloseButton = true,
    };

    private readonly CancellationTokenSource _cts = new();
    private CustomCardFormat[] _customFormats = [];
    private bool _dialogOpen;
    private int? _doorFilter;
    private DoorConfiguration[] _doors = [];
    private string? _error;
    private SimulationEventRecord[] _events = [];
    private string _kindFilter = AllKinds;
    private int _limit = 100;
    private bool _loading = true;

    private IEnumerable<SimulationEventRecord> FilteredEvents => _kindFilter switch
    {
        AllKinds => _events,
        ResendableKinds => _events.Where(SimulationEventFormatting.IsResendable),
        var kind => _events.Where(p => p.Kind.ToString() == kind),
    };

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var doorsTask = DoorClient.GetAllAsync();
            var formatsTask = FormatClient.GetAllAsync();
            await Task.WhenAll(doorsTask, formatsTask);
            _doors = doorsTask.Result;
            _customFormats = formatsTask.Result;
        }
        catch (Exception ex)
        {
            _error = $"Failed to load doors and card formats: {ex.Message}";
        }

        await RefreshAsync();
        _ = AutoRefreshAsync();
    }

    private async Task AutoRefreshAsync()
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));

        try
        {
            while (await timer.WaitForNextTickAsync(_cts.Token))
            {
                // Don't shuffle rows underneath an open dialog.
                if (_dialogOpen)
                    continue;

                await RefreshAsync();
                await InvokeAsync(StateHasChanged);
            }
        }
        catch (OperationCanceledException)
        {
            // Page was disposed
        }
    }

    private async Task RefreshAsync()
    {
        _loading = true;

        try
        {
            _events = await MetricsClient.GetRecentEventsAsync(_limit, _doorFilter);
            _error = null;
        }
        catch (Exception ex)
        {
            _error = $"Failed to load the transmit log: {ex.Message}";
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task OnDoorFilterChanged(int? doorId)
    {
        _doorFilter = doorId;
        await RefreshAsync();
    }

    private async Task OnLimitChanged(int limit)
    {
        _limit = limit;
        await RefreshAsync();
    }

    private async Task OnRowClickAsync(TableRowClickEventArgs<SimulationEventRecord> args)
    {
        if (args.Item is not { } record || !SimulationEventFormatting.IsResendable(record))
            return;

        var parameters = new DialogParameters<TransmitDialog>
        {
            { p => p.Record, record },
            { p => p.Doors, _doors },
            { p => p.CustomFormats, _customFormats },
        };

        _dialogOpen = true;
        try
        {
            var dialogRef = await Dialog.ShowAsync<TransmitDialog>(
                $"Resend {SimulationEventFormatting.SplitPascal(record.Kind.ToString())}", parameters, DialogOptions);

            var result = await dialogRef.Result;
            if (result is { Canceled: false })
                await RefreshAsync();
        }
        finally
        {
            _dialogOpen = false;
        }
    }

    private static string RowClass(SimulationEventRecord record, int index) =>
        SimulationEventFormatting.IsResendable(record)
            ? "transmit-row"
            : "transmit-row transmit-row-disabled";

    private static string PayloadLabel(SimulationEventRecord record)
    {
        if (record.RawBits is not null)
            return record.RawBits.Length <= 40
                ? record.RawBits
                : $"{record.RawBits[..40]}… ({record.RawBits.Length} bits)";

        return SimulationEventFormatting.CredentialLabel(record);
    }
}
