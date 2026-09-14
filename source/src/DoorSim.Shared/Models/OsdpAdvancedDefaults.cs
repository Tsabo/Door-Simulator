using System.Globalization;

namespace DoorSim.Shared.Models;

/// <summary>
/// Simulator defaults and legal-value tables for the advanced OSDP settings.
/// </summary>
/// <remarks>
/// Every advanced setting on <see cref="DoorConfiguration" /> is nullable, where null means
/// "use the simulator's stock behaviour". The stock values live here rather than in the
/// simulator so the UI, the server-side validator, and the capability mapper all resolve a
/// null the same way.
/// <para>
/// Compliance-level tables and their descriptions are transcribed from the XML documentation
/// on OSDP.Net's <c>CapabilityFunction</c> enum, which the Blazor client cannot reference.
/// </para>
/// </remarks>
public static class OsdpAdvancedDefaults
{
    // -------------------------------------------------------------------------
    // Stock capability bytes — what the simulator advertised before advanced
    // settings existed. An all-null door must still produce exactly these.
    // -------------------------------------------------------------------------

    /// <summary>Card data sent as a raw bit array, up to 1024 bits.</summary>
    public const byte CardDataFormatCompliance = 1;

    /// <summary>Reader LED supports on/off control only.</summary>
    public const byte LedControlCompliance = 1;

    /// <summary>One LED per reader.</summary>
    public const byte LedsPerReader = 1;

    /// <summary>CRC-16 check characters supported.</summary>
    public const byte CheckCharacterCompliance = 1;

    // -------------------------------------------------------------------------
    // Defaults applied only once an opt-in capability has been enabled.
    // -------------------------------------------------------------------------

    /// <summary>The simulator's osdp_ISTAT reply reports two inputs: DPS and REX.</summary>
    public const byte ContactStatusInputs = 2;

    /// <summary>The simulator acknowledges output control for a single output.</summary>
    public const byte OutputControlCount = 1;

    /// <summary>No textual displays, matching compliance level 0.</summary>
    public const byte TextOutputDisplays = 0;

    /// <summary>Stock vendor code in the dashed-hex form the UI displays.</summary>
    public const string VendorCodeText = "00-00-01";

    /// <summary>Number of bytes in an OSDP vendor code.</summary>
    public const int VendorCodeLength = 3;

    public const byte ModelNumber = 1;
    public const byte HardwareVersion = 1;
    public const byte FirmwareMajor = 1;
    public const byte FirmwareMinor = 0;
    public const byte FirmwareBuild = 0;

    // -------------------------------------------------------------------------
    // Timing.
    // -------------------------------------------------------------------------

    /// <summary>
    /// Default OSDP connection timeout, in seconds.
    /// </summary>
    /// <remarks>
    /// Upstream OSDP.Net default is 8s, tuned for fast-polling ACUs. Mercury MP1502 panels
    /// were observed polling as slow as ~3s per reader with real-world jitter, which made
    /// Device.IsConnected flicker false against the 8s default despite a healthy link.
    /// </remarks>
    public const int ConnectionTimeoutSeconds = 20;

    /// <summary>
    /// Default OSDP reply / inter-byte timeout, in milliseconds.
    /// </summary>
    /// <remarks>
    /// Upstream OSDP.Net default is 200ms, which assumes near-instantaneous delivery of the
    /// remaining bytes of an in-progress frame. On this Pi (one USB bus shared across many
    /// OSDP + Modbus serial devices), real inter-byte gaps within a single frame occasionally
    /// exceeded 200ms, throwing a TimeoutException that tore down and reopened the whole
    /// connection every cycle — confirmed via direct instrumentation, root cause of the
    /// "readers keep showing disconnected" issue, not the ConnectionTimeout above.
    /// </remarks>
    public const int ReplyTimeoutMilliseconds = 2000;

    public const int MinConnectionTimeoutSeconds = 1;
    public const int MaxConnectionTimeoutSeconds = 120;
    public const int MinReplyTimeoutMilliseconds = 50;
    public const int MaxReplyTimeoutMilliseconds = 10_000;

    /// <summary>Message-size capabilities are a 16-bit value split across two bytes.</summary>
    public const int MinMessageSize = 1;

    /// <inheritdoc cref="MinMessageSize" />
    public const int MaxMessageSize = ushort.MaxValue;

    // -------------------------------------------------------------------------
    // Stock osdp_ID values.
    // -------------------------------------------------------------------------

    /// <summary>Stock three-byte OSDP vendor code.</summary>
    public static readonly byte[] VendorCode = [0x00, 0x00, 0x01];

    // -------------------------------------------------------------------------
    // Legal compliance levels, with the specification's meaning for each.
    // -------------------------------------------------------------------------

