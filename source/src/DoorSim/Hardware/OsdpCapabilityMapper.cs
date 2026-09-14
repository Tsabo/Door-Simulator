using OSDP.Net.Model;
using OSDP.Net.Model.ReplyData;
using DeviceCapabilities = OSDP.Net.Model.ReplyData.DeviceCapabilities;

namespace DoorSim.Hardware;

/// <summary>
/// Translates a door's advanced OSDP settings into the OSDP.Net reply objects the PD sends
/// during the handshake.
/// </summary>
/// <remarks>
/// This is the only place the shared configuration primitives meet OSDP.Net types, and it is
/// deliberately where all the decision logic lives. <see cref="LoggingDevice" /> is sealed
/// with <c>protected override</c> handlers and OSDP.Net grants no <c>InternalsVisibleTo</c>
/// to the test project, so those handlers are unreachable from tests — keeping them as thin
/// pass-throughs over this class is what makes the behaviour testable at all.
/// <para>
/// A door with every advanced setting left null must produce byte-for-byte the same replies
/// the simulator sent before advanced settings existed.
/// </para>
/// </remarks>
internal static class OsdpCapabilityMapper
{
    /// <summary>
    /// Builds the osdp_CAP reply, emitted in ascending function code so a capture diffs
    /// cleanly against a real PD's.
    /// </summary>
    /// <remarks>
    /// These are operator-declared, not derived from what the simulator actually implements.
    /// A door can deliberately over-state its behaviour — declaring a text display or AES-128
    /// it does not back up — to test how a panel reacts.
    /// </remarks>
    internal static DeviceCapabilities BuildCapabilities(DoorConfiguration door)
    {
        var capabilities = new List<DeviceCapability>();

        if (door.OsdpCapContactStatusCompliance is { } contactStatus)
        {
            capabilities.Add(new DeviceCapability(CapabilityFunction.ContactStatusMonitoring,
                contactStatus,
                door.OsdpCapContactStatusInputs ?? OsdpAdvancedDefaults.ContactStatusInputs));
        }

        if (door.OsdpCapOutputControlCompliance is { } outputControl)
        {
            capabilities.Add(new DeviceCapability(CapabilityFunction.OutputControl,
                outputControl,
                door.OsdpCapOutputControlCount ?? OsdpAdvancedDefaults.OutputControlCount));
        }

        // numberOf is specified as N/A for card data format — always zero.
        capabilities.Add(new DeviceCapability(CapabilityFunction.CardDataFormat,
            door.OsdpCapCardDataFormatCompliance ?? OsdpAdvancedDefaults.CardDataFormatCompliance,
            0));

        capabilities.Add(new DeviceCapability(CapabilityFunction.ReaderLEDControl,
            door.OsdpCapLedControlCompliance ?? OsdpAdvancedDefaults.LedControlCompliance,
            door.OsdpCapLedsPerReader ?? OsdpAdvancedDefaults.LedsPerReader));

        if (door.OsdpCapAudibleOutputCompliance is { } audibleOutput)
        {
            // numberOf is specified as ignored for audible output.
            capabilities.Add(new DeviceCapability(CapabilityFunction.ReaderAudibleOutput, audibleOutput, 0));
        }

        if (door.OsdpCapTextOutputCompliance is { } textOutput)
        {
            capabilities.Add(new DeviceCapability(CapabilityFunction.ReaderTextOutput,
                textOutput,
                door.OsdpCapTextOutputDisplays ?? OsdpAdvancedDefaults.TextOutputDisplays));
        }

        // numberOf is specified as N/A for check character support — always zero.
        capabilities.Add(new DeviceCapability(CapabilityFunction.CheckCharacterSupport,
            door.OsdpCapCheckCharacterCompliance ?? OsdpAdvancedDefaults.CheckCharacterCompliance,
            0));

        // Declaration only. The simulator never negotiates a secure channel regardless of
        // what these bits say; RequireSecurity stays false and there is no SCBK.
        capabilities.Add(new DeviceCapability(CapabilityFunction.CommunicationSecurity,
            door.OsdpCapDeclareAes128
                ? (byte)1
                : (byte)0,
            door.OsdpCapDeclareDefaultAesKey
                ? (byte)1
                : (byte)0));

        if (door.OsdpCapReceiveBufferSize is { } receiveBuffer)
        {
            var (compliance, numberOf) = OsdpAdvancedDefaults.SplitMessageSize(receiveBuffer);
            capabilities.Add(new DeviceCapability(CapabilityFunction.ReceiveBufferSize, compliance, numberOf));
        }

        if (door.OsdpCapLargestCombinedMessageSize is { } combinedSize)
        {
            var (compliance, numberOf) = OsdpAdvancedDefaults.SplitMessageSize(combinedSize);
            capabilities.Add(new DeviceCapability(CapabilityFunction.LargestCombinedMessageSize, compliance, numberOf));
        }

        if (door.OsdpCapDownstreamReaders is { } readers)
        {
            // The specification requires the compliance byte to be zero for this function.
            capabilities.Add(new DeviceCapability(CapabilityFunction.Readers, 0, readers));
        }

        if (door.OsdpCapOsdpVersion is { } version)
        {
            // numberOf is specified as N/A for OSDP version — always zero.
            capabilities.Add(new DeviceCapability(CapabilityFunction.OSDPVersion, version, 0));
        }

        return new DeviceCapabilities(capabilities);
    }

    /// <summary>
    /// Builds the osdp_ID reply. The serial number falls back to the door id, which is what
    /// the simulator reported before this was configurable.
    /// </summary>
    internal static DeviceIdentification BuildIdentification(DoorConfiguration door) =>
        new(ResolveVendorCode(door),
            door.OsdpIdModelNumber ?? OsdpAdvancedDefaults.ModelNumber,
            door.OsdpIdHardwareVersion ?? OsdpAdvancedDefaults.HardwareVersion,
            door.OsdpIdSerialNumber ?? door.Id,
            door.OsdpIdFirmwareMajor ?? OsdpAdvancedDefaults.FirmwareMajor,
            door.OsdpIdFirmwareMinor ?? OsdpAdvancedDefaults.FirmwareMinor,
            door.OsdpIdFirmwareBuild ?? OsdpAdvancedDefaults.FirmwareBuild);

    /// <summary>
    /// Builds the client identification (cUID) for the OSDP.Net device configuration, kept in
    /// step with <see cref="BuildIdentification" /> so the cUID and osdp_ID cannot diverge.
    /// </summary>
    internal static ClientIdentification BuildClientIdentification(DoorConfiguration door) =>
        new(ResolveVendorCode(door), (uint)(door.OsdpIdSerialNumber ?? door.Id));

    /// <summary>
    /// Resolves the configured vendor code, falling back to the stock code. An unparseable
    /// value also falls back rather than throwing: validation rejects it at the API boundary,
    /// and a door already in the database must never take the simulator down.
    /// </summary>
    private static byte[] ResolveVendorCode(DoorConfiguration door) =>
        OsdpAdvancedDefaults.TryParseVendorCode(door.OsdpIdVendorCode, out var vendorCode)
            ? vendorCode
            : [.. OsdpAdvancedDefaults.VendorCode];
}
