namespace DoorSim.Shared.Models;

/// <summary>
/// Represents a configured door/reader in the simulation bank.
/// D0Pin/D1Pin are used for Wiegand; OsdpAddress is used for OSDP.
/// OsdpBaudRate overrides the bus speed for this door; null means
/// <see cref="OsdpBaudRates.Default"/>.
/// DpsPin/RexPin are GPIO-based contacts. DpsModbusChannel/RexModbusChannel route
/// through the Modbus relay board instead of GPIO.
/// RTU path: ModbusSerialPort + ModbusUnitId.
/// TCP path: ModbusTcpHost (+ optional ModbusTcpPort, default 502) + ModbusUnitId.
/// When ModbusTcpHost is set it takes precedence over ModbusSerialPort for relay control.
/// OsdpNakManufacturerCommand controls the reply to an incoming osdp_MFG command: false
/// (default) ACKs it, since some panels appear to abandon the session after a NAK to an
/// unsupported vendor command; true NAKs it as unsupported instead, matching a real PD with
/// no vendor extension implemented. Named for the non-default (Nak) case so that a column
/// SQLite adds to pre-existing rows — always defaulted to false — lands on the correct
/// default (Ack) automatically, with no backfill needed.
/// </summary>
public record DoorConfiguration(
    int Id,
    string Label,
    ProtocolType Protocol,
    int? D0Pin,
    int? D1Pin,
    byte? OsdpAddress,
    string? OsdpSerialPort,
    int? OsdpBaudRate,
    int? DpsPin,
    int? RexPin,
    string? ModbusSerialPort,
    byte? ModbusUnitId,
    int? DpsModbusChannel,
    int? RexModbusChannel,
    string? ModbusTcpHost,
    int? ModbusTcpPort,
    bool DpsNormallyOpen = false,
    bool RexNormallyOpen = false,
    bool OsdpNakManufacturerCommand = false);
