namespace DoorSim.Data.Entities;

public class DoorConfigEntity
{
    public int Id { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Protocol { get; set; } = nameof(ProtocolType.Wiegand);
    public int? D0Pin { get; set; }
    public int? D1Pin { get; set; }
    public byte? OsdpAddress { get; set; }
    public string? OsdpSerialPort { get; set; }
    public int? OsdpBaudRate { get; set; }
    public int? DpsPin { get; set; }
    public int? RexPin { get; set; }
    public string? ModbusSerialPort { get; set; }
    public byte? ModbusUnitId { get; set; }
    public int? DpsModbusChannel { get; set; }
    public int? RexModbusChannel { get; set; }
    public string? ModbusTcpHost { get; set; }
    public int? ModbusTcpPort { get; set; }
    public bool DpsNormallyOpen { get; set; }
    public bool RexNormallyOpen { get; set; }
    public bool OsdpAckManufacturerCommand { get; set; }

    public DoorConfiguration ToDto() => new(
        Id, Label,
        Enum.TryParse<ProtocolType>(Protocol, true, out var proto) ? proto : ProtocolType.Wiegand,
        D0Pin, D1Pin, OsdpAddress, OsdpSerialPort, OsdpBaudRate, DpsPin, RexPin,
        ModbusSerialPort, ModbusUnitId, DpsModbusChannel, RexModbusChannel,
        ModbusTcpHost, ModbusTcpPort, DpsNormallyOpen, RexNormallyOpen, OsdpAckManufacturerCommand);

    public static DoorConfigEntity FromDto(DoorConfiguration dto) => new()
    {
        Id               = dto.Id,
        Label            = dto.Label,
        Protocol         = dto.Protocol.ToString(),
        D0Pin            = dto.D0Pin,
        D1Pin            = dto.D1Pin,
        OsdpAddress      = dto.OsdpAddress,
        OsdpSerialPort   = dto.OsdpSerialPort,
        OsdpBaudRate     = dto.OsdpBaudRate,
        DpsPin           = dto.DpsPin,
        RexPin           = dto.RexPin,
        ModbusSerialPort = dto.ModbusSerialPort,
        ModbusUnitId     = dto.ModbusUnitId,
        DpsModbusChannel = dto.DpsModbusChannel,
        RexModbusChannel = dto.RexModbusChannel,
        ModbusTcpHost    = dto.ModbusTcpHost,
        ModbusTcpPort    = dto.ModbusTcpPort,
        DpsNormallyOpen  = dto.DpsNormallyOpen,
        RexNormallyOpen  = dto.RexNormallyOpen,
        OsdpAckManufacturerCommand = dto.OsdpAckManufacturerCommand,
    };
}
