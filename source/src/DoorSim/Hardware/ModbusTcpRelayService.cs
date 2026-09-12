using System.Collections.Concurrent;
using System.Net;
using FluentModbus;

namespace DoorSim.Hardware;

/// <summary>
/// Manages Modbus TCP relay operations through <see cref="ModbusTcpClient"/>.
/// </summary>
/// <remarks>
/// Targets Waveshare 16-Ch Ethernet relay modules (and compatible Modbus TCP devices).
/// Uses FC05 for writes and FC01 for readback. One <see cref="ModbusTcpClient"/> is
/// maintained per host:port pair and protected by a per-connection semaphore.
/// </remarks>
public sealed class ModbusTcpRelayService : IDisposable
{
    private readonly ILogger<ModbusTcpRelayService> _logger;
    private readonly int _port;
    private readonly ConcurrentDictionary<string, (ModbusTcpClient Client, SemaphoreSlim Lock)> _clients = new();
    private readonly ConcurrentDictionary<string, bool> _coilStateCache = new();

    public ModbusTcpRelayService(ILogger<ModbusTcpRelayService> logger, IConfiguration config)
    {
        _logger = logger;
        _port   = config.GetValue("Modbus:TcpPort", 502);
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Opens the TCP connection, writes coil 0 OFF, and reads back coil 0 state.
    /// Intended to be called at startup so connection errors surface in the log early.
    /// </summary>
    public void Probe(string host, int port, byte unitId)
    {
        _logger.LogInformation("Modbus TCP probe: {Host}:{Port} unit {Unit}", host, port, unitId);

        (ModbusTcpClient Client, SemaphoreSlim Lock) entry;
        try
        {
            entry = GetOrCreateClient(host, port);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                "Modbus TCP probe: {Host}:{Port} unit {Unit} — could not connect ({ExType}: {Msg})",
                host, port, unitId, ex.GetType().Name, ex.Message);
            return;
        }

        entry.Lock.Wait();
        try
        {
            EnsureConnected(host, port, entry.Client);
            entry.Client.WriteSingleCoil(unitId, 0, false);

            bool? state = TryReadSingleCoil(entry.Client, unitId, channel: 0);
            if (state.HasValue)
                _logger.LogInformation(
                    "Modbus TCP probe: {Host}:{Port} unit {Unit} — coil 0 reported {State}",
                    host, port, unitId, state.Value ? "ON" : "OFF");
            else
                _logger.LogWarning(
                    "Modbus TCP probe: {Host}:{Port} unit {Unit} — write succeeded but readback failed",
                    host, port, unitId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                "Modbus TCP probe: {Host}:{Port} unit {Unit} — no response ({ExType}: {Msg})",
                host, port, unitId, ex.GetType().Name, ex.Message);
        }
        finally
        {
            entry.Lock.Release();
        }
    }

    /// <summary>
    /// Send Modbus FC05 WriteSingleCoil to <paramref name="channel"/> on the device at
    /// <paramref name="host"/>:<paramref name="port"/> / <paramref name="unitId"/>,
    /// then read back state via FC01.
    /// </summary>
    /// <param name="host">Hostname or IP address of the Modbus TCP device.</param>
    /// <param name="port">TCP port (default 502).</param>
    /// <param name="unitId">Modbus unit ID (1–247).</param>
    /// <param name="channel">Zero-based relay channel (0–15).</param>
    /// <param name="active"><c>true</c> to energise the coil; <c>false</c> to de-energise.</param>
    public async Task SetCoilAsync(string host, int port, byte unitId, int channel, bool active)
    {
        (ModbusTcpClient Client, SemaphoreSlim Lock) entry;
        try
        {
            entry = GetOrCreateClient(host, port);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                "Modbus TCP {Host}:{Port} unit {Unit}: could not connect for coil {Ch} ({ExType}: {Msg})",
                host, port, unitId, channel, ex.GetType().Name, ex.Message);
            return;
        }

