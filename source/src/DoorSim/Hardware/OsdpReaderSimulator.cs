using System.Collections;
using OSDP.Net;
using OSDP.Net.Connections;
using OSDP.Net.Model.ReplyData;

namespace DoorSim.Hardware;

/// <summary>
/// OSDP RS-485 implementation of <see cref="IReaderSimulator" />.
/// Uses <see cref="Device" /> from OSDP.Net to act as a Peripheral Device (PD) on the RS-485 bus.
/// Card reads are delivered via EnqueuePollReply; DPS and REX contacts are managed by
/// <see cref="DoorContactController" /> (GPIO or Modbus relay).
/// </summary>
public sealed class OsdpReaderSimulator : IReaderSimulator, IAsyncDisposable
{
    private readonly int _baudRate;

    // Per-door, resolved from the door's advanced OSDP settings. The rationale for the
    // defaults — and why they differ from upstream OSDP.Net's 8s / 200ms — lives on the
    // corresponding constants in OsdpAdvancedDefaults.
    private readonly TimeSpan _connectionTimeout;

    private readonly DoorContactController _contacts;

    // null when no serial port is configured (PIN-only DPS/REX mode)
    private readonly Device? _device;
    private readonly ILogger<OsdpReaderSimulator> _logger;
    private readonly TimeSpan _replyTimeout;

    private volatile bool _isDoorOpen;
    private volatile bool _isRexActive;
    private SerialPortConnectionListener? _listener;

