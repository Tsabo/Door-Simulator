using MudBlazor;

namespace DoorSim.Client.Components.Shared;

public partial class DoorEditDialog
{
    // MudTabs.ActivePanelIndex is positional, so the conditionally-disabled panels need their
    // indices named — inserting a panel shifts every index after it.
    private const int GeneralTabIndex = 0;
    private const int AdvancedOsdpTabIndex = 1;
    private const int RelayBoardTabIndex = 4;

    private int _activeTabIndex;
    private string[] _availableSerialPorts = [];
    private string? _error;
    private DoorFormModel _form = new();
    private bool _saving;

    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Parameter]
    public DoorConfiguration? Editing { get; set; }

    private bool AnyModbus => _form is { HasDps: true, DpsModeIsModbus: true } or { HasRex: true, RexModeIsModbus: true };

    protected override async Task OnInitializedAsync()
    {
        if (Editing is { } door)
        {
            _form = new DoorFormModel
            {
                Label = door.Label,
                Protocol = door.Protocol,
                D0Pin = door.D0Pin,
                D1Pin = door.D1Pin,
                OsdpAddress = door.OsdpAddress,
                OsdpSerialPort = door.OsdpSerialPort,
                OsdpBaudRate = door.OsdpBaudRate,
                OsdpNakManufacturerCommand = door.OsdpNakManufacturerCommand,
                OsdpCapContactStatusCompliance = door.OsdpCapContactStatusCompliance,
                OsdpCapContactStatusInputs = door.OsdpCapContactStatusInputs,
                OsdpCapOutputControlCompliance = door.OsdpCapOutputControlCompliance,
                OsdpCapOutputControlCount = door.OsdpCapOutputControlCount,
                OsdpCapAudibleOutputCompliance = door.OsdpCapAudibleOutputCompliance,
                OsdpCapTextOutputCompliance = door.OsdpCapTextOutputCompliance,
                OsdpCapTextOutputDisplays = door.OsdpCapTextOutputDisplays,
                OsdpCapCardDataFormatCompliance = door.OsdpCapCardDataFormatCompliance,
                OsdpCapLedControlCompliance = door.OsdpCapLedControlCompliance,
                OsdpCapLedsPerReader = door.OsdpCapLedsPerReader,
                OsdpCapCheckCharacterCompliance = door.OsdpCapCheckCharacterCompliance,
                OsdpCapDeclareAes128 = door.OsdpCapDeclareAes128,
                OsdpCapDeclareDefaultAesKey = door.OsdpCapDeclareDefaultAesKey,
                OsdpCapReceiveBufferSize = door.OsdpCapReceiveBufferSize,
                OsdpCapLargestCombinedMessageSize = door.OsdpCapLargestCombinedMessageSize,
                OsdpCapOsdpVersion = door.OsdpCapOsdpVersion,
                OsdpCapDownstreamReaders = door.OsdpCapDownstreamReaders,
                OsdpIdVendorCode = door.OsdpIdVendorCode,
                OsdpIdModelNumber = door.OsdpIdModelNumber,
                OsdpIdHardwareVersion = door.OsdpIdHardwareVersion,
                OsdpIdSerialNumber = door.OsdpIdSerialNumber,
                OsdpIdFirmwareMajor = door.OsdpIdFirmwareMajor,
                OsdpIdFirmwareMinor = door.OsdpIdFirmwareMinor,
                OsdpIdFirmwareBuild = door.OsdpIdFirmwareBuild,
                OsdpComsetHandling = door.OsdpComsetHandling,
                OsdpConnectionTimeoutSeconds = door.OsdpConnectionTimeoutSeconds,
                OsdpReplyTimeoutMilliseconds = door.OsdpReplyTimeoutMilliseconds,
                OsdpDefaultLedColor = door.OsdpDefaultLedColor,
                HasDps = door.DpsPin.HasValue || door.DpsModbusChannel.HasValue,
                DpsPin = door.DpsPin,
                DpsModeIsModbus = door.DpsModbusChannel.HasValue,
                DpsModbusChannel = door.DpsModbusChannel,
                DpsNormallyOpen = door.DpsNormallyOpen,
                HasRex = door.RexPin.HasValue || door.RexModbusChannel.HasValue,
                RexPin = door.RexPin,
                RexModeIsModbus = door.RexModbusChannel.HasValue,
                RexModbusChannel = door.RexModbusChannel,
                RexNormallyOpen = door.RexNormallyOpen,
                ModbusTransportIsTcp = door.ModbusTcpHost is not null,
                ModbusSerialPort = door.ModbusSerialPort,
                ModbusTcpHost = door.ModbusTcpHost,
                ModbusTcpPort = door.ModbusTcpPort,
                ModbusUnitId = door.ModbusUnitId,
            };
        }

        await LoadAvailableSerialPortsAsync();
    }

    private async Task LoadAvailableSerialPortsAsync() => _availableSerialPorts = await Doors.GetAvailableSerialPortsAsync(Editing?.Id);

    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(_form.Label))
        {
            _error = "Label is required.";
            _activeTabIndex = 0;

            return;
        }

        _saving = true;
        _error = null;

        try
        {
            var anyModbus = AnyModbus;
            var osdp = _form.Protocol == ProtocolType.Osdp;

            var dto = new DoorConfiguration(
                Editing?.Id ?? 0,
                _form.Label.Trim(),
                _form.Protocol,
                _form.Protocol == ProtocolType.Wiegand
                    ? _form.D0Pin
                    : null,
                _form.Protocol == ProtocolType.Wiegand
                    ? _form.D1Pin
                    : null,
                _form.Protocol == ProtocolType.Osdp
                    ? _form.OsdpAddress
                    : null,
                _form.Protocol == ProtocolType.Osdp
                    ? _form.OsdpSerialPort
                    : null,
                _form.Protocol == ProtocolType.Osdp
                    ? _form.OsdpBaudRate
                    : null,
                _form is { HasDps: true, DpsModeIsModbus: false }
                    ? _form.DpsPin
                    : null,
                _form is { HasRex: true, RexModeIsModbus: false }
                    ? _form.RexPin
                    : null,
                DpsNormallyOpen: _form is { HasDps: true, DpsNormallyOpen: true },
                RexNormallyOpen: _form is { HasRex: true, RexNormallyOpen: true },
                OsdpNakManufacturerCommand: _form is { Protocol: ProtocolType.Osdp, OsdpNakManufacturerCommand: true },
                ModbusSerialPort: anyModbus && !_form.ModbusTransportIsTcp
                    ? _form.ModbusSerialPort
                    : null,
                ModbusUnitId: anyModbus
                    ? _form.ModbusUnitId
                    : null,
                DpsModbusChannel: _form is { HasDps: true, DpsModeIsModbus: true }
                    ? _form.DpsModbusChannel
                    : null,
                RexModbusChannel: _form is { HasRex: true, RexModeIsModbus: true }
                    ? _form.RexModbusChannel
                    : null,
                ModbusTcpHost: anyModbus && _form.ModbusTransportIsTcp
                    ? _form.ModbusTcpHost
                    : null,
                ModbusTcpPort: anyModbus && _form.ModbusTransportIsTcp
                    ? _form.ModbusTcpPort
                    : null,

                // Advanced OSDP — every field is protocol-gated the same way the basic OSDP
                // fields above are, so a door switched to Wiegand doesn't carry stale values.
                OsdpCapContactStatusCompliance: osdp
                    ? _form.OsdpCapContactStatusCompliance
                    : null,
                OsdpCapContactStatusInputs: osdp
                    ? _form.OsdpCapContactStatusInputs
                    : null,
                OsdpCapOutputControlCompliance: osdp
                    ? _form.OsdpCapOutputControlCompliance
                    : null,
                OsdpCapOutputControlCount: osdp
                    ? _form.OsdpCapOutputControlCount
                    : null,
                OsdpCapAudibleOutputCompliance: osdp
                    ? _form.OsdpCapAudibleOutputCompliance
                    : null,
                OsdpCapTextOutputCompliance: osdp
                    ? _form.OsdpCapTextOutputCompliance
                    : null,
                OsdpCapTextOutputDisplays: osdp
                    ? _form.OsdpCapTextOutputDisplays
                    : null,
                OsdpCapCardDataFormatCompliance: osdp
                    ? _form.OsdpCapCardDataFormatCompliance
                    : null,
                OsdpCapLedControlCompliance: osdp
                    ? _form.OsdpCapLedControlCompliance
                    : null,
                OsdpCapLedsPerReader: osdp
                    ? _form.OsdpCapLedsPerReader
                    : null,
                OsdpCapCheckCharacterCompliance: osdp
                    ? _form.OsdpCapCheckCharacterCompliance
                    : null,
                OsdpCapDeclareAes128: osdp && _form.OsdpCapDeclareAes128,

                // The default-key bit is meaningless without the AES-128 bit, and the switch
                // is disabled in that state — clear it rather than sending a rejected pair.
                OsdpCapDeclareDefaultAesKey: osdp && _form is
                {
                    OsdpCapDeclareAes128: true, OsdpCapDeclareDefaultAesKey: true,
                },
                OsdpCapReceiveBufferSize: osdp
                    ? _form.OsdpCapReceiveBufferSize
                    : null,
                OsdpCapLargestCombinedMessageSize: osdp
                    ? _form.OsdpCapLargestCombinedMessageSize
                    : null,
                OsdpCapOsdpVersion: osdp
                    ? _form.OsdpCapOsdpVersion
                    : null,
                OsdpCapDownstreamReaders: osdp
                    ? _form.OsdpCapDownstreamReaders
                    : null,
                OsdpIdVendorCode: osdp
                    ? NullIfBlank(_form.OsdpIdVendorCode)
                    : null,
                OsdpIdModelNumber: osdp
                    ? _form.OsdpIdModelNumber
                    : null,
                OsdpIdHardwareVersion: osdp
                    ? _form.OsdpIdHardwareVersion
                    : null,
                OsdpIdSerialNumber: osdp
                    ? _form.OsdpIdSerialNumber
                    : null,
                OsdpIdFirmwareMajor: osdp
                    ? _form.OsdpIdFirmwareMajor
                    : null,
                OsdpIdFirmwareMinor: osdp
                    ? _form.OsdpIdFirmwareMinor
                    : null,
                OsdpIdFirmwareBuild: osdp
                    ? _form.OsdpIdFirmwareBuild
                    : null,
                OsdpComsetHandling: osdp
                    ? _form.OsdpComsetHandling
                    : OsdpComsetBehavior.Ignore,
                OsdpConnectionTimeoutSeconds: osdp
                    ? _form.OsdpConnectionTimeoutSeconds
                    : null,
                OsdpReplyTimeoutMilliseconds: osdp
                    ? _form.OsdpReplyTimeoutMilliseconds
                    : null,
                OsdpDefaultLedColor: osdp
                    ? _form.OsdpDefaultLedColor
                    : null);

            if (Editing is null)
            {
                var (_, error) = await Doors.CreateAsync(dto);
                if (error is not null)
                {
                    _error = error;

                    return;
                }
            }
            else
            {
                var (_, error) = await Doors.UpdateAsync(Editing.Id, dto);
                if (error is not null)
                {
                    _error = error;

                    return;
                }
            }

            MudDialog.Close(DialogResult.Ok(true));
        }
        catch (Exception ex)
        {
            _error = ex.Message;
        }
        finally
        {
            _saving = false;
        }
    }

    private void Cancel() => MudDialog.Close(DialogResult.Cancel());

    /// <summary>
    /// Collapses a cleared text field to null, since null is what "use the simulator default"
    /// means everywhere in the advanced OSDP settings.
    /// </summary>
    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();

    /// <summary>
    /// Label for the "leave blank" option of a capability whose stock value is advertised, so
    /// the default row names the level the simulator will actually declare.
    /// </summary>
    private static string DefaultLevelLabel(OsdpCapabilityLevel[] levels, byte stockValue) =>
        levels.FirstOrDefault(p => p.Value == stockValue).Label ?? stockValue.ToString();

    private Task OnHasDpsChanged(bool value)
    {
        _form.HasDps = value;
        ResetActiveTabIfDisabled();

        return Task.CompletedTask;
    }

    private Task OnDpsModeChanged(bool value)
    {
        _form.DpsModeIsModbus = value;
        ResetActiveTabIfDisabled();

        return Task.CompletedTask;
    }

    private Task OnHasRexChanged(bool value)
    {
        _form.HasRex = value;
        ResetActiveTabIfDisabled();

        return Task.CompletedTask;
    }

    private Task OnRexModeChanged(bool value)
    {
        _form.RexModeIsModbus = value;
        ResetActiveTabIfDisabled();

        return Task.CompletedTask;
    }

    private Task OnModbusTransportChanged(bool isTcp)
    {
        _form.ModbusTransportIsTcp = isTcp;

        return Task.CompletedTask;
    }

    private Task OnProtocolChanged(ProtocolType value)
    {
        _form.Protocol = value;
        ResetActiveTabIfDisabled();

        return Task.CompletedTask;
    }

    /// <summary>
    /// Returns focus to General if the active tab has just become disabled. MudTabs indexes
    /// panels positionally, so the conditionally-disabled panels are addressed by constant
    /// rather than by a literal that silently means a different tab once one is inserted.
    /// </summary>
    private void ResetActiveTabIfDisabled()
    {
        if (_activeTabIndex == AdvancedOsdpTabIndex && _form.Protocol != ProtocolType.Osdp)
            _activeTabIndex = GeneralTabIndex;

        if (_activeTabIndex == RelayBoardTabIndex && !AnyModbus)
            _activeTabIndex = GeneralTabIndex;
    }

    private sealed class DoorFormModel
    {
        public string Label { get; set; } = string.Empty;

        public ProtocolType Protocol { get; set; } = ProtocolType.Wiegand;

        public int? D0Pin { get; set; }

        public int? D1Pin { get; set; }

        public byte? OsdpAddress { get; set; }

        public string? OsdpSerialPort { get; set; }

        public int? OsdpBaudRate { get; set; }

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
        public OsdpComsetBehavior OsdpComsetHandling { get; set; } = OsdpComsetBehavior.Ignore;

        public int? OsdpConnectionTimeoutSeconds { get; set; }

        public int? OsdpReplyTimeoutMilliseconds { get; set; }

        public OsdpLedColor? OsdpDefaultLedColor { get; set; }

        public bool HasDps { get; set; }

        public int? DpsPin { get; set; }

        public bool DpsModeIsModbus { get; set; }

        public int? DpsModbusChannel { get; set; }

        public bool DpsNormallyOpen { get; set; }

        public bool HasRex { get; set; }

        public int? RexPin { get; set; }

        public bool RexModeIsModbus { get; set; }

        public int? RexModbusChannel { get; set; }

        public bool RexNormallyOpen { get; set; }

        public bool ModbusTransportIsTcp { get; set; }

        public string? ModbusSerialPort { get; set; }

        public string? ModbusTcpHost { get; set; }

        public int? ModbusTcpPort { get; set; }

        public byte? ModbusUnitId { get; set; }
    }
}
