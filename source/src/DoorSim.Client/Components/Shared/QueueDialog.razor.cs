using MudBlazor;

namespace DoorSim.Client.Components.Shared;

public partial class QueueDialog
{
    private readonly CancellationTokenSource _cts = new();
    private SimulationQueueItemDto[] _items = [];
    private bool _loading = true;

    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Parameter]
    public int DoorId { get; set; }

    [Parameter]
    public string DoorLabel { get; set; } = string.Empty;

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();
        _cts.Dispose();
    }

    protected override async Task OnInitializedAsync()
    {
        await LoadQueueAsync();
        _ = SubscribeToQueueStreamAsync(_cts.Token);
    }

    private async Task SubscribeToQueueStreamAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var updates in Sim.SubscribeAsync(ct))
            {
                var matching = updates.FirstOrDefault(p => p.DoorId == DoorId);
                if (matching?.Queue == null)
                    continue;

                _items = [.. matching.Queue];
                _loading = false;
                await InvokeAsync(StateHasChanged);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Console.WriteLine($"Queue stream subscription error: {ex.Message}");
        }
    }

    private async Task LoadQueueAsync()
    {
        _loading = true;
        try
        {
            _items = await Sim.GetQueueAsync(DoorId);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading queue: {ex.Message}");
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task RefreshAsync() => await LoadQueueAsync();

    private async Task CancelItemAsync(Guid itemId)
    {
        try
        {
            await Sim.CancelQueueItemAsync(DoorId, itemId);
            await LoadQueueAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error cancelling queue item: {ex.Message}");
        }
    }

    private async Task ClearAllAsync()
    {
        try
        {
            await Sim.ClearQueueAsync(DoorId);
            await LoadQueueAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error clearing queue: {ex.Message}");
        }
    }

    private void Close() => MudDialog.Close(DialogResult.Ok(true));
}
