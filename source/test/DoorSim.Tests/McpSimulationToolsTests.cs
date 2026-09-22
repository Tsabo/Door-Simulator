using System.Diagnostics.CodeAnalysis;
using DoorSim.Data;
using DoorSim.Hardware;
using DoorSim.Mcp;
using DoorSim.Services;
using DoorSim.Shared.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol;

namespace DoorSim.Tests;

public class McpSimulationToolsTests
{
    [Test]
    public async Task SimulateCardEvent_InvalidReaderId_ThrowsMcpException()
    {
        using var fixture = new TestDbFixture();
        var (orchestrator, bank) = fixture.MakeOrchestrator();

        var request = new DoorEventRequest(0, DoorEventType.CardReadOnly, 1);

        await Assert.That(async () => await SimulationTools.SimulateCardEvent(
                request, orchestrator, bank, new CardLibraryService(fixture.Db), new CardFormatService(fixture.Db)))
            .Throws<McpException>()
            .WithMessageContaining("ReaderId must be greater than zero");
    }

    [Test]
    public async Task SimulateCardEvent_ReaderNotFound_ThrowsMcpException()
    {
        using var fixture = new TestDbFixture();
        var (orchestrator, bank) = fixture.MakeOrchestrator();

        var request = new DoorEventRequest(99, DoorEventType.CardReadOnly, 1);

        await Assert.That(async () => await SimulationTools.SimulateCardEvent(
                request, orchestrator, bank, new CardLibraryService(fixture.Db), new CardFormatService(fixture.Db)))
            .Throws<McpException>()
            .WithMessageContaining("not found in the active bank");
    }

    [Test]
    public async Task SimulateCardEvent_CardNotInLibrary_ThrowsMcpException()
    {
        using var fixture = new TestDbFixture();
        var (orchestrator, bank) = fixture.MakeOrchestrator();
        bank.Add(1, new FakeReaderSimulator());

        var request = new DoorEventRequest(1, DoorEventType.CardReadOnly, 999);

        await Assert.That(async () => await SimulationTools.SimulateCardEvent(
                request, orchestrator, bank, new CardLibraryService(fixture.Db), new CardFormatService(fixture.Db)))
            .Throws<McpException>()
            .WithMessageContaining("not found in library");
    }

    [Test]
    public async Task SimulateRawBits_InvalidBits_ThrowsMcpException()
    {
        using var fixture = new TestDbFixture();
        var (orchestrator, bank) = fixture.MakeOrchestrator();
        bank.Add(1, new FakeReaderSimulator());

        await Assert.That(async () =>
                await SimulationTools.SimulateRawBits(new RawBitsRequest(1, "not-bits"), orchestrator, bank))
            .Throws<McpException>();
    }

    [Test]
    public async Task SimulateRawBits_ReaderNotFound_ThrowsMcpException()
    {
        using var fixture = new TestDbFixture();
        var (orchestrator, bank) = fixture.MakeOrchestrator();

        await Assert.That(async () =>
                await SimulationTools.SimulateRawBits(new RawBitsRequest(1, "1010"), orchestrator, bank))
            .Throws<McpException>()
            .WithMessageContaining("not found in the active bank");
    }

    [Test]
    public async Task SimulateRawBits_Valid_ReturnsConfirmationMentioningBitCount()
    {
        using var fixture = new TestDbFixture();
        var (orchestrator, bank) = fixture.MakeOrchestrator();
        bank.Add(1, new FakeReaderSimulator());

        var result = await SimulationTools.SimulateRawBits(new RawBitsRequest(1, "10110"), orchestrator, bank);

        await Assert.That(result).Contains("5");
        await Assert.That(result).Contains("reader 1");
    }

    [Test]
    public async Task OpenDoor_ReaderNotFound_ThrowsMcpException()
    {
        using var fixture = new TestDbFixture();
        var (orchestrator, bank) = fixture.MakeOrchestrator();

        await Assert.That(async () => await SimulationTools.OpenDoor(1, orchestrator, bank))
            .Throws<McpException>();
    }

    [Test]
    public async Task GetReaderStatus_ReaderNotFound_ThrowsMcpException()
    {
        using var fixture = new TestDbFixture();
        var (orchestrator, bank) = fixture.MakeOrchestrator();

        await Assert.That(() => SimulationTools.GetReaderStatus(1, orchestrator, bank))
            .Throws<McpException>();
    }

    [Test]
    public async Task GetReaderStatus_NoActivityYet_ReturnsIdle()
    {
        using var fixture = new TestDbFixture();
        var (orchestrator, bank) = fixture.MakeOrchestrator();
        bank.Add(1, new FakeReaderSimulator());

        var status = SimulationTools.GetReaderStatus(1, orchestrator, bank);

        await Assert.That(status).IsEqualTo(SimulationStatus.Idle);
    }

    [Test]
    public async Task GetReaderConnectivity_ReaderNotFound_ThrowsMcpException()
    {
        using var fixture = new TestDbFixture();
        var (orchestrator, bank) = fixture.MakeOrchestrator();

        await Assert.That(() => SimulationTools.GetReaderConnectivity(1, orchestrator, bank))
            .Throws<McpException>();
    }

