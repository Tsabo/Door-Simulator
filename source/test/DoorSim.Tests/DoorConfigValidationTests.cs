using DoorSim.Data;
using DoorSim.Services;
using DoorSim.Shared.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DoorSim.Tests;

public class DoorConfigValidationTests
{
    private static DoorConfiguration MakeWiegandDoor(int d0 = 4, int d1 = 17, string label = "Wiegand Door") => new(
        0,
        label,
        ProtocolType.Wiegand,
        d0,
        d1,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null);

    private static DoorConfiguration MakeOsdpDoor(string port = "/dev/ttyUSB0", byte address = 0, string label = "OSDP Door") => new(
        0,
        label,
        ProtocolType.Osdp,
        null,
        null,
        address,
        port,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null);

    [Test]
    public async Task Create_EmptyLabel_ReturnsError()
    {
        using var fixture = new TestDbFixture();
        var svc = new DoorConfigService(fixture.Db);

        var (result, error) = await svc.CreateAsync(MakeWiegandDoor(label: ""));

        await Assert.That(result).IsNull();
        await Assert.That(error).IsNotNull();
        await Assert.That(error!).Contains("label cannot be empty");
    }

    [Test]
    public async Task Create_Wiegand_MissingPins_ReturnsError()
    {
        using var fixture = new TestDbFixture();
        var svc = new DoorConfigService(fixture.Db);

        var door = MakeWiegandDoor() with { D0Pin = null };
        var (result, error) = await svc.CreateAsync(door);

        await Assert.That(result).IsNull();
        await Assert.That(error).IsNotNull();
        await Assert.That(error!).Contains("requires both D0Pin and D1Pin");
    }

    [Test]
    public async Task Create_Wiegand_SamePins_ReturnsError()
    {
        using var fixture = new TestDbFixture();
        var svc = new DoorConfigService(fixture.Db);

        var door = MakeWiegandDoor(4, 4);
        var (result, error) = await svc.CreateAsync(door);

        await Assert.That(result).IsNull();
        await Assert.That(error).IsNotNull();
        await Assert.That(error!).Contains("must be different");
    }

    [Test]
    public async Task Create_Wiegand_PinOutOfRange_ReturnsError()
    {
        using var fixture = new TestDbFixture();
        var svc = new DoorConfigService(fixture.Db);

        var door = MakeWiegandDoor(4, 30);
        var (result, error) = await svc.CreateAsync(door);

        await Assert.That(result).IsNull();
        await Assert.That(error).IsNotNull();
        await Assert.That(error!).Contains("valid BCM range");
    }

    [Test]
    public async Task Create_Osdp_MissingSerialPort_ReturnsError()
    {
        using var fixture = new TestDbFixture();
        var svc = new DoorConfigService(fixture.Db);

        var door = MakeOsdpDoor() with { OsdpSerialPort = " " };
        var (result, error) = await svc.CreateAsync(door);

        await Assert.That(result).IsNull();
        await Assert.That(error).IsNotNull();
        await Assert.That(error!).Contains("requires an OSDP serial port");
    }

    [Test]
    public async Task Create_Osdp_AddressOutOfRange_ReturnsError()
    {
        using var fixture = new TestDbFixture();
        var svc = new DoorConfigService(fixture.Db);

        var door = MakeOsdpDoor(address: 127);
        var (result, error) = await svc.CreateAsync(door);

        await Assert.That(result).IsNull();
        await Assert.That(error).IsNotNull();
        await Assert.That(error!).Contains("between 0 and 126");
    }

    [Test]
    public async Task Create_DuplicatePinsAcrossDoors_ReturnsError()
    {
        using var fixture = new TestDbFixture();
        var svc = new DoorConfigService(fixture.Db);

        var (first, error1) = await svc.CreateAsync(MakeWiegandDoor(4, 17, "Door 1"));
        await Assert.That(error1).IsNull();
        await Assert.That(first).IsNotNull();

        var (second, error2) = await svc.CreateAsync(MakeWiegandDoor(4, 27, "Door 2"));
        await Assert.That(second).IsNull();
        await Assert.That(error2).IsNotNull();
        await Assert.That(error2!).Contains("GPIO pin 4 is already assigned");
    }

    [Test]
    public async Task Create_ModbusTcp_InvalidHost_ReturnsError()
    {
        using var fixture = new TestDbFixture();
        var svc = new DoorConfigService(fixture.Db);

        var door = MakeOsdpDoor() with
        {
            DpsModbusChannel = 0,
            ModbusUnitId = 1,
            ModbusTcpHost = "invalid host with spaces!!!",
        };

        var (result, error) = await svc.CreateAsync(door);

        await Assert.That(result).IsNull();
        await Assert.That(error).IsNotNull();
        await Assert.That(error!).Contains("Invalid Modbus TCP host");
    }

    [Test]
    public async Task Create_ModbusTcp_InvalidPort_ReturnsError()
    {
        using var fixture = new TestDbFixture();
        var svc = new DoorConfigService(fixture.Db);

        var door = MakeOsdpDoor() with
        {
            DpsModbusChannel = 0,
            ModbusUnitId = 1,
            ModbusTcpHost = "192.168.1.50",
            ModbusTcpPort = 70000,
        };

        var (result, error) = await svc.CreateAsync(door);

        await Assert.That(result).IsNull();
        await Assert.That(error).IsNotNull();
        await Assert.That(error!).Contains("between 1 and 65,535");
    }

    [Test]
    public async Task Create_Modbus_BothGpioAndModbusDps_ReturnsError()
    {
        using var fixture = new TestDbFixture();
        var svc = new DoorConfigService(fixture.Db);

        var door = MakeWiegandDoor() with
        {
            DpsPin = 22,
            DpsModbusChannel = 0,
            ModbusUnitId = 1,
            ModbusSerialPort = "/dev/ttyUSB1",
        };

        var (result, error) = await svc.CreateAsync(door);

        await Assert.That(result).IsNull();
        await Assert.That(error).IsNotNull();
        await Assert.That(error!).Contains("cannot have both a GPIO DPS pin and a Modbus DPS channel");
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
