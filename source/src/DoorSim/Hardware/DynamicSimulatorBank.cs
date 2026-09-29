using System.Diagnostics.CodeAnalysis;

namespace DoorSim.Hardware;

/// <summary>
/// Singleton that manages all active <see cref="IReaderSimulator" /> instances.
/// Door configurations are loaded from the database at startup and can be refreshed
/// at runtime as doors are added, updated, or deleted via the API.
/// Owns the shared <see cref="GpioController" /> for the application lifetime.
/// </summary>
public sealed class DynamicSimulatorBank : IReaderBank, IAsyncDisposable
{
    private readonly GpioController? _gpio;
    private readonly ILogger<DynamicSimulatorBank> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ModbusRelayService? _modbus;
    private readonly ModbusTcpRelayService? _modbusTcp;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ConcurrentDictionary<int, IReaderSimulator> _simulators = new();

    // Doors that exist in the database but cannot run on this host (e.g. Wiegand without GPIO,
    // typically a database copied from a Pi), keyed by door ID with the reason reported to callers.
    private readonly ConcurrentDictionary<int, string> _unavailable = new();

    public DynamicSimulatorBank(ILoggerFactory loggerFactory,
        IServiceScopeFactory scopeFactory,
        GpioController? gpio,
        ModbusRelayService modbus,
        ModbusTcpRelayService modbusTcp)
    {
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<DynamicSimulatorBank>();
        _scopeFactory = scopeFactory;
        _modbus = modbus;
        _modbusTcp = modbusTcp;
        _gpio = gpio;
    }

    // -------------------------------------------------------------------------
    // IAsyncDisposable
    // -------------------------------------------------------------------------

    public async ValueTask DisposeAsync()
    {
        foreach (var sim in _simulators.Values)
        {
            if (sim is IAsyncDisposable asyncSim)
                await asyncSim.DisposeAsync();
            else
                (sim as IDisposable)?.Dispose();
        }

        _simulators.Clear();
        // GpioController is owned by the DI container — do not dispose here.
    }

    // -------------------------------------------------------------------------
    // IReaderBank
    // -------------------------------------------------------------------------

    /// <inheritdoc />
    public IReaderSimulator GetReader(int doorId)
    {
        if (!_simulators.TryGetValue(doorId, out var sim))
            throw new KeyNotFoundException($"Door {doorId} not found in the active bank.");

        return sim;
    }

    /// <inheritdoc />
    public bool TryGetReader(int doorId, [NotNullWhen(true)] out IReaderSimulator? simulator) =>
        _simulators.TryGetValue(doorId, out simulator);

    /// <inheritdoc />
    public bool ContainsReader(int doorId) =>
        _simulators.ContainsKey(doorId);

    /// <inheritdoc />
    public IReadOnlyCollection<int> ActiveDoorIds => [.. _simulators.Keys];

    /// <inheritdoc />
    public string? GetUnavailableReason(int doorId) =>
        _unavailable.TryGetValue(doorId, out var reason)
            ? reason
            : null;

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    /// <summary>
    /// Load all door configurations from the database and initialise their simulators.
    /// Called once during application startup after the database has been seeded.
    /// </summary>
    public async Task LoadFromDbAsync()
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var svc = scope.ServiceProvider.GetRequiredService<DoorConfigService>();
        var doors = await svc.GetAllAsync().ConfigureAwait(false);

        foreach (var door in doors)
        {
            try
            {
                await AddDoor(door).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load simulator for door {Id} ({Label})", door.Id, door.Label);
            }
        }

        // Probe each distinct Modbus RTU port so connection errors appear in the log at startup.
        if (_modbus is not null)
        {
            var rtuBoards = doors
                .Where(d => d.ModbusTcpHost is null && d.ModbusSerialPort is not null && d.ModbusUnitId.HasValue)
                .Select(d => (d.ModbusSerialPort!, d.ModbusUnitId!.Value))
                .Distinct();

            foreach (var (port, unitId) in rtuBoards)
                _modbus.Probe(port, unitId);
        }

        // Probe each distinct Modbus TCP host so connection errors appear in the log at startup.
        if (_modbusTcp is not null)
        {
            var tcpBoards = doors
                .Where(d => d.ModbusTcpHost is not null && d.ModbusUnitId.HasValue)
                .Select(d => (d.ModbusTcpHost!, d.ModbusTcpPort ?? 502, d.ModbusUnitId!.Value))
                .Distinct();

            foreach (var (host, port, unitId) in tcpBoards)
                _modbusTcp.Probe(host, port, unitId);
        }

        _logger.LogInformation(
            "DynamicSimulatorBank loaded — {Count} door(s), GPIO {GpioState}",
            _simulators.Count,
            _gpio is not null
                ? "active"
                : "unavailable (dev mode)");
    }

    /// <summary>Add or replace the simulator for a door configuration.</summary>
    public async Task AddDoor(DoorConfiguration door)
    {
        await RemoveDoor(door.Id).ConfigureAwait(false); // Dispose any existing instance first

        if (_gpio is null)
        {
            if (door.Protocol == ProtocolType.Wiegand)
            {
                _unavailable[door.Id] = $"Door {door.Id} ({door.Label}) is a Wiegand door. {HostCapabilities.GpioUnavailableMessage}";
                _logger.LogWarning("Door {Id} ({Label}) not started — Wiegand requires GPIO, which is unavailable on this host",
                    door.Id, door.Label);

                return;
            }

            if (door.DpsPin is not null || door.RexPin is not null)
            {
                _logger.LogWarning(
                    "Door {Id} ({Label}) has GPIO DPS/REX pins but GPIO is unavailable on this host — those contacts are ignored",
                    door.Id, door.Label);
            }
        }

        IReaderSimulator sim = door.Protocol == ProtocolType.Wiegand
            ? new DoorSimulator(door, _gpio, _modbus, _modbusTcp, _loggerFactory.CreateLogger<DoorSimulator>())
            : new OsdpReaderSimulator(door, _gpio, _modbus, _modbusTcp, _loggerFactory);

        _simulators[door.Id] = sim;
    }

    /// <summary>
    /// Remove and dispose the simulator for a door.
    /// Must await <see cref="IAsyncDisposable" /> (OSDP) — a fire-and-forget or IDisposable-only
    /// cast here leaves the old OSDP.Net Device/listener running in the background, permanently
    /// holding its serial port and retrying forever, which starves any new simulator reassigned
    /// to that port.
    /// </summary>
    public async Task RemoveDoor(int doorId)
    {
        _unavailable.TryRemove(doorId, out var _);

        if (!_simulators.TryRemove(doorId, out var sim))
            return;

        if (sim is IAsyncDisposable asyncSim)
            await asyncSim.DisposeAsync().ConfigureAwait(false);
        else
            (sim as IDisposable)?.Dispose();
    }
}
