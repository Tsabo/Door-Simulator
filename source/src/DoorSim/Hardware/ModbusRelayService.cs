using System.IO.Ports;
using FluentModbus;

namespace DoorSim.Hardware;

/// <summary>
/// Manages Modbus RTU relay operations through <see cref="ModbusRtuClient" />.
/// </summary>
/// <remarks>
/// This implementation targets USB-RS485 adapters (e.g. /dev/ttyACM* / /dev/ttyUSB*).
/// It uses FluentModbus FC05 for writes and FC01 for readback so the app can report
/// relay active/inactive state.
/// </remarks>
public sealed class ModbusRelayService : IDisposable
{
    private readonly int _baudRate;
    private readonly ConcurrentDictionary<string, (ModbusRtuClient Client, SemaphoreSlim Lock)> _clients = new();
    private readonly ConcurrentDictionary<string, bool> _coilStateCache = new();
    private readonly int? _deRePin;
    private readonly ILogger<ModbusRelayService> _logger;
    private readonly int _maxNoResponseRetries;
    private readonly Parity _parity;
    private readonly StopBits _stopBits;

    public ModbusRelayService(ILogger<ModbusRelayService> logger,
        IConfiguration config,
        GpioController? gpio)
    {
        _logger = logger;
        _deRePin = config.GetValue<int?>("Modbus:DeRePinBcm");
        _baudRate = config.GetValue("Modbus:BaudRate", 9600);
        _parity = ParseParity(config["Modbus:Parity"], Parity.None);
        _stopBits = ParseStopBits(config["Modbus:StopBits"], StopBits.One);
        _maxNoResponseRetries = Math.Max(0, config.GetValue("Modbus:NoResponseRetries", 1));

        if (_deRePin.HasValue)
        {
            _logger.LogInformation(
                "Modbus:DeRePinBcm={Pin} configured; ignored by FluentModbus USB-RS485 mode",
                _deRePin.Value);
        }
        else
        {
            _logger.LogInformation(
                "Modbus:DeRePinBcm not set — using FluentModbus RTU over serial adapter");
        }

        _logger.LogInformation(
            "Modbus serial settings: {Baud} {Parity} {StopBits} (retries={Retries})",
            _baudRate, _parity, _stopBits, _maxNoResponseRetries);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var (client, sem) in _clients.Values)
        {
            try
            {
                client.Close();
            }
            catch
            {
                /* best-effort */
            }

            try
            {
                client.Dispose();
            }
            catch
            {
                /* best-effort */
            }

            sem.Dispose();
        }

        _clients.Clear();
        _coilStateCache.Clear();
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Opens the serial client, writes coil 0 OFF, and reads back coil 0 state.
    /// </summary>
    public void Probe(string serialPort, byte unitId)
    {
        string portPath = NormalizeSerialPort(serialPort);
        _logger.LogInformation("Modbus probe: {Port} unit {Unit} — probing via FluentModbus", portPath, unitId);

        if (!OSDP.Net.Connections.SerialPortUtils.PortExists(portPath))
        {
            _logger.LogWarning(
                "Modbus probe: {Port} unit {Unit} — serial port not found on the system",
                portPath, unitId);
            return;
        }

        (ModbusRtuClient Client, SemaphoreSlim Lock) entry;
        try
        {
            entry = GetOrCreateClient(portPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                "Modbus probe: {Port} unit {Unit} — could not open serial port; " +
                "check adapter is connected ({ExType}: {Msg})",
                portPath, unitId, ex.GetType().Name, ex.Message);

            return;
        }

        entry.Lock.Wait();
        try
        {
            EnsureConnected(portPath, entry.Client);
            entry.Client.WriteSingleCoil(unitId, 0, false);

            bool? state = TryReadSingleCoil(entry.Client, unitId, 0);
            if (state.HasValue)
            {
                _logger.LogInformation(
                    "Modbus probe: {Port} unit {Unit} — coil 0 reported {State}",
                    portPath, unitId, state.Value
                        ? "ON"
                        : "OFF");
            }
            else
            {
                _logger.LogWarning(
                    "Modbus probe: {Port} unit {Unit} — write succeeded but readback failed",
                    portPath, unitId);
            }
        }
        catch (Exception ex)
        {
            // Many relay boards do not send a response to FC05/FC01, causing FluentModbus
            // to throw a timeout.  The write frame was likely transmitted successfully.
            // Log as a warning so startup is not treated as a fatal failure.
            _logger.LogWarning(
                "Modbus probe: {Port} unit {Unit} — no response (board may not echo); " +
                "continuing ({ExType}: {Msg})",
                portPath, unitId, ex.GetType().Name, ex.Message);
        }
        finally
        {
            entry.Lock.Release();
        }
    }