    /// <summary>
    /// Constructs an OSDP reader simulator.
    /// If <see cref="DoorConfiguration.OsdpSerialPort" /> is set the OSDP.Net Device is
    /// started immediately as a background task; otherwise card sends are no-ops.
    /// </summary>
    public OsdpReaderSimulator(DoorConfiguration config,
        GpioController? gpio,
        ModbusRelayService? modbus,
        ModbusTcpRelayService? modbusTcp,
        ILoggerFactory loggerFactory)
    {
        Config = config;
        _baudRate = OsdpBaudRates.Resolve(config.OsdpBaudRate);
        _connectionTimeout = OsdpAdvancedDefaults.ResolveConnectionTimeout(config.OsdpConnectionTimeoutSeconds);
        _replyTimeout = OsdpAdvancedDefaults.ResolveReplyTimeout(config.OsdpReplyTimeoutMilliseconds);
        _logger = loggerFactory.CreateLogger<OsdpReaderSimulator>();
        _contacts = new DoorContactController(config, gpio, modbus, modbusTcp, _logger);

        if (config.OsdpSerialPort is not null)
        {
            var deviceConfig = new DeviceConfiguration(
                // Built from the same door settings as the osdp_ID reply, so the cUID's
                // vendor code and serial number cannot drift out of step with it.
                OsdpCapabilityMapper.BuildClientIdentification(config))
            {
                Address = config.OsdpAddress ?? 0,
                RequireSecurity = false,
                ConnectionTimeout = _connectionTimeout
            };

            _device = new LoggingDevice(deviceConfig, loggerFactory, config,
                _baudRate,
                () => _isDoorOpen,
                () => _isRexActive);

            _ = StartListeningAsync(config.OsdpSerialPort, loggerFactory);
        }
        else
        {
            _logger.LogWarning(
                "Door {Id} ({Label}): no OSDP serial port configured — card sends will be no-ops.",
                config.Id, config.Label);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _contacts.Dispose();

        if (_device is not null)
        {
            try
            {
                await _device.StopListening();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Door {Id}: error stopping OSDP listener", Config.Id);
            }
        }

        _listener?.Dispose();
    }

    public DoorConfiguration Config { get; }

    // -------------------------------------------------------------------------
    // Connectivity
    // -------------------------------------------------------------------------

    /// <inheritdoc />
    public bool IsConnected => _device?.IsConnected ?? false;

    /// <inheritdoc />
    public ReaderLedState? LedState => (_device as LoggingDevice)?.CurrentLedState;

    /// <inheritdoc />
    public bool IsDoorOpen => _isDoorOpen;

    /// <inheritdoc />
    public bool IsRexActive => _isRexActive;

    // -------------------------------------------------------------------------
    // Card read
    // -------------------------------------------------------------------------

    /// <inheritdoc />
    public Task SendCardAsync(CardEntry card) =>
        SendCardAsync(card.CardNumber, card.FacilityCode, card.Format);

    /// <inheritdoc />
    public Task SendCardAsync(uint cardNumber, ushort facilityCode, WiegandFormat format)
    {
        if (_device is null)
        {
            _logger.LogWarning(
                "Door {Id} ({Label}): no serial port — card send skipped.",
                Config.Id, Config.Label);

            return Task.CompletedTask;
        }

        if (!_device.IsConnected)
        {
            _logger.LogWarning(
                "Door {Id} ({Label}): OSDP not connected — card send skipped.",
                Config.Id, Config.Label);

            return Task.CompletedTask;
        }

        var (frame, bitCount) = BuildFrame(cardNumber, facilityCode, format);
        var cardData = new RawCardData(0, FormatCode.Wiegand, FrameToBitArray(frame, bitCount));
        _device.EnqueuePollReply(cardData);

        _logger.LogInformation(
            "Door {Id} ({Label}): queued osdp_RAW {Format} FC={FC} Card={Card} ({Bits} bits) — {Data}",
            Config.Id, Config.Label, format, facilityCode, cardNumber, bitCount, cardData);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SendCardAsync(uint cardNumber, ushort facilityCode, CustomCardFormat format)
    {
        if (_device is null)
        {
            _logger.LogWarning(
                "Door {Id} ({Label}): no serial port — card send skipped.",
                Config.Id, Config.Label);

            return Task.CompletedTask;
        }

        if (!_device.IsConnected)
        {
            _logger.LogWarning(
                "Door {Id} ({Label}): OSDP not connected — card send skipped.",
                Config.Id, Config.Label);

            return Task.CompletedTask;
        }

        var (frame, bitCount) = BuildFrame(cardNumber, facilityCode, format);
        var cardData = new RawCardData(0, FormatCode.Wiegand, FrameToBitArray(frame, bitCount));
        _device.EnqueuePollReply(cardData);

        _logger.LogInformation(
            "Door {Id} ({Label}): queued osdp_RAW custom format '{FormatName}' FC={FC} Card={Card} ({Bits} bits) — {Data}",
            Config.Id, Config.Label, format.Name, facilityCode, cardNumber, bitCount, cardData);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SendBitsAsync(string bits)
    {
        if (_device is null)
        {
            _logger.LogWarning(
                "Door {Id} ({Label}): no serial port — bits send skipped.",
                Config.Id, Config.Label);

            return Task.CompletedTask;
        }

        if (!_device.IsConnected)
        {
            _logger.LogWarning(
                "Door {Id} ({Label}): OSDP not connected — bits send skipped.",
                Config.Id, Config.Label);

            return Task.CompletedTask;
        }

        var cardData = new RawCardData(0, FormatCode.Wiegand, StringToBitArray(bits));
        _device.EnqueuePollReply(cardData);

        _logger.LogInformation(
            "Door {Id} ({Label}): queued osdp_RAW ({Bits} bits) — {Data}",
            Config.Id, Config.Label, bits.Length, cardData);

        return Task.CompletedTask;
    }

    // -------------------------------------------------------------------------
    // Door lifecycle
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
    // Primitives
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

    private async Task StartListeningAsync(string serialPort, ILoggerFactory loggerFactory)
    {
        try
        {
            _listener = new SerialPortConnectionListener(serialPort, _baudRate, loggerFactory)
            {
                ReplyTimeout = _replyTimeout
            };

            _logger.LogInformation(
                "Door {Id} ({Label}): starting OSDP listener on {Port} at {Baud} baud",
                Config.Id, Config.Label, serialPort, _baudRate);

            await _device!.StartListening(_listener).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Door {Id} ({Label}): OSDP listener failed on {Port}",
                Config.Id, Config.Label, serialPort);
        }
    }

    private static (ulong frame, int bitCount) BuildFrame(uint cardNumber, ushort facilityCode, WiegandFormat format) =>
        format switch
        {
            WiegandFormat.Wiegand26 => (WiegandTransmitter.BuildWiegand26(facilityCode, (ushort)cardNumber), 26),
            WiegandFormat.Wiegand34 => (WiegandTransmitter.BuildWiegand34(facilityCode, cardNumber), 34),
            WiegandFormat.Wiegand37 => (WiegandTransmitter.BuildWiegand37(cardNumber), 37),
            WiegandFormat.HidCorporate1000 => (WiegandTransmitter.BuildHidCorporate1000(facilityCode, cardNumber), 35),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
        };

    private static (ulong frame, int bitCount) BuildFrame(uint cardNumber, ushort facilityCode, CustomCardFormat format)
    {
        var encoder = new CardFormatEncoder(format.CardMask, format.Parity1Mask, format.Parity2Mask, format.Parity3Mask);

        return (encoder.Encode(cardNumber, facilityCode), format.CardMask.Length);
    }

    /// <summary>
    /// Converts a WiegandTransmitter ulong frame (MSB at bit <paramref name="bitCount" />-1)
    /// to a <see cref="BitArray" /> where index 0 is the most-significant bit.
    /// This matches the osdp_RAW wire order expected by RawCardData.BuildData().
    /// </summary>
    private static BitArray FrameToBitArray(ulong frame, int bitCount)
    {
        var bits = new BitArray(bitCount);
        for (var i = 0; i < bitCount; i++)
            bits[i] = (frame >> bitCount - 1 - i & 1) == 1;

        return bits;
    }

    private static BitArray StringToBitArray(string bits)
    {
        var bitArray = new BitArray(bits.Length);
        for (var i = 0; i < bits.Length; i++)
            bitArray[i] = bits[i] == '1';

        return bitArray;
    }
}
