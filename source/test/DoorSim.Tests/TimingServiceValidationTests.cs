using DoorSim.Data;
using DoorSim.Services;
using DoorSim.Shared.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DoorSim.Tests;

public class TimingServiceValidationTests
{
    [Test]
    public async Task Update_ValidTiming_Succeeds()
    {
        using var fixture = new TestDbFixture();
        var svc = new SimulationSettingsService(fixture.ScopeFactory, NullLogger<SimulationSettingsService>.Instance);
        await svc.LoadAsync();

        var valid = new SimulationTimingSettings(1000, 4000, 600, 1500);
        var (result, error) = await svc.UpdateAsync(valid);

        await Assert.That(error).IsNull();
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.CardToDoorDelayMs).IsEqualTo(1000);
        await Assert.That(svc.Current.CardToDoorDelayMs).IsEqualTo(1000);
    }

    [Test]
    public async Task Update_NegativeTiming_ReturnsError()
    {
        using var fixture = new TestDbFixture();
        var svc = new SimulationSettingsService(fixture.ScopeFactory, NullLogger<SimulationSettingsService>.Instance);
        await svc.LoadAsync();

        var invalid = new SimulationTimingSettings(-10, 4000, 600, 1500);
        var (result, error) = await svc.UpdateAsync(invalid);

        await Assert.That(result).IsNull();
        await Assert.That(error).IsNotNull();
        await Assert.That(error!).Contains("Card-to-door delay must be between");
    }

    private sealed class TestDbFixture : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ServiceProvider _serviceProvider;

        public TestDbFixture()
        {
            _connection = new SqliteConnection("Filename=:memory:");
            _connection.Open();

            var services = new ServiceCollection();
            services.AddDbContext<DoorSimDbContext>(opt => opt.UseSqlite(_connection));

            _serviceProvider = services.BuildServiceProvider();

            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DoorSimDbContext>();
            db.Database.EnsureCreated();
        }

        public IServiceScopeFactory ScopeFactory => _serviceProvider.GetRequiredService<IServiceScopeFactory>();

        public void Dispose()
        {
            _serviceProvider.Dispose();
            _connection.Dispose();
        }
    }
}
