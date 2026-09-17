using System.Diagnostics.CodeAnalysis;
using DoorSim.Hardware;
using DoorSim.Services;
using DoorSim.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DoorSim.Tests;

public class SimulatorBankLookupTests
{
    [Test]
    public async Task Bank_ContainsReader_ReturnsExpected()
    {
        var bank = new TestReaderBank();
        bank.Add(1, new FakeReaderSimulator());

        await Assert.That(bank.ContainsReader(1)).IsTrue();
        await Assert.That(bank.ContainsReader(2)).IsFalse();
    }

    [Test]
    public async Task Bank_TryGetReader_NonExistent_ReturnsFalse()
    {
        var bank = new TestReaderBank();
        var found = bank.TryGetReader(99, out var sim);

        await Assert.That(found).IsFalse();
        await Assert.That(sim).IsNull();
    }

    [Test]
    public async Task Bank_TryGetReader_Existing_ReturnsTrue()
    {
        var bank = new TestReaderBank();
        var fake = new FakeReaderSimulator();
        bank.Add(5, fake);

        var found = bank.TryGetReader(5, out var sim);

        await Assert.That(found).IsTrue();
        await Assert.That(sim).IsNotNull();
        await Assert.That(sim).IsSameReferenceAs(fake);
    }

    [Test]
    public async Task Orchestrator_NonExistentReader_GetConnectivity_ReturnsFalse()
    {
        var bank = new TestReaderBank();
        var services = new ServiceCollection().BuildServiceProvider();
        var scopeFactory = services.GetRequiredService<IServiceScopeFactory>();
        var settings = new SimulationSettingsService(scopeFactory, NullLogger<SimulationSettingsService>.Instance);
        await using var metrics = new SimulationMetricsService(scopeFactory, bank, settings, NullLogger<SimulationMetricsService>.Instance);
        await using var orchestrator = new SimulationOrchestrator(bank, scopeFactory, settings, metrics, NullLogger<SimulationOrchestrator>.Instance);

        var isConnected = orchestrator.GetConnectivity(999);
        await Assert.That(isConnected).IsFalse();
    }

    [Test]
    public async Task Orchestrator_NonExistentReader_GetLedState_ReturnsNull()
    {
        var bank = new TestReaderBank();
        var services = new ServiceCollection().BuildServiceProvider();
        var scopeFactory = services.GetRequiredService<IServiceScopeFactory>();
        var settings = new SimulationSettingsService(scopeFactory, NullLogger<SimulationSettingsService>.Instance);
        await using var metrics = new SimulationMetricsService(scopeFactory, bank, settings, NullLogger<SimulationMetricsService>.Instance);
        await using var orchestrator = new SimulationOrchestrator(bank, scopeFactory, settings, metrics, NullLogger<SimulationOrchestrator>.Instance);

        var led = orchestrator.GetLedState(999);
        await Assert.That(led).IsNull();
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
}
