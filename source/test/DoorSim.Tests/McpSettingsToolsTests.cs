using DoorSim.Data;
using DoorSim.Mcp;
using DoorSim.Services;
using DoorSim.Shared.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol;

namespace DoorSim.Tests;

public class McpSettingsToolsTests
{
    [Test]
    public async Task GetSimulationSettings_ReturnsCurrent()
    {
        using var fixture = new TestDbFixture();
        var service = new SimulationSettingsService(fixture.ScopeFactory, NullLogger<SimulationSettingsService>.Instance);
        await service.LoadAsync();

        var settings = SettingsTools.GetSimulationSettings(service);

        await Assert.That(settings).IsEqualTo(service.Current);
    }

    [Test]
    public async Task UpdateSimulationSettings_Valid_Succeeds()
    {
        using var fixture = new TestDbFixture();
        var service = new SimulationSettingsService(fixture.ScopeFactory, NullLogger<SimulationSettingsService>.Instance);
        await service.LoadAsync();

        var updated = await SettingsTools.UpdateSimulationSettings(
            new SimulationTimingSettings(1000, 4000, 600, 1500), service);

        await Assert.That(updated.CardToDoorDelayMs).IsEqualTo(1000);
    }

    [Test]
    public async Task UpdateSimulationSettings_Invalid_ThrowsMcpException()
    {
        using var fixture = new TestDbFixture();
        var service = new SimulationSettingsService(fixture.ScopeFactory, NullLogger<SimulationSettingsService>.Instance);
        await service.LoadAsync();

        await Assert.That(async () => await SettingsTools.UpdateSimulationSettings(
                new SimulationTimingSettings(-10, 4000, 600, 1500), service))
            .Throws<McpException>()
            .WithMessageContaining("Card-to-door delay must be between");
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
