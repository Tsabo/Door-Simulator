using DoorSim.Data;
using DoorSim.Data.Entities;
using DoorSim.Services;
using DoorSim.Shared.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DoorSim.Tests;

/// <summary>
/// Tests for the per-door OSDP baud rate override: <see cref="OsdpBaudRates" />,
/// entity mapping, and <see cref="DoorConfigService" /> validation/persistence.
/// </summary>
public class OsdpBaudRateTests
{
    // -------------------------------------------------------------------------
    // OsdpBaudRates
    // -------------------------------------------------------------------------

    [Test]
    public async Task Resolve_NullFallsBackToDefault() => await Assert.That(OsdpBaudRates.Resolve(null)).IsEqualTo(9600);

    [Test]
    [Arguments(9600)]
    [Arguments(19200)]
    [Arguments(38400)]
    [Arguments(57600)]
    [Arguments(115200)]
    public async Task Resolve_ExplicitRateIsUsed(int baud)
    {
        await Assert.That(OsdpBaudRates.Resolve(baud)).IsEqualTo(baud);
        await Assert.That(OsdpBaudRates.IsSupported(baud)).IsTrue();
    }

    [Test]
    [Arguments(0)]
    [Arguments(1200)]
    [Arguments(4800)]
    [Arguments(9601)]
    [Arguments(230400)]
    [Arguments(-9600)]
    public async Task IsSupported_RejectsRatesOutsideSpec(int baud) => await Assert.That(OsdpBaudRates.IsSupported(baud)).IsFalse();

    // -------------------------------------------------------------------------
    // Entity mapping round-trip
    // -------------------------------------------------------------------------

    [Test]
    [Arguments(19200)]
    [Arguments(115200)]
    public async Task EntityRoundTrip_PreservesBaudRate(int baud)
    {
        var dto = MakeOsdpDoor(baud);
        var roundTripped = DoorConfigEntity.FromDto(dto).ToDto();
        await Assert.That(roundTripped.OsdpBaudRate).IsEqualTo(baud);
    }

    [Test]
    public async Task EntityRoundTrip_PreservesNullBaudRate()
    {
        var dto = MakeOsdpDoor(null);
        var roundTripped = DoorConfigEntity.FromDto(dto).ToDto();
        await Assert.That(roundTripped.OsdpBaudRate).IsNull();
    }

    // -------------------------------------------------------------------------
    // DoorConfigService validation + persistence
    // -------------------------------------------------------------------------

    [Test]
    public async Task Create_RejectsUnsupportedBaudRate()
    {
        using var ctx = NewDb();
        var svc = new DoorConfigService(ctx.Db);

        var (result, error) = await svc.CreateAsync(MakeOsdpDoor(4800));

        await Assert.That(result).IsNull();
        await Assert.That(error).IsNotNull();
        await Assert.That(error!).Contains("4800");
    }

    [Test]
    public async Task Create_AcceptsSupportedBaudRate()
    {
        using var ctx = NewDb();
        var svc = new DoorConfigService(ctx.Db);

        var (result, error) = await svc.CreateAsync(MakeOsdpDoor(19200));

        await Assert.That(error).IsNull();
        await Assert.That(result!.OsdpBaudRate).IsEqualTo(19200);
    }

    [Test]
    public async Task Update_PersistsBaudRateChange()
    {
        using var ctx = NewDb();
        var svc = new DoorConfigService(ctx.Db);

        var (created, _) = await svc.CreateAsync(MakeOsdpDoor(null));
        var (updated, error) = await svc.UpdateAsync(
            created!.Id, created with { OsdpBaudRate = 38400 });

        await Assert.That(error).IsNull();
        await Assert.That(updated!.OsdpBaudRate).IsEqualTo(38400);

        // confirm it actually reached the database, not just the returned DTO
        var reloaded = await svc.GetAsync(created.Id);
        await Assert.That(reloaded!.OsdpBaudRate).IsEqualTo(38400);
    }

    [Test]
    public async Task Wiegand_BaudRateIsNotValidated()
    {
        using var ctx = NewDb();
        var svc = new DoorConfigService(ctx.Db);

        // A nonsense rate on a Wiegand door is irrelevant - the field is unused.
        var dto = MakeOsdpDoor(4800) with
        {
            Protocol = ProtocolType.Wiegand,
            D0Pin = 4,
            D1Pin = 17,
            OsdpSerialPort = null,
        };

        var (result, error) = await svc.CreateAsync(dto);

        await Assert.That(error).IsNull();
        await Assert.That(result).IsNotNull();
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static DoorConfiguration MakeOsdpDoor(int? baudRate) => new(
        0,
        "Test Reader",
        ProtocolType.Osdp,
        null,
        null,
        0,
        "/dev/ttyRS485_1_1",
        baudRate,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null);

    private static DbFixture NewDb() => new();

    /// <summary>In-memory SQLite context; the connection must outlive the DbContext.</summary>
    private sealed class DbFixture : IDisposable
    {
        private readonly SqliteConnection _connection;

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

        public DoorSimDbContext Db { get; }

        public void Dispose()
        {
            Db.Dispose();
            _connection.Dispose();
        }
    }
}