        await entry.Lock.WaitAsync().ConfigureAwait(false);
        bool clientFaulted = false;
        try
        {
            EnsureConnected(host, port, entry.Client);
            entry.Client.WriteSingleCoil(unitId, (ushort)channel, active);
            SetCachedCoilState(host, port, unitId, channel, active);

            bool? reported = TryReadSingleCoil(entry.Client, unitId, channel);
            if (reported.HasValue)
            {
                SetCachedCoilState(host, port, unitId, channel, reported.Value);
                if (reported.Value != active)
                    _logger.LogWarning(
                        "Modbus TCP {Host}:{Port} unit {Unit}: coil {Ch} requested {State} but board reports {Reported}",
                        host, port, unitId, channel, active ? "ON" : "OFF", reported.Value ? "ON" : "OFF");
                else
                    _logger.LogDebug(
                        "Modbus TCP {Host}:{Port} unit {Unit}: coil {Ch} → {State} ✓ (readback confirmed)",
                        host, port, unitId, channel, active ? "ON" : "OFF");
            }
            else
            {
                _logger.LogWarning(
                    "Modbus TCP {Host}:{Port} unit {Unit}: coil {Ch} → {State} write succeeded; readback unavailable",
                    host, port, unitId, channel, active ? "ON" : "OFF");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Modbus TCP {Host}:{Port} unit {Unit}: failed to set coil {Ch} to {State}. " +
                "Check host/port, unit ID, and network connectivity.",
                host, port, unitId, channel, active ? "ON" : "OFF");
            clientFaulted = true;
            RemoveClient(BuildKey(host, port), entry);
        }
        finally
        {
            if (!clientFaulted)
                entry.Lock.Release();
        }
    }

    /// <summary>
    /// Reads current relay state for a single coil. Returns null if read fails and no cache exists.
    /// </summary>
    public async Task<bool?> GetCoilStateAsync(string host, int port, byte unitId, int channel)
    {
        (ModbusTcpClient Client, SemaphoreSlim Lock) entry;
        try
        {
            entry = GetOrCreateClient(host, port);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                "Modbus TCP {Host}:{Port} unit {Unit}: could not connect for coil {Ch} state ({ExType}: {Msg})",
                host, port, unitId, channel, ex.GetType().Name, ex.Message);
            return TryGetCachedCoilState(host, port, unitId, channel, out bool cached) ? cached : null;
        }

        await entry.Lock.WaitAsync().ConfigureAwait(false);
        try
        {
            EnsureConnected(host, port, entry.Client);
            bool? state = TryReadSingleCoil(entry.Client, unitId, channel);
            if (state.HasValue)
            {
                SetCachedCoilState(host, port, unitId, channel, state.Value);
                return state.Value;
            }

            return TryGetCachedCoilState(host, port, unitId, channel, out bool cached) ? cached : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Modbus TCP {Host}:{Port} unit {Unit}: failed to read coil {Ch} state",
                host, port, unitId, channel);

            return TryGetCachedCoilState(host, port, unitId, channel, out bool cached) ? cached : null;
        }
        finally
        {
            entry.Lock.Release();
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        foreach (var (client, sem) in _clients.Values)
        {
            try 
            { 
                client.Disconnect(); 
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
    // Helpers
    // -------------------------------------------------------------------------

    private (ModbusTcpClient Client, SemaphoreSlim Lock) GetOrCreateClient(string host, int port)
    {
        string key = BuildKey(host, port);
        return _clients.GetOrAdd(key, _ =>
        {
            var client = new ModbusTcpClient();
            try
            {
                client.Connect(ResolveEndpoint(host, port));
            }
            catch
            {
                try 
                { 
                    client.Dispose();
                } 
                catch 
                { 
                    /* best-effort */ 
                }
                throw;
            }
            return (client, new SemaphoreSlim(1, 1));
        });
    }

    private void EnsureConnected(string host, int port, ModbusTcpClient client)
    {
        if (!client.IsConnected)
            client.Connect(ResolveEndpoint(host, port));
    }

    private static IPEndPoint ResolveEndpoint(string host, int port)
    {
        if (IPAddress.TryParse(host, out var addr))
            return new IPEndPoint(addr, port);

        var addresses = Dns.GetHostAddresses(host);
        if (addresses.Length == 0)
            throw new InvalidOperationException($"Could not resolve host '{host}'.");

        return new IPEndPoint(addresses[0], port);
    }

    private void RemoveClient(string key, (ModbusTcpClient Client, SemaphoreSlim Lock) entry)
    {
        if (_clients.TryRemove(key, out _))
        {
            entry.Lock.Release();
            try 
            { 
                entry.Client.Disconnect(); 
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

    private bool? TryReadSingleCoil(ModbusTcpClient client, byte unitId, int channel)
    {
        try
        {
            var data = client.ReadCoils(unitId, (ushort)channel, 1);
            if (data.Length == 0)
                return null;

            return (data[0] & 0x01) != 0;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Modbus TCP unit {Unit}: readback failed for coil {Ch}", unitId, channel);
            return null;
        }
    }

    private void SetCachedCoilState(string host, int port, byte unitId, int channel, bool active) =>
        _coilStateCache[BuildCoilCacheKey(host, port, unitId, channel)] = active;

    private bool TryGetCachedCoilState(string host, int port, byte unitId, int channel, out bool state) =>
        _coilStateCache.TryGetValue(BuildCoilCacheKey(host, port, unitId, channel), out state);

    private static string BuildKey(string host, int port) => $"{host}:{port}";

    private static string BuildCoilCacheKey(string host, int port, byte unitId, int channel) =>
        $"{host}:{port}|{unitId}|{channel}";
}
