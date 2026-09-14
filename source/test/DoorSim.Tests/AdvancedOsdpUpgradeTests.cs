using DoorSim.Data;
using DoorSim.Hardware;
using DoorSim.Services;
using DoorSim.Shared.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DoorSim.Tests;

/// <summary>
/// Upgrade safety for the advanced OSDP settings.
/// </summary>
/// <remarks>
/// The schema differ in <see cref="DatabaseInitializer" /> is additive-only and there are no
/// migration files, so the advanced columns land on pre-existing rows as NULL. A door
/// configured before the feature existed must keep loading, keep editing, and keep putting
/// exactly the same bytes on the wire. These tests build a database with the pre-feature
/// Doors table, run the real initializer over it, and check all three.
/// </remarks>
public class AdvancedOsdpUpgradeTests
{
    /// <summary>The Doors table as it stood before the advanced OSDP settings were added.</summary>
    private const string _legacyDoorsTable =
        """
        CREATE TABLE "Doors" (
            "Id"                         INTEGER NOT NULL CONSTRAINT "PK_Doors" PRIMARY KEY AUTOINCREMENT,
            "Label"                      TEXT NOT NULL,
            "Protocol"                   TEXT NOT NULL,
            "D0Pin"                      INTEGER NULL,
            "D1Pin"                      INTEGER NULL,
            "OsdpAddress"                INTEGER NULL,
            "OsdpSerialPort"             TEXT NULL,
            "OsdpBaudRate"               INTEGER NULL,
            "DpsPin"                     INTEGER NULL,
            "RexPin"                     INTEGER NULL,
            "ModbusSerialPort"           TEXT NULL,
            "ModbusUnitId"               INTEGER NULL,
            "DpsModbusChannel"           INTEGER NULL,
            "RexModbusChannel"           INTEGER NULL,
            "ModbusTcpHost"              TEXT NULL,
            "ModbusTcpPort"              INTEGER NULL,
            "DpsNormallyOpen"            INTEGER NOT NULL DEFAULT 0,
            "RexNormallyOpen"            INTEGER NOT NULL DEFAULT 0,
            "OsdpNakManufacturerCommand" INTEGER NOT NULL DEFAULT 0
        );
        """;

    private const string _legacyDoorRow =
        """
        INSERT INTO "Doors" ("Label", "Protocol", "OsdpAddress", "OsdpSerialPort", "DpsPin", "RexPin")
        VALUES ('Legacy Reader', 'Osdp', 3, '/dev/ttyRS485_1_1', 21, 13);
        """;

    [Test]
    public async Task Upgrade_LegacyDoorStillLoadsWithStockAdvancedSettings()
    {
        using var ctx = await NewLegacyDbAsync();
        var svc = new DoorConfigService(ctx.Db);

        var doors = await svc.GetAllAsync();

        await Assert.That(doors.Length).IsEqualTo(1);

        var door = doors[0];
        await Assert.That(door.Label).IsEqualTo("Legacy Reader");
        await Assert.That(door.Protocol).IsEqualTo(ProtocolType.Osdp);
        await Assert.That(door.OsdpAddress).IsEqualTo((byte)3);

        // Every advanced setting must resolve to "simulator default".
        await Assert.That(door.OsdpCapContactStatusCompliance).IsNull();
        await Assert.That(door.OsdpCapCardDataFormatCompliance).IsNull();
        await Assert.That(door.OsdpCapLedControlCompliance).IsNull();
        await Assert.That(door.OsdpCapCheckCharacterCompliance).IsNull();
        await Assert.That(door.OsdpCapDeclareAes128).IsFalse();
        await Assert.That(door.OsdpCapDeclareDefaultAesKey).IsFalse();
        await Assert.That(door.OsdpIdVendorCode).IsNull();
        await Assert.That(door.OsdpIdSerialNumber).IsNull();
        await Assert.That(door.OsdpConnectionTimeoutSeconds).IsNull();
        await Assert.That(door.OsdpReplyTimeoutMilliseconds).IsNull();

        // The NULL enum column is the riskiest one: it must not throw and must not change
        // the pre-feature behaviour of ignoring osdp_COMSET.
        await Assert.That(door.OsdpComsetHandling).IsEqualTo(OsdpComsetBehavior.Ignore);
    }

