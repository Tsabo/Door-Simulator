namespace DoorSim.Client.Components.Pages;

public partial class DoorConfig
{
    private string[] _availableSerialPorts = [];
    private DoorConfiguration[] _doors = [];
    private DoorConfiguration? _editing;
    private string? _error;
    private DoorFormModel _form = new();
    private bool _loading = true;
    private bool _saving;

    protected override async Task OnInitializedAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        _loading = true;
        try
        {
            _doors = await Doors.GetAllAsync();
            await LoadAvailableSerialPortsAsync(_editing?.Id);
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

    private async Task BeginEditAsync(DoorConfiguration door)
    {
        _editing = door;
        _form = new DoorFormModel
        {
            Label = door.Label,
            Protocol = door.Protocol,
            D0Pin = door.D0Pin,
            D1Pin = door.D1Pin,
            OsdpAddress = door.OsdpAddress,
            OsdpSerialPort = door.OsdpSerialPort,
            OsdpBaudRate = door.OsdpBaudRate,
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

        _error = null;
        await LoadAvailableSerialPortsAsync(_editing?.Id);
    }

    private async Task CancelEditAsync()
    {
        _editing = null;
        _form = new DoorFormModel();
        _error = null;
        await LoadAvailableSerialPortsAsync(null);
    }

    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(_form.Label))
        {
            _error = "Label is required.";
            return;
        }

        _saving = true;
        _error = null;

        try
        {
            var anyModbus = _form is { HasDps: true, DpsModeIsModbus: true } or { HasRex: true, RexModeIsModbus: true };

            var dto = new DoorConfiguration(
                _editing?.Id ?? 0,
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

            if (_editing is null)
            {
                var (result, error) = await Doors.CreateAsync(dto);
                if (error is not null)
                {
                    _error = error;
                    return;
                }
            }
            else
            {
                var (result, error) = await Doors.UpdateAsync(_editing.Id, dto);
                if (error is not null)
                {
                    _error = error;
                    return;
                }
            }

            _editing = null;
            _form = new DoorFormModel();
            await LoadAsync();
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

    private async Task DeleteAsync(int id)
    {
        await Doors.DeleteAsync(id);
        await LoadAsync();
    }

    private async Task LoadAvailableSerialPortsAsync(int? excludeDoorId) => _availableSerialPorts = await Doors.GetAvailableSerialPortsAsync(excludeDoorId);

    private Task OnHasDpsChanged(bool value)
    {
        _form.HasDps = value;
        return Task.CompletedTask;
    }

    private Task OnDpsModeChanged(bool value)
    {
        _form.DpsModeIsModbus = value;
        return Task.CompletedTask;
    }

    private Task OnHasRexChanged(bool value)
    {
        _form.HasRex = value;
        return Task.CompletedTask;
    }

    private Task OnRexModeChanged(bool value)
    {
        _form.RexModeIsModbus = value;
        return Task.CompletedTask;
    }

    private Task OnModbusTransportChanged(bool isTcp)
    {
        _form.ModbusTransportIsTcp = isTcp;
        return Task.CompletedTask;
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

    private sealed class DoorFormModel
    {
        public string Label { get; set; } = string.Empty;
        public ProtocolType Protocol { get; set; } = ProtocolType.Wiegand;
        public int? D0Pin { get; set; }
        public int? D1Pin { get; set; }
        public byte? OsdpAddress { get; set; }
        public string? OsdpSerialPort { get; set; }
        public int? OsdpBaudRate { get; set; }
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
