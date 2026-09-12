using DoorSim.Client.Components.Shared;
using MudBlazor;

namespace DoorSim.Client.Components.Pages;

public partial class ReaderPanel
{
    private CardEntry[] _cards = [];
    private readonly Dictionary<int, bool> _connectivity = [];

    private readonly CancellationTokenSource _cts = new();
    private readonly Dictionary<int, string?> _currentActions = [];
    private readonly Dictionary<int, bool> _doorOpen = [];
    private DoorConfiguration[] _doors = [];
    private readonly Dictionary<int, string?> _errors = [];
    private readonly Dictionary<int, ReaderLedState?> _ledStates = [];
    private bool _loading = true;
    private readonly Dictionary<int, int> _queueDepths = [];
    private readonly Dictionary<int, bool> _rexActive = [];
    private readonly Dictionary<int, int> _selectedCard = [];

    private readonly Dictionary<int, SimulationStatus> _statuses = [];

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();
        _cts.Dispose();
    }

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var doorsTask = Doors.GetAllAsync();
            var cardsTask = Cards.GetAllAsync();
            await Task.WhenAll(doorsTask, cardsTask);
            _doors = doorsTask.Result;
            _cards = cardsTask.Result;

            foreach (var door in _doors)
            {
                _statuses[door.Id] = SimulationStatus.Idle;
                _connectivity[door.Id] = false;
                _ledStates[door.Id] = null;
                _selectedCard[door.Id] = _cards.Length > 0
                    ? _cards[0].Id
                    : 0;

                _errors[door.Id] = null;
                _doorOpen[door.Id] = false;
                _rexActive[door.Id] = false;
                _queueDepths[door.Id] = 0;
                _currentActions[door.Id] = null;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error initializing reader panel: {ex.Message}");
        }
        finally
        {
            _loading = false;
        }

        _ = SubscribeToStatusStreamAsync(_cts.Token);
    }

    // ── SSE stream ───────────────────────────────────────────────────────────

    private async Task SubscribeToStatusStreamAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var updates in Sim.SubscribeAsync(ct))
            {
                foreach (var u in updates)
                {
                    if (_statuses.ContainsKey(u.DoorId))
                        _statuses[u.DoorId] = u.Status;

                    if (_connectivity.ContainsKey(u.DoorId))
                        _connectivity[u.DoorId] = u.IsConnected;

                    if (_ledStates.ContainsKey(u.DoorId))
                        _ledStates[u.DoorId] = u.LedState;

                    if (_doorOpen.ContainsKey(u.DoorId))
                        _doorOpen[u.DoorId] = u.DoorIsOpen;

                    if (_rexActive.ContainsKey(u.DoorId))
                        _rexActive[u.DoorId] = u.RexIsActive;

                    if (_queueDepths.ContainsKey(u.DoorId))
                        _queueDepths[u.DoorId] = u.QueueDepth;

                    if (_currentActions.ContainsKey(u.DoorId))
                        _currentActions[u.DoorId] = u.CurrentAction;
                }

                await InvokeAsync(StateHasChanged);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Console.WriteLine($"Status stream subscription error: {ex.Message}");
        }
    }

    // ── Simulation events ────────────────────────────────────────────────────

    private async Task SendEventAsync(int doorId, DoorEventType eventType)
    {
        _errors[doorId] = null;
        try
        {
            var cardId = _selectedCard[doorId];
            if (cardId == 0 && eventType != DoorEventType.EgressCycle)
            {
                _errors[doorId] = "Select a card first.";
                return;
            }

            var request = new DoorEventRequest(doorId, eventType,
                eventType == DoorEventType.EgressCycle
                    ? null
                    : cardId);

            await Sim.RunEventAsync(request);
        }
        catch (Exception ex)
        {
            _errors[doorId] = ex.Message;
        }
    }

    // ── Direct DPS / REX control ─────────────────────────────────────────────

    private async Task ToggleDoorAsync(int doorId)
    {
        _errors[doorId] = null;
        var wasOpen = _doorOpen.TryGetValue(doorId, out var o) && o;
        _doorOpen[doorId] = !wasOpen; // optimistic — stream confirms within 500 ms
        try
        {
            if (!wasOpen)
                await Sim.OpenDoorAsync(doorId);
            else
                await Sim.CloseDoorAsync(doorId);
        }
        catch (Exception ex)
        {
            _doorOpen[doorId] = wasOpen;
            _errors[doorId] = ex.Message;
        }
    }

    private async Task TriggerRexAsync(int doorId)
    {
        _errors[doorId] = null;
        try
        {
            await Sim.QuickRexAsync(doorId);
        }
        catch (Exception ex)
        {
            _errors[doorId] = ex.Message;
        }
    }

    private async Task OpenQueueDialogAsync(int doorId, string label)
    {
        var parameters = new DialogParameters<QueueDialog>
        {
            { p => p.DoorId, doorId },
            { p => p.DoorLabel, label },
        };

        var options = new DialogOptions
        {
            MaxWidth = MaxWidth.Medium,
            FullWidth = true,
            CloseButton = true,
        };

        await Dialog.ShowAsync<QueueDialog>($"Simulation Queue — {label}", parameters, options);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static bool HasDps(DoorConfiguration door) =>
        door.DpsPin.HasValue || door.DpsModbusChannel.HasValue;

    private static bool HasRex(DoorConfiguration door) =>
        door.RexPin.HasValue || door.RexModbusChannel.HasValue;

    private bool IsBusy(int doorId) =>
        _statuses.TryGetValue(doorId, out var p) && p == SimulationStatus.Running;

    private static Color StatusColor(SimulationStatus status) => status switch
    {
        SimulationStatus.Running => Color.Warning,
        SimulationStatus.Success => Color.Success,
        SimulationStatus.Error => Color.Error,
        var _ => Color.Default,
    };

    private static string StatusIcon(SimulationStatus status) => status switch
    {
        SimulationStatus.Running => Icons.Material.Filled.Schedule,
        SimulationStatus.Success => Icons.Material.Filled.CheckCircle,
        SimulationStatus.Error => Icons.Material.Filled.Error,
        var _ => Icons.Material.Filled.HourglassEmpty,
    };
}
