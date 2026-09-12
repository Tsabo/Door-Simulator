namespace DoorSim.Hardware;

/// <summary>
/// Owns and drives the DPS (Door Position Switch) and REX (Request to Exit) contacts
/// for a single simulated door. Supports three modes per contact:
/// <list type="bullet">
///   <item>GPIO — direct pin drive via the shared <see cref="GpioController"/>.</item>
///   <item>Modbus — relay coil via <see cref="ModbusRelayService"/> on an RS485 board.</item>
///   <item>None — no hardware wired; operations are silent no-ops.</item>
/// </list>
/// NC (normally-closed) relay convention applies to both modes:
/// idle = contact closed = HIGH / coil OFF; active = contact open = LOW / coil ON.
/// </summary>
public sealed class DoorContactController : IDisposable
{
    private readonly DoorConfiguration _config;
    private readonly GpioController? _gpio;
    private readonly ModbusRelayService? _modbus;
    private readonly ModbusTcpRelayService? _modbusTcp;
    private readonly ILogger _logger;

    private bool _hasDpsGpio;
    private bool _hasRexGpio;

    public DoorContactController(
        DoorConfiguration config,
        GpioController? gpio,
        ModbusRelayService? modbus,
        ModbusTcpRelayService? modbusTcp,
        ILogger logger)
    {
        _config    = config;
        _gpio      = gpio;
        _modbus    = modbus;
        _modbusTcp = modbusTcp;
        _logger    = logger;

        InitializePins();
    }

    // -------------------------------------------------------------------------
    // DPS — Door Position Switch
    // -------------------------------------------------------------------------

    /// <summary>Assert DPS — relay trips, door open signal to panel.</summary>
    /// <remarks>NC: coil ON / GPIO LOW. NO: coil OFF / GPIO HIGH (polarity inverted).</remarks>
    public Task OpenDoorAsync()
    {
        if (_config.DpsModbusChannel.HasValue)
            return SetRelayCoilAsync(_config.DpsModbusChannel.Value, active: !_config.DpsNormallyOpen, "DPS");

        if (_hasDpsGpio)
            SetGpioPin(_config.DpsPin!.Value, _config.DpsNormallyOpen ? PinValue.High : PinValue.Low, "DPS");

        return Task.CompletedTask;
    }

    /// <summary>Release DPS — relay returns to idle, door closed signal to panel.</summary>
    public Task CloseDoorAsync()
    {
        if (_config.DpsModbusChannel.HasValue)
            return SetRelayCoilAsync(_config.DpsModbusChannel.Value, active: _config.DpsNormallyOpen, "DPS");

        if (_hasDpsGpio)
            SetGpioPin(_config.DpsPin!.Value, _config.DpsNormallyOpen ? PinValue.Low : PinValue.High, "DPS");

        return Task.CompletedTask;
    }

    // -------------------------------------------------------------------------
    // REX — Request to Exit
    // -------------------------------------------------------------------------

    /// <summary>Assert REX — relay trips, egress/motion signal to panel.</summary>
    /// <remarks>NC: coil ON / GPIO LOW. NO: coil OFF / GPIO HIGH (polarity inverted).</remarks>
    public Task TripRexAsync()
    {
        if (_config.RexModbusChannel.HasValue)
            return SetRelayCoilAsync(_config.RexModbusChannel.Value, active: !_config.RexNormallyOpen, "REX");

        if (_hasRexGpio)
            SetGpioPin(_config.RexPin!.Value, _config.RexNormallyOpen ? PinValue.High : PinValue.Low, "REX");

        return Task.CompletedTask;
    }

    /// <summary>Release REX — relay returns to idle, egress signal cleared.</summary>
    public Task ResetRexAsync()
    {
        if (_config.RexModbusChannel.HasValue)
            return SetRelayCoilAsync(_config.RexModbusChannel.Value, active: _config.RexNormallyOpen, "REX");

        if (_hasRexGpio)
            SetGpioPin(_config.RexPin!.Value, _config.RexNormallyOpen ? PinValue.Low : PinValue.High, "REX");

        return Task.CompletedTask;
    }

    // -------------------------------------------------------------------------
    // IDisposable
    // -------------------------------------------------------------------------

