using DoorSim.Data;
using DoorSim.Data.Entities;
using DoorSim.Services;
using DoorSim.Shared.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DoorSim.Tests;

public class CardFormatServiceValidationTests
{
    private const string ValidCardMask = "XFFFFFFFFCCCCCCCCCCCCCCCCX";
    private const string ValidParity1 = "EPPPPPPPPPPPPXXXXXXXXXXXXX";
    private const string ValidParity2 = "XXXXXXXXXXXXXPPPPPPPPPPPPO";

    private static CustomCardFormat ValidFormat(string name = "Test Format") =>
        new(0, name, ValidCardMask, ValidParity1, ValidParity2, null, DateTimeOffset.UtcNow);

    [Test]
    public async Task Create_ValidFormat_Succeeds()
    {
        using var fixture = new TestDbFixture();
        var svc = new CardFormatService(fixture.Db);

        var (created, error) = await svc.CreateAsync(ValidFormat());

        await Assert.That(error).IsNull();
        await Assert.That(created).IsNotNull();
        await Assert.That(created!.Id).IsGreaterThan(0);
    }

    [Test]
    public async Task Create_InvalidFormat_ReturnsError()
    {
        using var fixture = new TestDbFixture();
        var svc = new CardFormatService(fixture.Db);

        var invalid = ValidFormat() with { CardMask = "XXXXXXXX", Parity1Mask = null, Parity2Mask = null };
        var (created, error) = await svc.CreateAsync(invalid);

        await Assert.That(created).IsNull();
        await Assert.That(error).IsNotNull();
    }

    [Test]
    public async Task Update_NonExistentFormat_ReturnsNullResultAndNullError()
    {
        using var fixture = new TestDbFixture();
        var svc = new CardFormatService(fixture.Db);

        var (updated, error) = await svc.UpdateAsync(999, ValidFormat());

        await Assert.That(updated).IsNull();
        await Assert.That(error).IsNull();
    }

    [Test]
    public async Task Update_ValidChange_Succeeds()
    {
        using var fixture = new TestDbFixture();
        var svc = new CardFormatService(fixture.Db);

        var (created, _) = await svc.CreateAsync(ValidFormat());
        var (updated, error) = await svc.UpdateAsync(created!.Id, created with { Name = "Renamed" });

        await Assert.That(error).IsNull();
        await Assert.That(updated).IsNotNull();
        await Assert.That(updated!.Name).IsEqualTo("Renamed");
    }

    [Test]
    public async Task Delete_UnreferencedFormat_Succeeds()
    {
        using var fixture = new TestDbFixture();
        var svc = new CardFormatService(fixture.Db);

        var (created, _) = await svc.CreateAsync(ValidFormat());
        var (deleted, error) = await svc.DeleteAsync(created!.Id);

        await Assert.That(deleted).IsTrue();
        await Assert.That(error).IsNull();
    }

    [Test]
    public async Task Delete_NonExistentFormat_ReturnsFalseAndNullError()
    {
        using var fixture = new TestDbFixture();
        var svc = new CardFormatService(fixture.Db);

        var (deleted, error) = await svc.DeleteAsync(999);

        await Assert.That(deleted).IsFalse();
        await Assert.That(error).IsNull();
    }

    [Test]
    public async Task Delete_FormatReferencedByCard_ReturnsFalseWithError()
    {
        using var fixture = new TestDbFixture();
        var svc = new CardFormatService(fixture.Db);

        var (created, _) = await svc.CreateAsync(ValidFormat());

        fixture.Db.Cards.Add(new CardEntity
        {
            Label = "Custom card",
            FacilityCode = 100,
            CardNumber = 12345,
            Format = WiegandFormat.Custom,
            CustomFormatId = created!.Id,
            CreatedAt = DateTimeOffset.UtcNow
        });

        await fixture.Db.SaveChangesAsync();

        var (deleted, error) = await svc.DeleteAsync(created.Id);

        await Assert.That(deleted).IsFalse();
        await Assert.That(error).IsNotNull();
        await Assert.That(error!).Contains("still in use");
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
