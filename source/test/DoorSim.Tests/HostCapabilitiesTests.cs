using DoorSim.Data;
using DoorSim.Hardware;
using DoorSim.Hosting;
using DoorSim.Mcp;
using DoorSim.Services;
using DoorSim.Shared.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol;

namespace DoorSim.Tests;

/// <summary>
/// Hosts without GPIO (Windows, macOS, most containers) must reject Wiegand doors and GPIO contacts
/// up front instead of silently doing nothing.
/// </summary>
public class HostCapabilitiesTests
{
    private static readonly HostCapabilities NoGpio = new(false);

    private static DoorConfiguration MakeWiegandDoor(int id = 0) => new(
        id, "Wiegand Door", ProtocolType.Wiegand, 4, 17,
        null, null, null, null, null, null, null, null, null, null, null);

    private static DoorConfiguration MakeOsdpDoor() => new(
        0, "OSDP Door", ProtocolType.Osdp, null, null,
        0, "COM3", null, null, null, null, null, null, null, null, null);

    // -------------------------------------------------------------------------
    // DoorConfigService validation
    // -------------------------------------------------------------------------

    [Test]
    public async Task Create_WiegandWithoutGpio_ReturnsError()
    {
        using var fixture = new TestDbFixture();
        var svc = new DoorConfigService(fixture.Db, NoGpio);

        var (result, error) = await svc.CreateAsync(MakeWiegandDoor());

        await Assert.That(result).IsNull();
        await Assert.That(error!).Contains("Wiegand doors cannot be used on this host");
    }

