using DoorSim.Data;
using DoorSim.Services;
using DoorSim.Shared.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DoorSim.Tests;

/// <summary>
/// Tests that <see cref="DoorConfigService"/> strips control characters (CR/LF in particular)
/// from free-text door fields before persisting, so they can't be used to forge fake entries in
/// downstream logs (CWE-117 / CodeQL cs/log-forging).
/// </summary>
public class DoorConfigServiceLogSanitizationTests
{
    [Test]
    public async Task Create_StripsNewlinesFromLabel()
    {
        using var ctx = NewDb();
        var svc = new DoorConfigService(ctx.Db);

        var (result, error) = await svc.CreateAsync(
            MakeDoor(label: "Front Door\nFAKE LOG: admin override granted"));

        await Assert.That(error).IsNull();
        await Assert.That(result!.Label).DoesNotContain("\n");
        await Assert.That(result.Label).DoesNotContain("\r");
        await Assert.That(result.Label).IsEqualTo("Front DoorFAKE LOG: admin override granted");
    }

    [Test]
    public async Task Create_StripsNewlinesFromOsdpSerialPort()
    {
        using var ctx = NewDb();
        var svc = new DoorConfigService(ctx.Db);

        var (result, error) = await svc.CreateAsync(
            MakeDoor(osdpSerialPort: "/dev/ttyRS485_1_1\r\nFAKE LOG entry"));

        await Assert.That(error).IsNull();
        await Assert.That(result!.OsdpSerialPort).DoesNotContain("\n");
        await Assert.That(result.OsdpSerialPort).DoesNotContain("\r");
    }

    [Test]
    public async Task Update_StripsNewlinesFromLabel()
    {
        using var ctx = NewDb();
        var svc = new DoorConfigService(ctx.Db);

        var (created, _) = await svc.CreateAsync(MakeDoor(label: "Front Door"));
        var (updated, error) = await svc.UpdateAsync(
            created!.Id, created with { Label = "Front Door\nFAKE LOG: admin override granted" });

        await Assert.That(error).IsNull();
        await Assert.That(updated!.Label).DoesNotContain("\n");

        // confirm it actually reached the database, not just the returned DTO
        var reloaded = await svc.GetAsync(created.Id);
        await Assert.That(reloaded!.Label).DoesNotContain("\n");
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static DoorConfiguration MakeDoor(
        string label = "Test Reader",
        string? osdpSerialPort = "/dev/ttyRS485_1_1") => new(
        Id: 0,
        Label: label,
        Protocol: ProtocolType.Osdp,
        D0Pin: null,
        D1Pin: null,
        OsdpAddress: 0,
        OsdpSerialPort: osdpSerialPort,
        OsdpBaudRate: null,
        DpsPin: null,
        RexPin: null,
        ModbusSerialPort: null,
        ModbusUnitId: null,
        DpsModbusChannel: null,
        RexModbusChannel: null,
        ModbusTcpHost: null,
        ModbusTcpPort: null);

    /// <summary>In-memory SQLite context; the connection must outlive the DbContext.</summary>
    private sealed class DbFixture : IDisposable
    {
        private readonly SqliteConnection _connection;
        public DoorSimDbContext Db { get; }

        public DbFixture()
        {
            _connection = new SqliteConnection("Filename=:memory:");
            _connection.Open();
            Db = new DoorSimDbContext(
                new DbContextOptionsBuilder<DoorSimDbContext>()
                    .UseSqlite(_connection)
                    .Options);
            Db.Database.EnsureCreated();
        }

        public void Dispose()
        {
            Db.Dispose();
            _connection.Dispose();
        }
    }

    private static DbFixture NewDb() => new();
}
