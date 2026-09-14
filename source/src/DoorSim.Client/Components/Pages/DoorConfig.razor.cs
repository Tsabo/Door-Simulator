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
        CloseButton = true
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
            { p => p.Editing, null }
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
            { p => p.Editing, door }
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

    /// <summary>
    /// Names the groups of advanced OSDP settings a door has overridden, or null when it is
    /// running a stock PD personality — so a door that declares something unusual is visible
    /// without opening the edit dialog.
    /// </summary>
    /// <remarks>
    /// A new advanced setting that no group below checks will never show up here. Any addition
    /// to the advanced settings needs a corresponding term in one of these three predicates.
    /// </remarks>
    private static string? FormatAdvancedOverrides(DoorConfiguration door)
    {
        if (door.Protocol != ProtocolType.Osdp)
            return null;

        var groups = new List<string>();

        if (door.OsdpCapContactStatusCompliance is not null
            || door.OsdpCapContactStatusInputs is not null
            || door.OsdpCapOutputControlCompliance is not null
            || door.OsdpCapOutputControlCount is not null
            || door.OsdpCapAudibleOutputCompliance is not null
            || door.OsdpCapTextOutputCompliance is not null
            || door.OsdpCapTextOutputDisplays is not null
            || door.OsdpCapCardDataFormatCompliance is not null
            || door.OsdpCapLedControlCompliance is not null
            || door.OsdpCapLedsPerReader is not null
            || door.OsdpCapCheckCharacterCompliance is not null
            || door.OsdpCapDeclareAes128
            || door.OsdpCapDeclareDefaultAesKey
            || door.OsdpCapReceiveBufferSize is not null
            || door.OsdpCapLargestCombinedMessageSize is not null
            || door.OsdpCapOsdpVersion is not null
            || door.OsdpCapDownstreamReaders is not null)
            groups.Add("Caps");

        if (door.OsdpIdVendorCode is not null
            || door.OsdpIdModelNumber is not null
            || door.OsdpIdHardwareVersion is not null
            || door.OsdpIdSerialNumber is not null
            || door.OsdpIdFirmwareMajor is not null
            || door.OsdpIdFirmwareMinor is not null
            || door.OsdpIdFirmwareBuild is not null)
            groups.Add("ID");

        if (door.OsdpComsetHandling != OsdpComsetBehavior.Ignore
            || door.OsdpNakManufacturerCommand
            || door.OsdpConnectionTimeoutSeconds is not null
            || door.OsdpReplyTimeoutMilliseconds is not null)
            groups.Add("Behaviour");

        return groups.Count == 0
            ? null
            : string.Join(", ", groups);
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