    /// <summary>
    /// Send Modbus FC05 WriteSingleCoil to <paramref name="channel" /> on the board at
    /// <paramref name="serialPort" /> / <paramref name="unitId" />, then read back state via FC01.
    /// </summary>
    /// <param name="serialPort">Serial port path, e.g. <c>/dev/ttyAMA2</c>.</param>
    /// <param name="unitId">Modbus device address (1–247).</param>
    /// <param name="channel">Zero-based relay channel (0–15).</param>
    /// <param name="active"><c>true</c> to energise the coil; <c>false</c> to de-energise.</param>
    public async Task SetCoilAsync(string serialPort, byte unitId, int channel, bool active)
    {
        string portPath = NormalizeSerialPort(serialPort);
        if (!OSDP.Net.Connections.SerialPortUtils.PortExists(portPath))
        {
            _logger.LogWarning(
                "Modbus {Port} unit {Unit}: serial port not found on the system for coil {Ch}",
                portPath, unitId, channel);
            return;
        }

        (ModbusRtuClient Client, SemaphoreSlim Lock) entry;
        try
        {
            entry = GetOrCreateClient(portPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                "Modbus {Port} unit {Unit}: could not open serial port for coil {Ch} ({ExType}: {Msg})",
                portPath, unitId, channel, ex.GetType().Name, ex.Message);
            return;
        }

        await entry.Lock.WaitAsync().ConfigureAwait(false);
        bool portFaulted = false;
        try
        {
            EnsureConnected(portPath, entry.Client);

            for (int attempt = 0; attempt <= _maxNoResponseRetries; attempt++)
            {
                try
                {
                    await entry.Client.WriteSingleCoilAsync(unitId, (ushort)channel, active).ConfigureAwait(false);
                    SetCachedCoilState(portPath, unitId, channel, active);

                    bool? reported = TryReadSingleCoil(entry.Client, unitId, channel);
                    if (reported.HasValue)
                    {
                        SetCachedCoilState(portPath, unitId, channel, reported.Value);
                        if (reported.Value != active)
                        {
                            _logger.LogWarning(
                                "Modbus {Port} unit {Unit}: coil {Ch} requested {State} but board reports {Reported}",
                                portPath, unitId, channel, active
                                    ? "ON"
                                    : "OFF", reported.Value
                                    ? "ON"
                                    : "OFF");
                        }
                        else
                        {
                            _logger.LogDebug(
                                "Modbus {Port} unit {Unit}: coil {Ch} → {State} ✓ (readback confirmed)",
                                portPath, unitId, channel, active
                                    ? "ON"
                                    : "OFF");
                        }
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Modbus {Port} unit {Unit}: coil {Ch} → {State} write succeeded; readback unavailable",
                            portPath, unitId, channel, active
                                ? "ON"
                                : "OFF");
                    }

                    return;
                }
                catch (Exception ex) when (attempt < _maxNoResponseRetries)
                {
                    _logger.LogDebug(ex,
                        "Modbus {Port} unit {Unit}: write failed for coil {Ch} on attempt {Attempt}; reconnecting and retrying",
                        portPath, unitId, channel, attempt + 1);

                    Reconnect(portPath, entry.Client);
                }

                try
                {
                    await entry.Client.WriteSingleCoilAsync(unitId, (ushort)channel, active).ConfigureAwait(false);
                    SetCachedCoilState(portPath, unitId, channel, active);

                    _logger.LogDebug(
                        "Modbus {Port} unit {Unit}: coil {Ch} → {State} ✓",
                        portPath, unitId, channel, active
                            ? "ON"
                            : "OFF");

                    return;
                }
                catch (Exception finalEx)
                {
                    if (attempt >= _maxNoResponseRetries)
                    {
                        throw new IOException(
                            $"No response from Modbus device after {attempt + 1} attempt(s) on {portPath} unit {unitId}.",
                            finalEx);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Modbus {Port} unit {Unit}: failed to set coil {Ch} to {State}. " +
                "Check serial path, unit ID, baud/parity/stop bits, and A/B polarity.",
                portPath, unitId, channel, active
                    ? "ON"
                    : "OFF");

            portFaulted = true;
            RemoveClient(portPath, entry);
        }
        finally
        {
            if (!portFaulted)
                entry.Lock.Release();
        }
    }

    /// <summary>
    /// Reads current relay state for a single coil. Returns null if read fails and no cache exists.
    /// </summary>
    public async Task<bool?> GetCoilStateAsync(string serialPort, byte unitId, int channel)
    {
        string portPath = NormalizeSerialPort(serialPort);
        if (!OSDP.Net.Connections.SerialPortUtils.PortExists(portPath))
        {
            _logger.LogWarning(
                "Modbus {Port} unit {Unit}: serial port not found on the system for coil {Ch} state",
                portPath, unitId, channel);
            return TryGetCachedCoilState(portPath, unitId, channel, out bool cached) ? cached : null;
        }

        (ModbusRtuClient Client, SemaphoreSlim Lock) entry;
        try
        {
            entry = GetOrCreateClient(portPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                "Modbus {Port} unit {Unit}: could not open serial port for coil {Ch} state ({ExType}: {Msg})",
                portPath, unitId, channel, ex.GetType().Name, ex.Message);

            return TryGetCachedCoilState(portPath, unitId, channel, out bool cached)
                ? cached
                : null;
        }

        await entry.Lock.WaitAsync().ConfigureAwait(false);
        try
        {
            EnsureConnected(portPath, entry.Client);
            bool? state = TryReadSingleCoil(entry.Client, unitId, channel);
            if (state.HasValue)
            {
                SetCachedCoilState(portPath, unitId, channel, state.Value);

                return state.Value;
            }

            return TryGetCachedCoilState(portPath, unitId, channel, out bool cached)
                ? cached
                : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Modbus {Port} unit {Unit}: failed to read coil {Ch} state",
                portPath, unitId, channel);

            return TryGetCachedCoilState(portPath, unitId, channel, out bool cached)
                ? cached
                : null;
        }
        finally
        {
            entry.Lock.Release();
        }
    }

