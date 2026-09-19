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

    public bool OsdpNakManufacturerCommand { get; set; }

    // --- Advanced OSDP: advertised capabilities (osdp_CAP) ---
    public byte? OsdpCapContactStatusCompliance { get; set; }

    public byte? OsdpCapContactStatusInputs { get; set; }

    public byte? OsdpCapOutputControlCompliance { get; set; }

    public byte? OsdpCapOutputControlCount { get; set; }

    public byte? OsdpCapAudibleOutputCompliance { get; set; }

    public byte? OsdpCapTextOutputCompliance { get; set; }

    public byte? OsdpCapTextOutputDisplays { get; set; }

    public byte? OsdpCapCardDataFormatCompliance { get; set; }

    public byte? OsdpCapLedControlCompliance { get; set; }

    public byte? OsdpCapLedsPerReader { get; set; }

    public byte? OsdpCapCheckCharacterCompliance { get; set; }

    public bool OsdpCapDeclareAes128 { get; set; }

    public bool OsdpCapDeclareDefaultAesKey { get; set; }

    public int? OsdpCapReceiveBufferSize { get; set; }

    public int? OsdpCapLargestCombinedMessageSize { get; set; }

    public byte? OsdpCapOsdpVersion { get; set; }

    public byte? OsdpCapDownstreamReaders { get; set; }

    // --- Advanced OSDP: device identity (osdp_ID) ---
    public string? OsdpIdVendorCode { get; set; }

    public byte? OsdpIdModelNumber { get; set; }

    public byte? OsdpIdHardwareVersion { get; set; }

    public int? OsdpIdSerialNumber { get; set; }

    public byte? OsdpIdFirmwareMajor { get; set; }

    public byte? OsdpIdFirmwareMinor { get; set; }

    public byte? OsdpIdFirmwareBuild { get; set; }

    // --- Advanced OSDP: protocol behaviour and timing ---
    // Stored as a string to match Protocol above, so the column stays readable to anyone
    // inspecting the database directly. Null must resolve to Ignore: the schema differ adds
    // this column as NULL to every pre-existing row, and Ignore is the original behaviour.
    public string? OsdpComsetHandling { get; set; }

    public int? OsdpConnectionTimeoutSeconds { get; set; }

    public int? OsdpReplyTimeoutMilliseconds { get; set; }

    // Stored as a string for the same readability reason as OsdpComsetHandling above. Null
    // must resolve to the simulator's stock idle color (Off).
    public string? OsdpDefaultLedColor { get; set; }

    // Named arguments throughout: DoorConfiguration is a wide positional record, and two
    // transposed same-typed arguments would compile silently and mis-assign both fields.
    public DoorConfiguration ToDto() => new(
        Id,
        Label,
        Enum.TryParse<ProtocolType>(Protocol, true, out var proto)
            ? proto
            : ProtocolType.Wiegand,
        D0Pin,
        D1Pin,
        OsdpAddress,
        OsdpSerialPort,
        OsdpBaudRate,
        DpsPin,
        RexPin,
        ModbusSerialPort,
        ModbusUnitId,
        DpsModbusChannel,
        RexModbusChannel,
        ModbusTcpHost,
        ModbusTcpPort,
        DpsNormallyOpen,
        RexNormallyOpen,
        OsdpNakManufacturerCommand,
        OsdpCapContactStatusCompliance,
        OsdpCapContactStatusInputs,
        OsdpCapOutputControlCompliance,
        OsdpCapOutputControlCount,
        OsdpCapAudibleOutputCompliance,
        OsdpCapTextOutputCompliance,
        OsdpCapTextOutputDisplays,
        OsdpCapCardDataFormatCompliance,
        OsdpCapLedControlCompliance,
        OsdpCapLedsPerReader,
        OsdpCapCheckCharacterCompliance,
        OsdpCapDeclareAes128,
        OsdpCapDeclareDefaultAesKey,
        OsdpCapReceiveBufferSize,
        OsdpCapLargestCombinedMessageSize,
        OsdpCapOsdpVersion,
        OsdpCapDownstreamReaders,
        OsdpIdVendorCode,
        OsdpIdModelNumber,
        OsdpIdHardwareVersion,
        OsdpIdSerialNumber,
        OsdpIdFirmwareMajor,
        OsdpIdFirmwareMinor,
        OsdpIdFirmwareBuild,
        Enum.TryParse<OsdpComsetBehavior>(OsdpComsetHandling, true, out var comset)
            ? comset
            : OsdpComsetBehavior.Ignore,
        OsdpConnectionTimeoutSeconds,
        OsdpReplyTimeoutMilliseconds,
        Enum.TryParse<OsdpLedColor>(OsdpDefaultLedColor, true, out var ledColor)
            ? ledColor
            : null);

    public static DoorConfigEntity FromDto(DoorConfiguration dto)
    {
        var entity = new DoorConfigEntity { Id = dto.Id };
        entity.ApplyDto(dto);

        return entity;
    }

    /// <summary>
    /// Copies every configurable field from <paramref name="dto" /> onto this entity, leaving
    /// <see cref="Id" /> alone. Shared by <see cref="FromDto" /> and the update path so a new
    /// field cannot be wired into one and silently forgotten in the other.
    /// </summary>
    public void ApplyDto(DoorConfiguration dto)
    {
        Label = dto.Label;
        Protocol = dto.Protocol.ToString();
        D0Pin = dto.D0Pin;
        D1Pin = dto.D1Pin;
        OsdpAddress = dto.OsdpAddress;
        OsdpSerialPort = dto.OsdpSerialPort;
        OsdpBaudRate = dto.OsdpBaudRate;
        DpsPin = dto.DpsPin;
        RexPin = dto.RexPin;
        ModbusSerialPort = dto.ModbusSerialPort;
        ModbusUnitId = dto.ModbusUnitId;
        DpsModbusChannel = dto.DpsModbusChannel;
        RexModbusChannel = dto.RexModbusChannel;
        ModbusTcpHost = dto.ModbusTcpHost;
        ModbusTcpPort = dto.ModbusTcpPort;
        DpsNormallyOpen = dto.DpsNormallyOpen;
        RexNormallyOpen = dto.RexNormallyOpen;
        OsdpNakManufacturerCommand = dto.OsdpNakManufacturerCommand;

        OsdpCapContactStatusCompliance = dto.OsdpCapContactStatusCompliance;
        OsdpCapContactStatusInputs = dto.OsdpCapContactStatusInputs;
        OsdpCapOutputControlCompliance = dto.OsdpCapOutputControlCompliance;
        OsdpCapOutputControlCount = dto.OsdpCapOutputControlCount;
        OsdpCapAudibleOutputCompliance = dto.OsdpCapAudibleOutputCompliance;
        OsdpCapTextOutputCompliance = dto.OsdpCapTextOutputCompliance;
        OsdpCapTextOutputDisplays = dto.OsdpCapTextOutputDisplays;
        OsdpCapCardDataFormatCompliance = dto.OsdpCapCardDataFormatCompliance;
        OsdpCapLedControlCompliance = dto.OsdpCapLedControlCompliance;
        OsdpCapLedsPerReader = dto.OsdpCapLedsPerReader;
        OsdpCapCheckCharacterCompliance = dto.OsdpCapCheckCharacterCompliance;
        OsdpCapDeclareAes128 = dto.OsdpCapDeclareAes128;
        OsdpCapDeclareDefaultAesKey = dto.OsdpCapDeclareDefaultAesKey;
        OsdpCapReceiveBufferSize = dto.OsdpCapReceiveBufferSize;
        OsdpCapLargestCombinedMessageSize = dto.OsdpCapLargestCombinedMessageSize;
        OsdpCapOsdpVersion = dto.OsdpCapOsdpVersion;
        OsdpCapDownstreamReaders = dto.OsdpCapDownstreamReaders;

        OsdpIdVendorCode = dto.OsdpIdVendorCode;
        OsdpIdModelNumber = dto.OsdpIdModelNumber;
        OsdpIdHardwareVersion = dto.OsdpIdHardwareVersion;
        OsdpIdSerialNumber = dto.OsdpIdSerialNumber;
        OsdpIdFirmwareMajor = dto.OsdpIdFirmwareMajor;
        OsdpIdFirmwareMinor = dto.OsdpIdFirmwareMinor;
        OsdpIdFirmwareBuild = dto.OsdpIdFirmwareBuild;

        OsdpComsetHandling = dto.OsdpComsetHandling.ToString();
        OsdpConnectionTimeoutSeconds = dto.OsdpConnectionTimeoutSeconds;
        OsdpReplyTimeoutMilliseconds = dto.OsdpReplyTimeoutMilliseconds;
        OsdpDefaultLedColor = dto.OsdpDefaultLedColor?.ToString();
    }
}