    [Test]
    public async Task Create_WiegandWithGpio_Succeeds()
    {
        using var fixture = new TestDbFixture();
        var svc = new DoorConfigService(fixture.Db, HostCapabilities.Unrestricted);

        var (result, error) = await svc.CreateAsync(MakeWiegandDoor());

        await Assert.That(error).IsNull();
        await Assert.That(result).IsNotNull();
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task Create_OsdpWithGpioContactPinWithoutGpio_ReturnsError(bool dps)
    {
        using var fixture = new TestDbFixture();
        var svc = new DoorConfigService(fixture.Db, NoGpio);
        var door = dps
            ? MakeOsdpDoor() with { DpsPin = 21 }
            : MakeOsdpDoor() with { RexPin = 13 };

        var (result, error) = await svc.CreateAsync(door);

        await Assert.That(result).IsNull();
        await Assert.That(error!).Contains("GPIO DPS/REX pins cannot be used on this host");
    }

    [Test]
    public async Task Create_OsdpWithoutGpio_Succeeds()
    {
        using var fixture = new TestDbFixture();
        var svc = new DoorConfigService(fixture.Db, NoGpio);

        var (result, error) = await svc.CreateAsync(MakeOsdpDoor());

        await Assert.That(error).IsNull();
        await Assert.That(result).IsNotNull();
    }

    // -------------------------------------------------------------------------
    // DynamicSimulatorBank / MCP
    // -------------------------------------------------------------------------

    [Test]
    public async Task Bank_WiegandDoorWithoutGpio_IsSkippedWithReason()
    {
        await using var fixture = new NoGpioBank();
        var bank = fixture.Bank;

        await bank.AddDoor(MakeWiegandDoor(7));

        await Assert.That(bank.ContainsReader(7)).IsFalse();
        await Assert.That(bank.GetUnavailableReason(7)!).Contains("GPIO is not available");
    }

    [Test]
    public async Task Bank_RemoveDoor_ClearsUnavailableReason()
    {
        await using var fixture = new NoGpioBank();
        var bank = fixture.Bank;
        await bank.AddDoor(MakeWiegandDoor(7));

        await bank.RemoveDoor(7);

        await Assert.That(bank.GetUnavailableReason(7)).IsNull();
    }

    [Test]
    public async Task McpOpenDoor_UnavailableWiegandDoor_ThrowsWithGpioReason()
    {
        await using var fixture = new NoGpioBank();
        var bank = fixture.Bank;
        await bank.AddDoor(MakeWiegandDoor(7));

        // The reader check runs before the orchestrator is touched.
        await Assert.That(async () => await SimulationTools.OpenDoor(7, null!, bank))
            .Throws<McpException>()
            .WithMessageContaining("Wiegand door");
    }

    // -------------------------------------------------------------------------
    // DataPaths / AppVersion
    // -------------------------------------------------------------------------

    [Test]
    public async Task DataPaths_ConfiguredValue_WinsOverEnvironmentAndDefaults()
    {
        using var temp = new TempDirectory();
        var configured = Path.Combine(temp.Path, "configured");

        var root = DataPaths.EnsureRoot(configured, Path.Combine(temp.Path, "env"), true,
            Path.Combine(temp.Path, "user"), _ => true);

        await Assert.That(root).IsEqualTo(configured);
        await Assert.That(Directory.Exists(configured)).IsTrue();
    }

    [Test]
    public async Task DataPaths_EnvironmentVariable_WinsOverDefaults()
    {
        using var temp = new TempDirectory();
        var env = Path.Combine(temp.Path, "env");

        var root = DataPaths.EnsureRoot(null, env, true, Path.Combine(temp.Path, "user"), _ => true);

        await Assert.That(root).IsEqualTo(env);
    }

    [Test]
    public async Task DataPaths_Linux_PrefersSystemDirectoryWhenWritable()
    {
        using var temp = new TempDirectory();

        var root = DataPaths.EnsureRoot(null, null, true, Path.Combine(temp.Path, "user"), _ => true);

        await Assert.That(root).IsEqualTo(DataPaths.LinuxSystemDirectory);
    }

    [Test]
    public async Task DataPaths_Linux_FallsBackToUserDirectoryWhenSystemNotWritable()
    {
        using var temp = new TempDirectory();
        var user = Path.Combine(temp.Path, "user");

        var root = DataPaths.EnsureRoot(null, null, true, user, _ => false);

        await Assert.That(root).IsEqualTo(user);
        await Assert.That(Directory.Exists(user)).IsTrue();
    }

    [Test]
    public async Task DataPaths_NonLinux_UsesUserDirectory()
    {
        using var temp = new TempDirectory();
        var user = Path.Combine(temp.Path, "user");
        var probed = false;

        var root = DataPaths.EnsureRoot(null, null, false, user, _ => probed = true);

        await Assert.That(root).IsEqualTo(user);
        await Assert.That(probed).IsFalse();
    }

    [Test]
    [Arguments("1.2.3", "1.2.3")]
    [Arguments("1.2.3+7881449", "1.2.3+7881449")]
    [Arguments("1.2.3+7881449c0ffee1234567890", "1.2.3+7881449")]
    [Arguments("0.0.0-dev+abcdef0123456789", "0.0.0-dev+abcdef0")]
    public async Task AppVersion_Shorten_TrimsCommitSha(string informational, string expected) =>
        await Assert.That(AppVersion.Shorten(informational)).IsEqualTo(expected);

    /// <summary>A GPIO-less bank plus the Modbus services it borrows (the bank does not dispose them).</summary>
    private sealed class NoGpioBank : IAsyncDisposable
    {
        private readonly ModbusRelayService _modbus;
        private readonly ModbusTcpRelayService _modbusTcp;

        public NoGpioBank()
        {
            var config = new ConfigurationBuilder().Build();
            var scopeFactory = new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

            _modbus = new ModbusRelayService(NullLogger<ModbusRelayService>.Instance, config, null);
            _modbusTcp = new ModbusTcpRelayService(NullLogger<ModbusTcpRelayService>.Instance, config);
            Bank = new DynamicSimulatorBank(NullLoggerFactory.Instance, scopeFactory, null, _modbus, _modbusTcp);
        }

        public DynamicSimulatorBank Bank { get; }

        public async ValueTask DisposeAsync()
        {
            await Bank.DisposeAsync();
            _modbus.Dispose();
            _modbusTcp.Dispose();
        }
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"doorsim-test-{Guid.NewGuid():N}");

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, true);
        }
    }

    private sealed class TestDbFixture : IDisposable
    {
        private readonly SqliteConnection _connection;

        public TestDbFixture()
        {
            _connection = new SqliteConnection("Filename=:memory:");
            _connection.Open();
            Db = new DoorSimDbContext(
                new DbContextOptionsBuilder<DoorSimDbContext>()
                    .UseSqlite(_connection)
                    .Options);

            Db.Database.EnsureCreated();
        }

        public DoorSimDbContext Db { get; }

        public void Dispose()
        {
            Db.Dispose();
            _connection.Dispose();
        }
    }
}