    [Test]
    public async Task ClearReaderQueue_ReaderNotFound_ThrowsMcpException()
    {
        using var fixture = new TestDbFixture();
        var (orchestrator, bank) = fixture.MakeOrchestrator();

        await Assert.That(() => SimulationTools.ClearReaderQueue(1, orchestrator, bank))
            .Throws<McpException>();
    }

    [Test]
    public async Task CancelQueueItem_ItemNotFound_ThrowsMcpException()
    {
        using var fixture = new TestDbFixture();
        var (orchestrator, bank) = fixture.MakeOrchestrator();
        bank.Add(1, new FakeReaderSimulator());

        await Assert.That(() => SimulationTools.CancelQueueItem(1, Guid.NewGuid(), orchestrator, bank))
            .Throws<McpException>()
            .WithMessageContaining("not found for reader");
    }

    private sealed class FakeReaderSimulator : IReaderSimulator
    {
        public DoorConfiguration Config { get; } = new(1, "Fake", ProtocolType.Wiegand,
            null, null, null, null, null, null, null, null, null, null, null, null, null);

        public bool IsConnected => true;

        public ReaderLedState? LedState => null;

        public bool IsDoorOpen => false;

        public bool IsRexActive => false;

        public Task SendCardAsync(CardEntry card) => Task.CompletedTask;

        public Task SendCardAsync(uint cardNumber, ushort facilityCode, WiegandFormat format) => Task.CompletedTask;

        public Task SendCardAsync(uint cardNumber, ushort facilityCode, CustomCardFormat format) => Task.CompletedTask;

        public Task SendBitsAsync(string bits) => Task.CompletedTask;

        public Task SimulateAccessCycleAsync(CardEntry card, int cardToDoorDelayMs, int doorOpenMs) => Task.CompletedTask;

        public Task SimulateAccessCycleAsync(uint cardNumber, ushort facilityCode, WiegandFormat format, int cardToDoorDelayMs, int doorOpenMs) => Task.CompletedTask;

        public Task SimulateAccessCycleAsync(uint cardNumber, ushort facilityCode, CustomCardFormat format, int cardToDoorDelayMs, int doorOpenMs) => Task.CompletedTask;

        public Task SimulateEgressCycleAsync(int rexLeadMs, int doorOpenMs) => Task.CompletedTask;

        public Task OpenDoorAsync() => Task.CompletedTask;

        public Task CloseDoorAsync() => Task.CompletedTask;

        public Task TripRexAsync() => Task.CompletedTask;

        public Task ResetRexAsync() => Task.CompletedTask;
    }

    private sealed class TestReaderBank : IReaderBank
    {
        private readonly Dictionary<int, IReaderSimulator> _simulators = new();

        public IReaderSimulator GetReader(int doorId) =>
            _simulators.TryGetValue(doorId, out var sim)
                ? sim
                : throw new KeyNotFoundException($"Door {doorId} not found");

        public bool TryGetReader(int doorId, [NotNullWhen(true)] out IReaderSimulator? simulator) =>
            _simulators.TryGetValue(doorId, out simulator);

        public bool ContainsReader(int doorId) =>
            _simulators.ContainsKey(doorId);

        public IReadOnlyCollection<int> ActiveDoorIds => _simulators.Keys;

        public void Add(int doorId, IReaderSimulator sim) => _simulators[doorId] = sim;
    }

    private sealed class TestDbFixture : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ServiceProvider _serviceProvider;

        private SimulationMetricsService? _metrics;

        public TestDbFixture()
        {
            _connection = new SqliteConnection("Filename=:memory:");
            _connection.Open();

            var services = new ServiceCollection();
            services.AddDbContext<DoorSimDbContext>(opt => opt.UseSqlite(_connection));
            _serviceProvider = services.BuildServiceProvider();

            Db = new DoorSimDbContext(
                new DbContextOptionsBuilder<DoorSimDbContext>()
                    .UseSqlite(_connection)
                    .Options);

            Db.Database.EnsureCreated();
        }

        public DoorSimDbContext Db { get; }

        public void Dispose()
        {
            _metrics?.DisposeAsync().AsTask().GetAwaiter().GetResult();
            Db.Dispose();
            _serviceProvider.Dispose();
            _connection.Dispose();
        }

        public (SimulationOrchestrator Orchestrator, TestReaderBank Bank) MakeOrchestrator()
        {
            var scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();
            var bank = new TestReaderBank();
            var settings = new SimulationSettingsService(scopeFactory, NullLogger<SimulationSettingsService>.Instance);
            _metrics = new SimulationMetricsService(scopeFactory, bank, settings, NullLogger<SimulationMetricsService>.Instance);
            var orchestrator = new SimulationOrchestrator(bank, scopeFactory, settings, _metrics, NullLogger<SimulationOrchestrator>.Instance);

            return (orchestrator, bank);
        }
    }
}
