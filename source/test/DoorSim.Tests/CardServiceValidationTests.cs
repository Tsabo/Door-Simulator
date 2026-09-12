using DoorSim.Data;
using DoorSim.Services;
using DoorSim.Shared.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DoorSim.Tests;

public class CardServiceValidationTests
{
    [Test]
    public async Task Create_ValidCard_Succeeds()
    {
        using var fixture = new TestDbFixture();
        var svc = new CardLibraryService(fixture.Db);

        var card = new CardEntry(0, "Valid Card", 100, 1234, WiegandFormat.Wiegand26, DateTimeOffset.UtcNow);
        var (created, error) = await svc.CreateAsync(card);

        await Assert.That(error).IsNull();
        await Assert.That(created).IsNotNull();
        await Assert.That(created!.Id).IsGreaterThan(0);
        await Assert.That(created.Label).IsEqualTo("Valid Card");
    }

    [Test]
    public async Task Create_InvalidCard_ReturnsError()
    {
        using var fixture = new TestDbFixture();
        var svc = new CardLibraryService(fixture.Db);

        var card = new CardEntry(0, "Invalid Card", 999, 1234, WiegandFormat.Wiegand26, DateTimeOffset.UtcNow);
        var (created, error) = await svc.CreateAsync(card);

        await Assert.That(created).IsNull();
        await Assert.That(error).IsNotNull();
        await Assert.That(error!).Contains("facility code must be between 0 and 255");
    }

    [Test]
    public async Task Update_NonExistentCard_ReturnsNullResultAndNullError()
    {
        using var fixture = new TestDbFixture();
        var svc = new CardLibraryService(fixture.Db);

        var card = new CardEntry(999, "Updated Card", 100, 1234, WiegandFormat.Wiegand26, DateTimeOffset.UtcNow);
        var (updated, error) = await svc.UpdateAsync(999, card);

        await Assert.That(updated).IsNull();
        await Assert.That(error).IsNull();
    }

    [Test]
    public async Task Update_InvalidCardData_ReturnsError()
    {
        using var fixture = new TestDbFixture();
        var svc = new CardLibraryService(fixture.Db);

        var card = new CardEntry(0, "Initial Card", 100, 1234, WiegandFormat.Wiegand26, DateTimeOffset.UtcNow);
        var (created, _) = await svc.CreateAsync(card);

        var (updated, error) = await svc.UpdateAsync(created!.Id, created with { CardNumber = 100_000 });

        await Assert.That(updated).IsNull();
        await Assert.That(error).IsNotNull();
        await Assert.That(error!).Contains("card number must be between 0 and 65,535");
    }

    [Test]
    public async Task Update_ValidCardData_Succeeds()
    {
        using var fixture = new TestDbFixture();
        var svc = new CardLibraryService(fixture.Db);

        var card = new CardEntry(0, "Initial Card", 100, 1234, WiegandFormat.Wiegand26, DateTimeOffset.UtcNow);
        var (created, _) = await svc.CreateAsync(card);

        var (updated, error) = await svc.UpdateAsync(created!.Id, created with { Label = "New Label" });

        await Assert.That(error).IsNull();
        await Assert.That(updated).IsNotNull();
        await Assert.That(updated!.Label).IsEqualTo("New Label");
    }

    [Test]
    public async Task Delete_ExistingCard_ReturnsTrue()
    {
        using var fixture = new TestDbFixture();
        var svc = new CardLibraryService(fixture.Db);

        var card = new CardEntry(0, "To Delete", 100, 1234, WiegandFormat.Wiegand26, DateTimeOffset.UtcNow);
        var (created, _) = await svc.CreateAsync(card);

        var deleted = await svc.DeleteAsync(created!.Id);
        await Assert.That(deleted).IsTrue();
    }

    [Test]
    public async Task Delete_NonExistentCard_ReturnsFalse()
    {
        using var fixture = new TestDbFixture();
        var svc = new CardLibraryService(fixture.Db);

        var deleted = await svc.DeleteAsync(999);
        await Assert.That(deleted).IsFalse();
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
