namespace DoorSim.Hardware;

/// <summary>
/// Owns all GPIO lines for a single simulated Wiegand reader.
/// DPS and REX are normally closed relays — idle state is HIGH.
/// D0/D1 pins are required for Wiegand. DPS and REX contacts are managed by
/// <see cref="DoorContactController"/> and may be GPIO or Modbus relay.
/// </summary>
public sealed class DoorSimulator : IReaderSimulator, IDisposable
{

    private readonly DoorConfiguration _config;
    private readonly GpioController? _gpio;
    private readonly ILogger<DoorSimulator> _logger;
    private readonly WiegandTransmitter? _transmitter;
    private readonly DoorContactController _contacts;

    public DoorSimulator(
        DoorConfiguration config,
        GpioController? gpio,
        ModbusRelayService? modbus,
        ModbusTcpRelayService? modbusTcp,
        ILogger<DoorSimulator> logger)
    {
        _config = config;
        _gpio = gpio;
        _logger = logger;
        _contacts = new DoorContactController(config, gpio, modbus, modbusTcp, logger);

        if (config.D0Pin.HasValue && config.D1Pin.HasValue)
            _transmitter = new WiegandTransmitter(gpio, config.D0Pin.Value, config.D1Pin.Value);

        InitializeWiegandPins();
    }

    public void Dispose()
    {
        _contacts.Dispose();

        if (_gpio is null)
            return;

        foreach (var pin in new[] { _config.D0Pin, _config.D1Pin })
        {
            if (pin is null) continue;
            try
            {
                _gpio.Write(pin.Value, PinValue.High); // Return to idle
                _gpio.ClosePin(pin.Value);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Door {Id}: error closing pin {Pin}", _config.Id, pin);
            }
        }
    }

    // -------------------------------------------------------------------------
    // Card read
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    /// <remarks>Wiegand is GPIO-only; there is no handshake, so this is always true.</remarks>
    public bool IsConnected => true;

    /// <inheritdoc/>
    /// <remarks>Wiegand is one-directional — the panel never sends LED or buzzer commands back.</remarks>
    public ReaderLedState? LedState => null;

    // Volatile so the SSE-stream poller thread always sees the latest value.
    private volatile bool _isDoorOpen;
    private volatile bool _isRexActive;

    /// <inheritdoc/>
    public bool IsDoorOpen  => _isDoorOpen;

    /// <inheritdoc/>
    public bool IsRexActive => _isRexActive;

    /// <inheritdoc/>
    public Task SendCardAsync(CardEntry card)
    {
        EnsureWiegand();
        _logger.LogInformation(
            "Door {Id}: send {Format} FC={FC} Card={Card}",
            _config.Id, card.Format, card.FacilityCode, card.CardNumber);

        return Task.Run(() => _transmitter!.Send(card));
    }

    /// <inheritdoc/>
    public Task SendCardAsync(uint cardNumber, ushort facilityCode, WiegandFormat format)
    {
        EnsureWiegand();
        _logger.LogInformation(
            "Door {Id}: raw send {Format} FC={FC} Card={Card}",
            _config.Id, format, facilityCode, cardNumber);

        return Task.Run(() => _transmitter!.Send(cardNumber, facilityCode, format));
    }

    // -------------------------------------------------------------------------
    // Door lifecycle — composed sequences
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    public async Task SimulateAccessCycleAsync(CardEntry card, int cardToDoorDelayMs, int doorOpenMs)
    {
        await SendCardAsync(card).ConfigureAwait(false);
        if (cardToDoorDelayMs > 0)
            await Task.Delay(cardToDoorDelayMs).ConfigureAwait(false);
        await OpenDoorAsync().ConfigureAwait(false);
        await Task.Delay(doorOpenMs).ConfigureAwait(false);
        await CloseDoorAsync().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task SimulateAccessCycleAsync(uint cardNumber, ushort facilityCode, WiegandFormat format, int cardToDoorDelayMs, int doorOpenMs)
    {
        await SendCardAsync(cardNumber, facilityCode, format).ConfigureAwait(false);
        if (cardToDoorDelayMs > 0)
            await Task.Delay(cardToDoorDelayMs).ConfigureAwait(false);
        await OpenDoorAsync().ConfigureAwait(false);
        await Task.Delay(doorOpenMs).ConfigureAwait(false);
        await CloseDoorAsync().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task SimulateEgressCycleAsync(int rexLeadMs, int doorOpenMs)
    {
        await TripRexAsync().ConfigureAwait(false);
        if (rexLeadMs > 0)
            await Task.Delay(rexLeadMs).ConfigureAwait(false);
        await OpenDoorAsync().ConfigureAwait(false);
        await Task.Delay(doorOpenMs).ConfigureAwait(false);
        await CloseDoorAsync().ConfigureAwait(false);
        await ResetRexAsync().ConfigureAwait(false);
    }

    // -------------------------------------------------------------------------
    // Primitives — also callable directly for manual control
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    public async Task OpenDoorAsync()
    {
        _logger.LogDebug("Door {Id}: door open", _config.Id);
        await _contacts.OpenDoorAsync().ConfigureAwait(false);
        _isDoorOpen = true;
    }

    /// <inheritdoc/>
    public async Task CloseDoorAsync()
    {
        _logger.LogDebug("Door {Id}: door close", _config.Id);
        await _contacts.CloseDoorAsync().ConfigureAwait(false);
        _isDoorOpen = false;
    }

    /// <inheritdoc/>
    public async Task TripRexAsync()
    {
        _logger.LogDebug("Door {Id}: REX trip", _config.Id);
        await _contacts.TripRexAsync().ConfigureAwait(false);
        _isRexActive = true;
    }

    /// <inheritdoc/>
    public async Task ResetRexAsync()
    {
        _logger.LogDebug("Door {Id}: REX reset", _config.Id);
        await _contacts.ResetRexAsync().ConfigureAwait(false);
        _isRexActive = false;
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private void InitializeWiegandPins()
    {
        if (_gpio is null)
            return;

        foreach (var pin in new[] { _config.D0Pin, _config.D1Pin })
        {
            if (pin is null) 
                continue;
            _gpio.OpenPin(pin.Value, PinMode.Output);
            _gpio.Write(pin.Value, PinValue.High); // Wiegand lines idle HIGH
        }

        _logger.LogDebug(
            "Door {Id}: Wiegand GPIO pins initialized (D0={D0}, D1={D1})",
            _config.Id, _config.D0Pin, _config.D1Pin);
    }

    private void EnsureWiegand()
    {
        if (_transmitter is null)
            throw new InvalidOperationException(
                $"Door {_config.Id} ({_config.Label}): no D0/D1 pins configured — cannot send Wiegand credential.");
    }
}
