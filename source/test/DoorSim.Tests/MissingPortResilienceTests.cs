using DoorSim.Data;
using DoorSim.Hardware;
using DoorSim.Services;
using DoorSim.Shared.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using OSDP.Net.Connections;

namespace DoorSim.Tests;

public class MissingPortResilienceTests
{
    [Test]
    public async Task SerialPortConnectionListener_WithMissingPort_MultipleRetries_DoNotCrash()
    {
        using var listener = new SerialPortConnectionListener("COM4", 9600);

        await listener.Start(_ => Task.CompletedTask);
        await Assert.That(listener.IsRunning).IsTrue();

        // Let it attempt multiple connection retries
        await Task.Delay(150);

        // Force GC collection to check for any finalizer crashes
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        await listener.Stop();
        await Assert.That(listener.IsRunning).IsFalse();
    }

    [Test]
    public async Task OsdpReaderSimulator_WithMissingPort_ConstructsAndDisposesGracefully()
    {
        var door = new DoorConfiguration(
            Id: 42,
            Label: "Missing Port Door",
            Protocol: ProtocolType.Osdp,
            D0Pin: null,
            D1Pin: null,
            OsdpAddress: 0,
            OsdpSerialPort: "NON_EXISTENT_PORT_9999",
            OsdpBaudRate: 9600,
            DpsPin: null,
            RexPin: null,
            ModbusSerialPort: null,
            ModbusUnitId: null,
            DpsModbusChannel: null,
            RexModbusChannel: null,
            ModbusTcpHost: null,
            ModbusTcpPort: null);

        var simulator = new OsdpReaderSimulator(
            door,
            gpio: null,
            modbus: null,
            modbusTcp: null,
            NullLoggerFactory.Instance);

        await Assert.That(simulator.IsConnected).IsFalse();
        await Assert.That(simulator.IsDoorOpen).IsFalse();
        await Assert.That(simulator.IsRexActive).IsFalse();

        await Task.Delay(50);

        await simulator.DisposeAsync();
    }

    [Test]
    public async Task ModbusRelayService_WithMissingPort_SetCoilAsyncDoesNotThrow()
    {
        var config = new ConfigurationBuilder().Build();
        using var modbus = new ModbusRelayService(NullLogger<ModbusRelayService>.Instance, config, gpio: null);

        // Should log warning and complete gracefully without throwing unhandled exceptions
        await modbus.SetCoilAsync("NON_EXISTENT_PORT_9999", 1, 0, active: true);
    }

    [Test]
    public async Task ModbusRelayService_WithMissingPort_GetCoilStateAsyncReturnsNullWithoutThrowing()
    {
        var config = new ConfigurationBuilder().Build();
        using var modbus = new ModbusRelayService(NullLogger<ModbusRelayService>.Instance, config, gpio: null);

        var state = await modbus.GetCoilStateAsync("NON_EXISTENT_PORT_9999", 1, 0);
        await Assert.That(state).IsNull();
    }

    [Test]
    public async Task DoorContactController_WithMissingModbusPort_OpenAndCloseDoNotThrow()
    {
        var door = new DoorConfiguration(
            Id: 10,
            Label: "Modbus Missing Door",
            Protocol: ProtocolType.Wiegand,
            D0Pin: 4,
            D1Pin: 5,
            OsdpAddress: null,
            OsdpSerialPort: null,
            OsdpBaudRate: null,
            DpsPin: null,
            RexPin: null,
            ModbusSerialPort: "NON_EXISTENT_PORT_9999",
            ModbusUnitId: 1,
            DpsModbusChannel: 0,
            RexModbusChannel: 1,
            ModbusTcpHost: null,
            ModbusTcpPort: null);

        var config = new ConfigurationBuilder().Build();
        using var modbus = new ModbusRelayService(NullLogger<ModbusRelayService>.Instance, config, gpio: null);
        using var controller = new DoorContactController(door, gpio: null, modbus, modbusTcp: null, NullLogger.Instance);

        await controller.OpenDoorAsync();
        await controller.CloseDoorAsync();
        await controller.TripRexAsync();
        await controller.ResetRexAsync();
    }

    [Test]
    public async Task SerialPortUtils_PortExists_ReturnsFalseForMissingPorts()
    {
        await Assert.That(SerialPortUtils.PortExists(null!)).IsFalse();
        await Assert.That(SerialPortUtils.PortExists("")).IsFalse();
        await Assert.That(SerialPortUtils.PortExists("   ")).IsFalse();
        await Assert.That(SerialPortUtils.PortExists("NON_EXISTENT_PORT_12345")).IsFalse();
    }

    [Test]
    public async Task DoorConfigService_GetAvailableSerialPortsAsync_DoesNotThrow()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<DoorSimDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new DoorSimDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var svc = new DoorConfigService(db);
        var ports = await svc.GetAvailableSerialPortsAsync();

        await Assert.That(ports).IsNotNull();
    }
}
