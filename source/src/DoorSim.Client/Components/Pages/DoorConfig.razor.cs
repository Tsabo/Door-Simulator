using DoorSim.Client.Components.Shared;
using MudBlazor;

namespace DoorSim.Client.Components.Pages;

public partial class DoorConfig
{
    private DoorConfiguration[] _doors = [];
    private string? _error;
    private bool _loading = true;

    private static DialogOptions DialogOptions => new()
    {
        MaxWidth = MaxWidth.Medium,
        FullWidth = true,
        CloseButton = true,
    };

    protected override async Task OnInitializedAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        _loading = true;
        try
        {
            _doors = await Doors.GetAllAsync();
        }
        catch (Exception ex)
        {
            _error = $"Failed to load door configuration: {ex.Message}";
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task OpenAddDialogAsync()
    {
        var parameters = new DialogParameters<DoorEditDialog>
        {
            { p => p.Editing, null },
        };

        var dialogRef = await Dialog.ShowAsync<DoorEditDialog>("Add Door", parameters, DialogOptions);
        var result = await dialogRef.Result;
        if (result is { Canceled: false })
            await LoadAsync();
    }

    private async Task OpenEditDialogAsync(DoorConfiguration door)
    {
        var parameters = new DialogParameters<DoorEditDialog>
        {
            { p => p.Editing, door },
        };

        var dialogRef = await Dialog.ShowAsync<DoorEditDialog>($"Edit: {door.Label}", parameters, DialogOptions);
        var result = await dialogRef.Result;
        if (result is { Canceled: false })
            await LoadAsync();
    }

    private async Task DeleteAsync(int id)
    {
        await Doors.DeleteAsync(id);
        await LoadAsync();
    }

    // "*" marks a door running a non-default rate, so overrides are visible at a glance.
    private static string FormatBaud(DoorConfiguration door)
    {
        if (door.Protocol != ProtocolType.Osdp)
            return "—";

        return door.OsdpBaudRate is null
            ? OsdpBaudRates.Default.ToString()
            : $"{door.OsdpBaudRate} *";
    }

    private static string FormatContact(int? gpioPin, int? modbusChannel)
    {
        if (modbusChannel.HasValue)
            return $"MB:ch{modbusChannel}";

        if (gpioPin.HasValue)
            return $"GPIO:{gpioPin}";

        return "—";
    }
}
