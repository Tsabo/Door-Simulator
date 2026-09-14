namespace DoorSim.Shared.Models;

/// <summary>
/// Represents a configured door/reader in the simulation bank.
/// D0Pin/D1Pin are used for Wiegand; OsdpAddress is used for OSDP.
/// OsdpBaudRate overrides the bus speed for this door; null means
/// <see cref="OsdpBaudRates.Default" />.
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
/// <remarks>
/// The <c>OsdpCap*</c>, <c>OsdpId*</c>, <c>OsdpComsetHandling</c> and <c>Osdp*Timeout*</c>
/// members are the advanced OSDP settings, and they all share one rule:
/// <b>null means the simulator's stock behaviour.</b> For the capabilities the simulator
/// already advertises — CardDataFormat, ReaderLEDControl, CheckCharacterSupport and
/// CommunicationSecurity — stock means "advertised at the value in
/// <see cref="OsdpAdvancedDefaults" />". For every other capability, stock means "absent from
/// the osdp_CAP reply entirely". A non-null value always means "declare exactly this".
/// <para>
/// These settings change only what the PD <i>declares</i> to the panel; they do not change
/// what the simulator actually does. Declaring a capability the simulator does not implement
/// is a deliberate negative-test tool.
/// </para>
/// <para>
/// Like <c>OsdpNakManufacturerCommand</c>, the two bool flags are named for their non-default
/// case so a column added to pre-existing rows lands on stock behaviour with no backfill.
/// </para>
/// </remarks>
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
    bool OsdpNakManufacturerCommand = false,

    // --- Advanced OSDP: advertised capabilities (osdp_CAP) ---
    byte? OsdpCapContactStatusCompliance = null,
    byte? OsdpCapContactStatusInputs = null,
    byte? OsdpCapOutputControlCompliance = null,
    byte? OsdpCapOutputControlCount = null,
    byte? OsdpCapAudibleOutputCompliance = null,
    byte? OsdpCapTextOutputCompliance = null,
    byte? OsdpCapTextOutputDisplays = null,
    byte? OsdpCapCardDataFormatCompliance = null,
    byte? OsdpCapLedControlCompliance = null,
    byte? OsdpCapLedsPerReader = null,
    byte? OsdpCapCheckCharacterCompliance = null,
    bool OsdpCapDeclareAes128 = false,
    bool OsdpCapDeclareDefaultAesKey = false,
    int? OsdpCapReceiveBufferSize = null,
    int? OsdpCapLargestCombinedMessageSize = null,
    byte? OsdpCapOsdpVersion = null,
    byte? OsdpCapDownstreamReaders = null,

    // --- Advanced OSDP: device identity (osdp_ID) ---
    string? OsdpIdVendorCode = null,
    byte? OsdpIdModelNumber = null,
    byte? OsdpIdHardwareVersion = null,
    int? OsdpIdSerialNumber = null,
    byte? OsdpIdFirmwareMajor = null,
    byte? OsdpIdFirmwareMinor = null,
    byte? OsdpIdFirmwareBuild = null,

    // --- Advanced OSDP: protocol behaviour and timing ---
    OsdpComsetBehavior OsdpComsetHandling = OsdpComsetBehavior.Ignore,
    int? OsdpConnectionTimeoutSeconds = null,
    int? OsdpReplyTimeoutMilliseconds = null);
