namespace DoorSim.Hardware;

/// <summary>
/// Owns all GPIO lines for a single simulated Wiegand reader.
/// DPS and REX are normally closed relays — idle state is HIGH.
/// D0/D1 pins are required for Wiegand. DPS and REX contacts are managed by
/// <see cref="DoorContactController" /> and may be GPIO or Modbus relay.
/// </summary>
public sealed class DoorSimulator : IReaderSimulator, IDisposable
{
    private readonly DoorContactController _contacts;
    private readonly GpioController? _gpio;
    private readonly ILogger<DoorSimulator> _logger;
    private readonly WiegandTransmitter? _transmitter;

    // Volatile so the SSE-stream poller thread always sees the latest value.
    private volatile bool _isDoorOpen;
    private volatile bool _isRexActive;

    public DoorSimulator(DoorConfiguration config,
        GpioController? gpio,
        ModbusRelayService? modbus,
        ModbusTcpRelayService? modbusTcp,
        ILogger<DoorSimulator> logger)
    {
        Config = config;
        _gpio = gpio;
        _logger = logger;
        _contacts = new DoorContactController(config, gpio, modbus, modbusTcp, logger);

        if (config is { D0Pin: not null, D1Pin: not null })
            _transmitter = new WiegandTransmitter(gpio, config.D0Pin.Value, config.D1Pin.Value);

        InitializeWiegandPins();
    }

    public void Dispose()
    {
        _contacts.Dispose();

        if (_gpio is null)
            return;

        foreach (var pin in new[] { Config.D0Pin, Config.D1Pin })
        {
            if (pin is null)
                continue;

            try
            {
                _gpio.Write(pin.Value, PinValue.High); // Return to idle
                _gpio.ClosePin(pin.Value);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Door {Id}: error closing pin {Pin}", Config.Id, pin);
            }
        }
    }

    public DoorConfiguration Config { get; }

    // -------------------------------------------------------------------------
    // Card read
    // -------------------------------------------------------------------------

    /// <inheritdoc />
    /// <remarks>Wiegand is GPIO-only; there is no handshake, so this is always true.</remarks>
    public bool IsConnected => true;

    /// <inheritdoc />
    /// <remarks>Wiegand is one-directional — the panel never sends LED or buzzer commands back.</remarks>
    public ReaderLedState? LedState => null;

    /// <inheritdoc />
    public bool IsDoorOpen => _isDoorOpen;

    /// <inheritdoc />
    public bool IsRexActive => _isRexActive;

    /// <inheritdoc />
    public Task SendCardAsync(CardEntry card)
    {
        EnsureWiegand();
        _logger.LogInformation(
            "Door {Id}: send {Format} FC={FC} Card={Card}",
            Config.Id, card.Format, card.FacilityCode, card.CardNumber);

        return Task.Run(() => _transmitter!.Send(card));
    }

    /// <inheritdoc />
    public Task SendCardAsync(uint cardNumber, ushort facilityCode, WiegandFormat format)
    {
        EnsureWiegand();
        _logger.LogInformation(
            "Door {Id}: raw send {Format} FC={FC} Card={Card}",
            Config.Id, format, facilityCode, cardNumber);

        return Task.Run(() => _transmitter!.Send(cardNumber, facilityCode, format));
    }

    /// <inheritdoc />
    public Task SendCardAsync(uint cardNumber, ushort facilityCode, CustomCardFormat format)
    {
        EnsureWiegand();
        _logger.LogInformation(
            "Door {Id}: raw send custom format '{FormatName}' FC={FC} Card={Card}",
            Config.Id, format.Name, facilityCode, cardNumber);

        return Task.Run(() => _transmitter!.Send(cardNumber, facilityCode, format));
    }

    /// <inheritdoc />
    public Task SendBitsAsync(string bits)
    {
        EnsureWiegand();
        _logger.LogInformation(
            "Door {Id}: raw bits send ({BitCount} bits)",
            Config.Id, bits.Length);

        return Task.Run(() => _transmitter!.Send(bits));
    }

    // -------------------------------------------------------------------------
    // Door lifecycle — composed sequences
    // -------------------------------------------------------------------------

    /// <inheritdoc />
    public async Task SimulateAccessCycleAsync(CardEntry card, int cardToDoorDelayMs, int doorOpenMs)
    {
        await SendCardAsync(card).ConfigureAwait(false);
        if (cardToDoorDelayMs > 0)
            await Task.Delay(cardToDoorDelayMs).ConfigureAwait(false);

        await OpenDoorAsync().ConfigureAwait(false);
        await Task.Delay(doorOpenMs).ConfigureAwait(false);
        await CloseDoorAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SimulateAccessCycleAsync(uint cardNumber, ushort facilityCode, WiegandFormat format, int cardToDoorDelayMs, int doorOpenMs)
    {
        await SendCardAsync(cardNumber, facilityCode, format).ConfigureAwait(false);
        if (cardToDoorDelayMs > 0)
            await Task.Delay(cardToDoorDelayMs).ConfigureAwait(false);

        await OpenDoorAsync().ConfigureAwait(false);
        await Task.Delay(doorOpenMs).ConfigureAwait(false);
        await CloseDoorAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SimulateAccessCycleAsync(uint cardNumber, ushort facilityCode, CustomCardFormat format, int cardToDoorDelayMs, int doorOpenMs)
    {
        await SendCardAsync(cardNumber, facilityCode, format).ConfigureAwait(false);
        if (cardToDoorDelayMs > 0)
            await Task.Delay(cardToDoorDelayMs).ConfigureAwait(false);

        await OpenDoorAsync().ConfigureAwait(false);
        await Task.Delay(doorOpenMs).ConfigureAwait(false);
        await CloseDoorAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
    public async Task OpenDoorAsync()
    {
        _logger.LogDebug("Door {Id}: door open", Config.Id);
        await _contacts.OpenDoorAsync().ConfigureAwait(false);
        _isDoorOpen = true;
    }

    /// <inheritdoc />
    public async Task CloseDoorAsync()
    {
        _logger.LogDebug("Door {Id}: door close", Config.Id);
        await _contacts.CloseDoorAsync().ConfigureAwait(false);
        _isDoorOpen = false;
    }

    /// <inheritdoc />
    public async Task TripRexAsync()
    {
        _logger.LogDebug("Door {Id}: REX trip", Config.Id);
        await _contacts.TripRexAsync().ConfigureAwait(false);
        _isRexActive = true;
    }

    /// <inheritdoc />
    public async Task ResetRexAsync()
    {
        _logger.LogDebug("Door {Id}: REX reset", Config.Id);
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

        foreach (var pin in new[] { Config.D0Pin, Config.D1Pin })
        {
            if (pin is null)
                continue;

            _gpio.OpenPin(pin.Value, PinMode.Output);
            _gpio.Write(pin.Value, PinValue.High); // Wiegand lines idle HIGH
        }

        _logger.LogDebug(
            "Door {Id}: Wiegand GPIO pins initialized (D0={D0}, D1={D1})",
            Config.Id, Config.D0Pin, Config.D1Pin);
    }

    private void EnsureWiegand()
    {
        if (_transmitter is null)
        {
            throw new InvalidOperationException(
                $"Door {Config.Id} ({Config.Label}): no D0/D1 pins configured — cannot send Wiegand credential.");
        }
    }
}