    public static readonly OsdpCapabilityLevel[] ContactStatusLevels =
    [
        new(1, "1 — Report circuit state, no supervision"),
        new(2, "2 — Plus configurable normally-open/closed encoding per circuit"),
        new(3, "3 — Plus supervised monitoring"),
        new(4, "4 — Plus custom end-of-line settings")
    ];

    public static readonly OsdpCapabilityLevel[] OutputControlLevels =
    [
        new(1, "1 — Activate/deactivate on panel command"),
        new(2, "2 — Plus configurable inactive state (inverted drive)"),
        new(3, "3 — Plus supervised monitoring"),
        new(4, "4 — Plus custom end-of-line settings")
    ];

    public static readonly OsdpCapabilityLevel[] CardDataFormatLevels =
    [
        new(1, "1 — Raw bit array, up to 1024 bits"),
        new(2, "2 — BCD character array, up to 256 characters"),
        new(3, "3 — Either raw bits or BCD characters")
    ];

    public static readonly OsdpCapabilityLevel[] LedControlLevels =
    [
        new(1, "1 — On/off control only"),
        new(2, "2 — Timed commands"),
        new(3, "3 — Timed commands plus bi-color LEDs"),
        new(4, "4 — Timed commands plus tri-color LEDs")
    ];

    public static readonly OsdpCapabilityLevel[] AudibleOutputLevels =
    [
        new(1, "1 — On/off control only"),
        new(2, "2 — Timed commands")
    ];

    public static readonly OsdpCapabilityLevel[] TextOutputLevels =
    [
        new(0, "0 — No text display support"),
        new(1, "1 — One row of 16 characters"),
        new(2, "2 — Two rows of 16 characters"),
        new(3, "3 — Four rows of 16 characters")
    ];

    public static readonly OsdpCapabilityLevel[] CheckCharacterLevels =
    [
        new(0, "0 — Checksum only, no CRC-16"),
        new(1, "1 — 16-bit CRC-16 supported")
    ];

    public static readonly OsdpCapabilityLevel[] OsdpVersionLevels =
    [
        new(0, "0 — Unspecified"),
        new(1, "1 — IEC 60839-11-5"),
        new(2, "2 — SIA OSDP 2.2")
    ];

    // -------------------------------------------------------------------------
    // Resolve / validate helpers.
    // -------------------------------------------------------------------------

    /// <summary>Resolves a nullable configured connection timeout to the value to use.</summary>
    public static TimeSpan ResolveConnectionTimeout(int? seconds) =>
        TimeSpan.FromSeconds(seconds ?? ConnectionTimeoutSeconds);

    /// <summary>Resolves a nullable configured reply timeout to the value to use.</summary>
    public static TimeSpan ResolveReplyTimeout(int? milliseconds) =>
        TimeSpan.FromMilliseconds(milliseconds ?? ReplyTimeoutMilliseconds);

    /// <summary>
    /// True if <paramref name="value" /> is null (meaning "simulator default") or appears in
    /// <paramref name="levels" />.
    /// </summary>
    public static bool IsLegalLevel(OsdpCapabilityLevel[] levels, byte? value) =>
        value is null || levels.Any(l => l.Value == value.Value);

    /// <summary>
    /// Parses an OSDP vendor code. Accepts "00-00-01", "00 00 01", "000001" and "0x000001".
    /// A null or blank input yields <see cref="VendorCode" /> and returns true, since null
    /// means "simulator default" everywhere else.
    /// </summary>
    public static bool TryParseVendorCode(string? text, out byte[] vendorCode)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            vendorCode = [.. VendorCode];

            return true;
        }

        var digits = text.Trim();
        if (digits.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            digits = digits[2..];

        digits = digits.Replace("-", string.Empty).Replace(" ", string.Empty).Replace(":", string.Empty);

        if (digits.Length != VendorCodeLength * 2)
        {
            vendorCode = [];

            return false;
        }

        var parsed = new byte[VendorCodeLength];
        for (var i = 0; i < VendorCodeLength; i++)
        {
            if (byte.TryParse(digits.AsSpan(i * 2, 2), NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture, out parsed[i]))
                continue;

            vendorCode = [];

            return false;
        }

        vendorCode = parsed;

        return true;
    }

    /// <summary>Formats a vendor code as dashed hex, e.g. "00-00-01".</summary>
    public static string FormatVendorCode(byte[] vendorCode) => BitConverter.ToString(vendorCode);

    /// <summary>
    /// Splits a 16-bit message-size capability into its two capability bytes. Per the OSDP
    /// specification the compliance byte carries the LSB and the "number of" byte the MSB.
    /// </summary>
    public static (byte Compliance, byte NumberOf) SplitMessageSize(int size) =>
        ((byte)(size & 0xFF), (byte)(size >> 8 & 0xFF));
}