    [Test]
    public async Task Upgrade_LegacyDoorAdvertisesTheSameCapabilityBytes()
    {
        using var ctx = await NewLegacyDbAsync();
        var svc = new DoorConfigService(ctx.Db);

        var door = (await svc.GetAllAsync())[0];

        await Assert.That(OsdpCapabilityMapper.BuildCapabilities(door).BuildData())
            .IsEquivalentTo(new byte[]
            {
                3, 1, 0, // CardDataFormat
                4, 1, 1, // ReaderLEDControl
                8, 1, 0, // CheckCharacterSupport
                9, 0, 0 // CommunicationSecurity
            });
    }

    [Test]
    public async Task Upgrade_LegacyDoorReportsTheSameIdentification()
    {
        using var ctx = await NewLegacyDbAsync();
        var svc = new DoorConfigService(ctx.Db);

        var door = (await svc.GetAllAsync())[0];

        await Assert.That(OsdpCapabilityMapper.BuildIdentification(door).BuildData())
            .IsEquivalentTo(new byte[]
            {
                0x00, 0x00, 0x01,
                1,
                1,
                (byte)door.Id, 0x00, 0x00, 0x00,
                1, 0, 0
            });
    }

    [Test]
    public async Task Upgrade_LegacyDoorCanStillBeEdited()
    {
        using var ctx = await NewLegacyDbAsync();
        var svc = new DoorConfigService(ctx.Db);

        var door = (await svc.GetAllAsync())[0];
        var (updated, error) = await svc.UpdateAsync(door.Id, door with { Label = "Renamed" });

        await Assert.That(error).IsNull();
        await Assert.That(updated!.Label).IsEqualTo("Renamed");
        await Assert.That(updated.OsdpComsetHandling).IsEqualTo(OsdpComsetBehavior.Ignore);
    }

    [Test]
    public async Task Upgrade_AdvancedSettingsCanBeSetOnALegacyDoor()
    {
        using var ctx = await NewLegacyDbAsync();
        var svc = new DoorConfigService(ctx.Db);

        var door = (await svc.GetAllAsync())[0];
        var (updated, error) = await svc.UpdateAsync(door.Id, door with
        {
            OsdpCapContactStatusCompliance = 2,
            OsdpComsetHandling = OsdpComsetBehavior.Nak,
            OsdpIdVendorCode = "AA-BB-CC"
        });

        await Assert.That(error).IsNull();
        await Assert.That(updated!.OsdpCapContactStatusCompliance).IsEqualTo((byte)2);

        var reloaded = await svc.GetAsync(door.Id);
        await Assert.That(reloaded!.OsdpComsetHandling).IsEqualTo(OsdpComsetBehavior.Nak);
        await Assert.That(reloaded.OsdpIdVendorCode).IsEqualTo("AA-BB-CC");
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static async Task<LegacyDbFixture> NewLegacyDbAsync()
    {
        var fixture = new LegacyDbFixture();
        await fixture.InitializeAsync();

        return fixture;
    }

    /// <summary>
    /// A temp-file SQLite database seeded with the pre-feature Doors table and one row, then
    /// brought forward by the real <see cref="DatabaseInitializer" />.
    /// </summary>
    /// <remarks>
    /// A file rather than <c>:memory:</c> deliberately. Tsabo's schema reader opens its own
    /// connection from the connection string, and every connection to <c>:memory:</c> gets a
    /// private database — so an in-memory fixture reports an empty schema and the differ
    /// decides to create all the tables from scratch, which is not the upgrade path being
    /// tested here. A file is also what production actually uses.
    /// </remarks>
    private sealed class LegacyDbFixture : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly string _path;

        public LegacyDbFixture()
        {
            _path = Path.Combine(Path.GetTempPath(), $"doorsim-upgrade-{Guid.NewGuid():N}.db");
            _connection = new SqliteConnection($"Data Source={_path}");
            _connection.Open();
            Db = new DoorSimDbContext(
                new DbContextOptionsBuilder<DoorSimDbContext>()
                    .UseSqlite(_connection)
                    .Options);
        }

        public DoorSimDbContext Db { get; }

        public void Dispose()
        {
            Db.Dispose();
            _connection.Dispose();
            SqliteConnection.ClearAllPools();

            try
            {
                File.Delete(_path);
            }
            catch (IOException)
            {
                /* best-effort temp cleanup */
            }
        }

        public async Task InitializeAsync()
        {
            await using (var command = _connection.CreateCommand())
            {
                command.CommandText = _legacyDoorsTable;
                await command.ExecuteNonQueryAsync();
            }

            await using (var command = _connection.CreateCommand())
            {
                command.CommandText = _legacyDoorRow;
                await command.ExecuteNonQueryAsync();
            }

            await DatabaseInitializer.InitializeAsync(Db, NullLogger.Instance);
        }
    }
}