    public void Dispose()
    {
        if (_gpio is null) return;

        foreach (var (pin, name) in new (int?, string)[] { (_config.DpsPin, "DPS"), (_config.RexPin, "REX") })
        {
            if (pin is null) continue;
            try
            {
                _gpio.Write(pin.Value, PinValue.High); // return to idle
                _gpio.ClosePin(pin.Value);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Door {Id}: error closing {Name} pin {Pin}", _config.Id, name, pin);
            }
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private void InitializePins()
    {
        // Log DPS mode
        if (_config.DpsModbusChannel.HasValue)
        {
            if (_config.ModbusTcpHost is not null)
                _logger.LogInformation(
                    "Door {Id} ({Label}): DPS → Modbus TCP relay ch {Ch} on {Host}:{Port} unit {Unit}",
                    _config.Id, _config.Label,
                    _config.DpsModbusChannel, _config.ModbusTcpHost,
                    _config.ModbusTcpPort ?? 502, _config.ModbusUnitId);
            else
                _logger.LogInformation(
                    "Door {Id} ({Label}): DPS → Modbus RTU relay ch {Ch} on {Port} unit {Unit}",
                    _config.Id, _config.Label,
                    _config.DpsModbusChannel, _config.ModbusSerialPort, _config.ModbusUnitId);
        }
        else if (_config.DpsPin.HasValue)
            _logger.LogInformation(
                "Door {Id} ({Label}): DPS → GPIO pin {Pin}",
                _config.Id, _config.Label, _config.DpsPin);
        else
            _logger.LogWarning(
                "Door {Id} ({Label}): DPS → no-op (no GPIO pin or Modbus channel configured)",
                _config.Id, _config.Label);

        // Log REX mode
        if (_config.RexModbusChannel.HasValue)
        {
            if (_config.ModbusTcpHost is not null)
                _logger.LogInformation(
                    "Door {Id} ({Label}): REX → Modbus TCP relay ch {Ch} on {Host}:{Port} unit {Unit}",
                    _config.Id, _config.Label,
                    _config.RexModbusChannel, _config.ModbusTcpHost,
                    _config.ModbusTcpPort ?? 502, _config.ModbusUnitId);
            else
                _logger.LogInformation(
                    "Door {Id} ({Label}): REX → Modbus RTU relay ch {Ch} on {Port} unit {Unit}",
                    _config.Id, _config.Label,
                    _config.RexModbusChannel, _config.ModbusSerialPort, _config.ModbusUnitId);
        }
        else if (_config.RexPin.HasValue)
            _logger.LogInformation(
                "Door {Id} ({Label}): REX → GPIO pin {Pin}",
                _config.Id, _config.Label, _config.RexPin);
        else
            _logger.LogWarning(
                "Door {Id} ({Label}): REX → no-op (no GPIO pin or Modbus channel configured)",
                _config.Id, _config.Label);

        if (_gpio is null) return;

        _hasDpsGpio = TryOpenPin(_config.DpsPin, "DPS");
        _hasRexGpio = TryOpenPin(_config.RexPin, "REX");
    }

    private bool TryOpenPin(int? pin, string name)
    {
        if (pin is null || _gpio is null) return false;

        // Skip GPIO if this contact is handled via Modbus
        bool isModbus = name == "DPS"
            ? _config.DpsModbusChannel.HasValue
            : _config.RexModbusChannel.HasValue;
        if (isModbus) return false;

        try
        {
            _gpio.OpenPin(pin.Value, PinMode.Output, PinValue.High);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Door {Id}: failed to open {Name} pin {Pin}", _config.Id, name, pin);
            return false;
        }
    }

    private void SetGpioPin(int pin, PinValue value, string name)
    {
        if (_gpio is null) return;
        try
        {
            _gpio.Write(pin, value);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Door {Id}: failed to write {Name} pin {Pin}", _config.Id, name, pin);
        }
    }

    /// <summary>
    /// Routes a relay coil write to the appropriate Modbus transport.
    /// TCP takes precedence over RTU when ModbusTcpHost is set.
    /// </summary>
    private async Task SetRelayCoilAsync(int channel, bool active, string name)
    {
        if (_config.ModbusUnitId is null)
        {
            _logger.LogWarning(
                "Door {Id}: {Name} Modbus channel {Ch} has no unit ID configured — skipping",
                _config.Id, name, channel);
            return;
        }

        try
        {
            if (_config.ModbusTcpHost is not null && _modbusTcp is not null)
            {
                int tcpPort = _config.ModbusTcpPort ?? 502;
                await _modbusTcp.SetCoilAsync(_config.ModbusTcpHost, tcpPort, _config.ModbusUnitId.Value, channel, active).ConfigureAwait(false);
                return;
            }

            if (_config.ModbusSerialPort is not null && _modbus is not null)
            {
                await _modbus.SetCoilAsync(_config.ModbusSerialPort, _config.ModbusUnitId.Value, channel, active).ConfigureAwait(false);
                return;
            }

            _logger.LogWarning(
                "Door {Id}: {Name} Modbus channel {Ch} has no transport configured (no TCP host or serial port) — skipping",
                _config.Id, name, channel);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Door {Id}: {Name} Modbus channel {Ch} write failed ({ExType}: {Msg})",
                _config.Id, name, channel, ex.GetType().Name, ex.Message);
        }
    }
}