    private (ModbusRtuClient Client, SemaphoreSlim Lock) GetOrCreateClient(string path) =>
        _clients.GetOrAdd(path, p =>
        {
            if (!OSDP.Net.Connections.SerialPortUtils.PortExists(p))
            {
                throw new FileNotFoundException($"Serial port '{p}' not found on the system.");
            }

            var client = new ModbusRtuClient();
            ConfigureClient(client);
            try
            {
                client.Connect(p);
            }
            catch
            {
                try { client.Dispose(); }
                catch
                {
                    /* best-effort */
                }

                throw;
            }

            return (client, new SemaphoreSlim(1, 1));
        });

    private void EnsureConnected(string portPath, ModbusRtuClient client)
    {
        if (!client.IsConnected)
        {
            ConfigureClient(client);
            client.Connect(portPath);
        }
    }

    private void Reconnect(string portPath, ModbusRtuClient client)
    {
        try
        {
            client.Close();
        }
        catch
        {
            /* ignore */
        }

        ConfigureClient(client);
        client.Connect(portPath);
    }

    private void ConfigureClient(ModbusRtuClient client)
    {
        client.BaudRate = _baudRate;
        client.Parity = _parity;
        client.StopBits = _stopBits;
        client.Handshake = Handshake.None;
    }

    private void RemoveClient(string path, (ModbusRtuClient Client, SemaphoreSlim Lock) entry)
    {
        if (_clients.TryRemove(path, out _))
        {
            entry.Lock.Release(); // release before dispose so no waiting thread deadlocks
            try
            {
                entry.Client.Close();
            }
            catch
            {
                /* best-effort */
            }

            try
            {
                entry.Client.Dispose();
            }
            catch
            {
                /* best-effort */
            }

            entry.Lock.Dispose();
        }
    }

    private bool? TryReadSingleCoil(ModbusRtuClient client, byte unitId, int channel)
    {
        try
        {
            var data = client.ReadCoils(unitId, (ushort)channel, 1);

            if (data.Length == 0)
                return null;

            // FC01 packs coil bits into bytes; bit0 is first requested coil.
            return (data[0] & 0x01) != 0;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex,
                "Modbus unit {Unit}: readback failed for coil {Ch}",
                unitId, channel);

            return null;
        }
    }

    private void SetCachedCoilState(string portPath, byte unitId, int channel, bool active) =>
        _coilStateCache[BuildCoilCacheKey(portPath, unitId, channel)] = active;

    private bool TryGetCachedCoilState(string portPath, byte unitId, int channel, out bool state) =>
        _coilStateCache.TryGetValue(BuildCoilCacheKey(portPath, unitId, channel), out state);

    private static string BuildCoilCacheKey(string portPath, byte unitId, int channel) =>
        $"{portPath}|{unitId}|{channel}";

    private static string NormalizeSerialPort(string serialPort)
    {
        string value = serialPort.Trim();

        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Serial port path is required.", nameof(serialPort));

        return value;
    }

    private static Parity ParseParity(string? parityText, Parity fallback)
    {
        if (string.IsNullOrWhiteSpace(parityText))
            return fallback;

        return Enum.TryParse<Parity>(parityText, true, out var parsed)
            ? parsed
            : fallback;
    }

    private static StopBits ParseStopBits(string? stopBitsText, StopBits fallback)
    {
        if (string.IsNullOrWhiteSpace(stopBitsText))
            return fallback;

        return Enum.TryParse<StopBits>(stopBitsText, true, out var parsed)
            ? parsed
            : fallback;
    }
}
