using MudBlazor;

namespace DoorSim.Client.Components.Shared;

public partial class DoorEditDialog
{
    private int _activeTabIndex;
    private string[] _availableSerialPorts = [];
    private string? _error;
    private DoorFormModel _form = new();
    private bool _saving;

    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Parameter]
    public DoorConfiguration? Editing { get; set; }

    private bool AnyModbus => _form is { HasDps: true, DpsModeIsModbus: true } or { HasRex: true, RexModeIsModbus: true };

    protected override async Task OnInitializedAsync()
    {
        if (Editing is { } door)
        {
            _form = new DoorFormModel
            {
                Label = door.Label,
                Protocol = door.Protocol,
                D0Pin = door.D0Pin,
                D1Pin = door.D1Pin,
                OsdpAddress = door.OsdpAddress,
                OsdpSerialPort = door.OsdpSerialPort,
                OsdpBaudRate = door.OsdpBaudRate,
                OsdpNakManufacturerCommand = door.OsdpNakManufacturerCommand,
                HasDps = door.DpsPin.HasValue || door.DpsModbusChannel.HasValue,
                DpsPin = door.DpsPin,
                DpsModeIsModbus = door.DpsModbusChannel.HasValue,
                DpsModbusChannel = door.DpsModbusChannel,
                DpsNormallyOpen = door.DpsNormallyOpen,
                HasRex = door.RexPin.HasValue || door.RexModbusChannel.HasValue,
                RexPin = door.RexPin,
                RexModeIsModbus = door.RexModbusChannel.HasValue,
                RexModbusChannel = door.RexModbusChannel,
                RexNormallyOpen = door.RexNormallyOpen,
                ModbusTransportIsTcp = door.ModbusTcpHost is not null,
                ModbusSerialPort = door.ModbusSerialPort,
                ModbusTcpHost = door.ModbusTcpHost,
                ModbusTcpPort = door.ModbusTcpPort,
                ModbusUnitId = door.ModbusUnitId,
            };
        }

        await LoadAvailableSerialPortsAsync();
    }

    private async Task LoadAvailableSerialPortsAsync() => _availableSerialPorts = await Doors.GetAvailableSerialPortsAsync(Editing?.Id);

    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(_form.Label))
        {
            _error = "Label is required.";
            _activeTabIndex = 0;
            return;
        }

        _saving = true;
        _error = null;

        try
        {
            var anyModbus = AnyModbus;

            var dto = new DoorConfiguration(
                Editing?.Id ?? 0,
                _form.Label.Trim(),
                _form.Protocol,
                _form.Protocol == ProtocolType.Wiegand
                    ? _form.D0Pin
                    : null,
                _form.Protocol == ProtocolType.Wiegand
                    ? _form.D1Pin
                    : null,
                _form.Protocol == ProtocolType.Osdp
                    ? _form.OsdpAddress
                    : null,
                _form.Protocol == ProtocolType.Osdp
                    ? _form.OsdpSerialPort
                    : null,
                _form.Protocol == ProtocolType.Osdp
                    ? _form.OsdpBaudRate
                    : null,
                _form is { HasDps: true, DpsModeIsModbus: false }
                    ? _form.DpsPin
                    : null,
                _form is { HasRex: true, RexModeIsModbus: false }
                    ? _form.RexPin
                    : null,
                DpsNormallyOpen: _form is { HasDps: true, DpsNormallyOpen: true },
                RexNormallyOpen: _form is { HasRex: true, RexNormallyOpen: true },
                OsdpNakManufacturerCommand: _form is { Protocol: ProtocolType.Osdp, OsdpNakManufacturerCommand: true },
                ModbusSerialPort: anyModbus && !_form.ModbusTransportIsTcp
                    ? _form.ModbusSerialPort
                    : null,
                ModbusUnitId: anyModbus
                    ? _form.ModbusUnitId
                    : null,
                DpsModbusChannel: _form is { HasDps: true, DpsModeIsModbus: true }
                    ? _form.DpsModbusChannel
                    : null,
                RexModbusChannel: _form is { HasRex: true, RexModeIsModbus: true }
                    ? _form.RexModbusChannel
                    : null,
                ModbusTcpHost: anyModbus && _form.ModbusTransportIsTcp
                    ? _form.ModbusTcpHost
                    : null,
                ModbusTcpPort: anyModbus && _form.ModbusTransportIsTcp
                    ? _form.ModbusTcpPort
                    : null);

            if (Editing is null)
            {
                var (_, error) = await Doors.CreateAsync(dto);
                if (error is not null)
                {
                    _error = error;
                    return;
                }
            }
            else
            {
                var (_, error) = await Doors.UpdateAsync(Editing.Id, dto);
                if (error is not null)
                {
                    _error = error;
                    return;
                }
            }

            MudDialog.Close(DialogResult.Ok(true));
        }
        catch (Exception ex)
        {
            _error = ex.Message;
        }
        finally
        {
            _saving = false;
        }
    }

    private void Cancel() => MudDialog.Close(DialogResult.Cancel());

    private Task OnHasDpsChanged(bool value)
    {
        _form.HasDps = value;
        ResetActiveTabIfRelayBoardDisabled();
        return Task.CompletedTask;
    }

    private Task OnDpsModeChanged(bool value)
    {
        _form.DpsModeIsModbus = value;
        ResetActiveTabIfRelayBoardDisabled();
        return Task.CompletedTask;
    }

    private Task OnHasRexChanged(bool value)
    {
        _form.HasRex = value;
        ResetActiveTabIfRelayBoardDisabled();
        return Task.CompletedTask;
    }

    private Task OnRexModeChanged(bool value)
    {
        _form.RexModeIsModbus = value;
        ResetActiveTabIfRelayBoardDisabled();
        return Task.CompletedTask;
    }

    private Task OnModbusTransportChanged(bool isTcp)
    {
        _form.ModbusTransportIsTcp = isTcp;
        return Task.CompletedTask;
    }

    private void ResetActiveTabIfRelayBoardDisabled()
    {
        if (_activeTabIndex == 3 && !AnyModbus)
            _activeTabIndex = 0;
    }

    private sealed class DoorFormModel
    {
        public string Label { get; set; } = string.Empty;
        public ProtocolType Protocol { get; set; } = ProtocolType.Wiegand;
        public int? D0Pin { get; set; }
        public int? D1Pin { get; set; }
        public byte? OsdpAddress { get; set; }
        public string? OsdpSerialPort { get; set; }
        public int? OsdpBaudRate { get; set; }
        public bool OsdpNakManufacturerCommand { get; set; }
        public bool HasDps { get; set; }
        public int? DpsPin { get; set; }
        public bool DpsModeIsModbus { get; set; }
        public int? DpsModbusChannel { get; set; }
        public bool DpsNormallyOpen { get; set; }
        public bool HasRex { get; set; }
        public int? RexPin { get; set; }
        public bool RexModeIsModbus { get; set; }
        public int? RexModbusChannel { get; set; }
        public bool RexNormallyOpen { get; set; }
        public bool ModbusTransportIsTcp { get; set; }
        public string? ModbusSerialPort { get; set; }
        public string? ModbusTcpHost { get; set; }
        public int? ModbusTcpPort { get; set; }
        public byte? ModbusUnitId { get; set; }
    }
}
