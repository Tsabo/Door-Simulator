using DoorSim.Data;
using DoorSim.Mcp;
using DoorSim.Services;
using DoorSim.Shared.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol;

namespace DoorSim.Tests;

public class McpCardToolsTests
{
    [Test]
    public async Task ListCards_Empty_ReturnsEmptyArray()
    {
        using var fixture = new TestDbFixture();
        var cards = await CardTools.ListCards(new CardLibraryService(fixture.Db));

        await Assert.That(cards).IsEmpty();
    }

    [Test]
    public async Task GetCard_NotFound_ThrowsMcpException()
    {
        using var fixture = new TestDbFixture();

        await Assert.That(async () => await CardTools.GetCard(999, new CardLibraryService(fixture.Db)))
            .Throws<McpException>()
            .WithMessageContaining("not found");
    }

    [Test]
    public async Task CreateCard_Valid_ReturnsCreatedCard()
    {
        using var fixture = new TestDbFixture();
        var card = new CardEntry(0, "Test Card", 100, 1234, WiegandFormat.Wiegand26, DateTimeOffset.UtcNow);

        var created = await CardTools.CreateCard(card, new CardLibraryService(fixture.Db));

        await Assert.That(created.Id).IsGreaterThan(0);
        await Assert.That(created.Label).IsEqualTo("Test Card");
    }

    [Test]
    public async Task CreateCard_Invalid_ThrowsMcpException()
    {
        using var fixture = new TestDbFixture();
        var card = new CardEntry(0, "Bad Card", 999, 1234, WiegandFormat.Wiegand26, DateTimeOffset.UtcNow);

        await Assert.That(async () => await CardTools.CreateCard(card, new CardLibraryService(fixture.Db)))
            .Throws<McpException>()
            .WithMessageContaining("facility code must be between 0 and 255");
    }

    [Test]
    public async Task UpdateCard_NotFound_ThrowsMcpException()
    {
        using var fixture = new TestDbFixture();
        var card = new CardEntry(999, "Updated", 100, 1234, WiegandFormat.Wiegand26, DateTimeOffset.UtcNow);

        await Assert.That(async () => await CardTools.UpdateCard(999, card, new CardLibraryService(fixture.Db)))
            .Throws<McpException>()
            .WithMessageContaining("not found");
    }

    [Test]
    public async Task DeleteCard_NotFound_ThrowsMcpException()
    {
        using var fixture = new TestDbFixture();

        await Assert.That(async () => await CardTools.DeleteCard(999, new CardLibraryService(fixture.Db)))
            .Throws<McpException>()
            .WithMessageContaining("not found");
    }

    [Test]
    public async Task DeleteCard_Existing_ReturnsConfirmation()
    {
        using var fixture = new TestDbFixture();
        var service = new CardLibraryService(fixture.Db);
        var created = await CardTools.CreateCard(
            new CardEntry(0, "To Delete", 100, 1234, WiegandFormat.Wiegand26, DateTimeOffset.UtcNow), service);

        var result = await CardTools.DeleteCard(created.Id, service);

        await Assert.That(result).Contains(created.Id.ToString());
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
