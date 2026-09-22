using DoorSim.Data;
using DoorSim.Mcp;
using DoorSim.Services;
using DoorSim.Shared.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol;

namespace DoorSim.Tests;

/// <summary>
/// Covers the DB-only paths of <see cref="DoorTools" /> (validation errors and not-found, which
/// return before touching <c>DynamicSimulatorBank</c>). The success paths of create_door/update_door
/// — which go on to add a live simulator to the hardware bank — aren't exercised here, matching how
/// the rest of this suite tests <c>DoorConfigService</c> directly rather than through
/// <c>DynamicSimulatorBank</c>; see the plan's manual end-to-end verification step for that coverage.
/// </summary>
public class McpDoorToolsTests
{
    private static DoorConfiguration MakeWiegandDoor(int d0 = 4, int d1 = 17, string label = "Wiegand Door") => new(
        0, label, ProtocolType.Wiegand, d0, d1, null, null, null, null, null, null, null, null, null, null, null);

    [Test]
    public async Task ListDoors_Empty_ReturnsEmptyArray()
    {
        using var fixture = new TestDbFixture();
        var doors = await DoorTools.ListDoors(new DoorConfigService(fixture.Db));

        await Assert.That(doors).IsEmpty();
    }

    [Test]
    public async Task GetDoor_NotFound_ThrowsMcpException()
    {
        using var fixture = new TestDbFixture();

        await Assert.That(async () => await DoorTools.GetDoor(999, new DoorConfigService(fixture.Db)))
            .Throws<McpException>()
            .WithMessageContaining("not found");
    }

    [Test]
    public async Task CreateDoor_InvalidLabel_ThrowsMcpException()
    {
        using var fixture = new TestDbFixture();
        var service = new DoorConfigService(fixture.Db);

        await Assert.That(async () => await DoorTools.CreateDoor(MakeWiegandDoor(label: ""), service, null!))
            .Throws<McpException>()
            .WithMessageContaining("label cannot be empty");
    }

    [Test]
    public async Task UpdateDoor_NotFound_ThrowsMcpException()
    {
        using var fixture = new TestDbFixture();
        var service = new DoorConfigService(fixture.Db);

        await Assert.That(async () => await DoorTools.UpdateDoor(999, MakeWiegandDoor(), service, null!))
            .Throws<McpException>()
            .WithMessageContaining("not found");
    }

    [Test]
    public async Task DeleteDoor_NotFound_ThrowsMcpException()
    {
        using var fixture = new TestDbFixture();
        var service = new DoorConfigService(fixture.Db);

        await Assert.That(async () => await DoorTools.DeleteDoor(999, service, null!, null!))
            .Throws<McpException>()
            .WithMessageContaining("not found");
    }

    [Test]
    public async Task ListSerialPorts_ReturnsArray()
    {
        using var fixture = new TestDbFixture();

        var ports = await DoorTools.ListSerialPorts(null, new DoorConfigService(fixture.Db));

        await Assert.That(ports).IsNotNull();
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
