using DoorSim.Data;
using DoorSim.Mcp;
using DoorSim.Services;
using DoorSim.Shared.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol;

namespace DoorSim.Tests;

public class McpCardFormatToolsTests
{
    private const string ValidCardMask = "XFFFFFFFFCCCCCCCCCCCCCCCCX";
    private const string ValidParity1 = "EPPPPPPPPPPPPXXXXXXXXXXXXX";
    private const string ValidParity2 = "XXXXXXXXXXXXXPPPPPPPPPPPPO";

    private static CustomCardFormat ValidFormat(string name = "Test Format") =>
        new(0, name, ValidCardMask, ValidParity1, ValidParity2, null, DateTimeOffset.UtcNow);

    [Test]
    public async Task ListCardFormats_Empty_ReturnsEmptyArray()
    {
        using var fixture = new TestDbFixture();
        var formats = await CardFormatTools.ListCardFormats(new CardFormatService(fixture.Db));

        await Assert.That(formats).IsEmpty();
    }

    [Test]
    public async Task GetCardFormat_NotFound_ThrowsMcpException()
    {
        using var fixture = new TestDbFixture();

        await Assert.That(async () => await CardFormatTools.GetCardFormat(999, new CardFormatService(fixture.Db)))
            .Throws<McpException>()
            .WithMessageContaining("not found");
    }

    [Test]
    public async Task CreateCardFormat_Invalid_ThrowsMcpException()
    {
        using var fixture = new TestDbFixture();
        var invalid = ValidFormat() with { CardMask = "XXXXXXXX", Parity1Mask = null, Parity2Mask = null };

        await Assert.That(async () => await CardFormatTools.CreateCardFormat(invalid, new CardFormatService(fixture.Db)))
            .Throws<McpException>();
    }

    [Test]
    public async Task CreateCardFormat_Valid_ReturnsCreated()
    {
        using var fixture = new TestDbFixture();

        var created = await CardFormatTools.CreateCardFormat(ValidFormat(), new CardFormatService(fixture.Db));

        await Assert.That(created.Id).IsGreaterThan(0);
    }

    [Test]
    public async Task UpdateCardFormat_NotFound_ThrowsMcpException()
    {
        using var fixture = new TestDbFixture();

        await Assert.That(async () =>
                await CardFormatTools.UpdateCardFormat(999, ValidFormat(), new CardFormatService(fixture.Db)))
            .Throws<McpException>()
            .WithMessageContaining("not found");
    }

    [Test]
    public async Task DeleteCardFormat_NotFound_ThrowsMcpException()
    {
        using var fixture = new TestDbFixture();

        await Assert.That(async () => await CardFormatTools.DeleteCardFormat(999, new CardFormatService(fixture.Db)))
            .Throws<McpException>()
            .WithMessageContaining("not found");
    }

    [Test]
    public async Task DeleteCardFormat_StillReferencedByCard_ThrowsMcpException()
    {
        using var fixture = new TestDbFixture();
        var formatService = new CardFormatService(fixture.Db);
        var cardService = new CardLibraryService(fixture.Db);

        var format = await CardFormatTools.CreateCardFormat(ValidFormat(), formatService);
        await CardTools.CreateCard(
            new CardEntry(0, "Uses Format", 0, 0, WiegandFormat.Custom, DateTimeOffset.UtcNow, format.Id),
            cardService);

        await Assert.That(async () => await CardFormatTools.DeleteCardFormat(format.Id, formatService))
            .Throws<McpException>()
            .WithMessageContaining("still in use");
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
