using DoorSim.Hardware;
using DoorSim.Shared.Cards;

namespace DoorSim.Tests;

/// <summary>
/// Tests for the generic mask-based <see cref="CardFormatEncoder" />. The strongest evidence the
/// port is correct: reconstructing all four built-in <see cref="WiegandFormat" /> layouts via
/// hand-derived masks and cross-checking byte-for-byte against <see cref="WiegandTransmitter" />'s
/// fixed-format builders.
/// </summary>
public class CardFormatEncoderTests
{
    // 26 bits: [EP(1)][FC(8)][CN(16)][OP(1)]
    private const string Wiegand26CardMask = "XFFFFFFFFCCCCCCCCCCCCCCCCX";
    private const string Wiegand26Parity1 = "EPPPPPPPPPPPPXXXXXXXXXXXXX";
    private const string Wiegand26Parity2 = "XXXXXXXXXXXXXPPPPPPPPPPPPO";

    // 34 bits: [EP(1)][FC(8)][CN(24)][OP(1)]
    private const string Wiegand34CardMask = "XFFFFFFFFCCCCCCCCCCCCCCCCCCCCCCCCX";
    private const string Wiegand34Parity1 = "EPPPPPPPPPPPPPPPPXXXXXXXXXXXXXXXXX";
    private const string Wiegand34Parity2 = "XXXXXXXXXXXXXXXXXPPPPPPPPPPPPPPPPO";

    // 37 bits: [EP(1)][CN(35)][OP(1)] — no facility code
    private const string Wiegand37CardMask = "XCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCX";
    private const string Wiegand37Parity1 = "EPPPPPPPPPPPPPPPPPPXXXXXXXXXXXXXXXXXX";
    private const string Wiegand37Parity2 = "XXXXXXXXXXXXXXXXXXXPPPPPPPPPPPPPPPPPO";

    // 35 bits: [0][0][FC(12)][CN(20)][0] — no parity
    private const string HidCorporate1000CardMask = "00FFFFFFFFFFFFCCCCCCCCCCCCCCCCCCCC0";

    // -------------------------------------------------------------------------
    // Cross-checks against WiegandTransmitter's fixed-format builders
    // -------------------------------------------------------------------------

    [Test]
    [Arguments((ushort)0, (ushort)0)]
    [Arguments((ushort)1, (ushort)1)]
    [Arguments((ushort)255, (ushort)65535)]
    [Arguments((ushort)100, (ushort)12345)]
    public async Task Encode_Wiegand26Mask_MatchesBuildWiegand26(ushort fc, ushort cn)
    {
        var encoder = new CardFormatEncoder(Wiegand26CardMask, Wiegand26Parity1, Wiegand26Parity2);
        var frame = encoder.Encode(cn, fc);

        await Assert.That(frame).IsEqualTo(WiegandTransmitter.BuildWiegand26(fc, cn));
    }

    [Test]
    [Arguments((ushort)0, 0u)]
    [Arguments((ushort)255, 16_777_215u)]
    [Arguments((ushort)100, 54321u)]
    public async Task Encode_Wiegand34Mask_MatchesBuildWiegand34(ushort fc, uint cn)
    {
        var encoder = new CardFormatEncoder(Wiegand34CardMask, Wiegand34Parity1, Wiegand34Parity2);
        var frame = encoder.Encode(cn, fc);

        await Assert.That(frame).IsEqualTo(WiegandTransmitter.BuildWiegand34(fc, cn));
    }

    [Test]
    [Arguments(0u)]
    [Arguments(1u)]
    [Arguments(uint.MaxValue)]
    [Arguments(12_345_678u)]
    public async Task Encode_Wiegand37Mask_MatchesBuildWiegand37(uint cn)
    {
        var encoder = new CardFormatEncoder(Wiegand37CardMask, Wiegand37Parity1, Wiegand37Parity2);
        var frame = encoder.Encode(cn, 0);

        await Assert.That(frame).IsEqualTo(WiegandTransmitter.BuildWiegand37(cn));
    }

    [Test]
    [Arguments((ushort)0, 0u)]
    [Arguments((ushort)1, 1u)]
    [Arguments((ushort)4095, 1_048_575u)]
    [Arguments((ushort)500, 123_456u)]
    public async Task Encode_HidCorporate1000Mask_MatchesBuildHidCorporate1000(ushort fc, uint cn)
    {
        var encoder = new CardFormatEncoder(HidCorporate1000CardMask);
        var frame = encoder.Encode(cn, fc);

        await Assert.That(frame).IsEqualTo(WiegandTransmitter.BuildHidCorporate1000(fc, cn));
    }

    // -------------------------------------------------------------------------
    // Decode round-trip
    // -------------------------------------------------------------------------

    [Test]
    public async Task Decode_Wiegand26Mask_RoundTrips()
    {
        var encoder = new CardFormatEncoder(Wiegand26CardMask, Wiegand26Parity1, Wiegand26Parity2);
        var frame = encoder.Encode(12345, 100);

        var (cardNumber, facilityCode, parityValid) = encoder.Decode(frame);

        await Assert.That(cardNumber).IsEqualTo(12345u);
        await Assert.That(facilityCode).IsEqualTo((ushort)100);
        await Assert.That(parityValid).IsTrue();
    }

    [Test]
    public async Task Decode_HidCorporate1000Mask_RoundTrips()
    {
        var encoder = new CardFormatEncoder(HidCorporate1000CardMask);
        var frame = encoder.Encode(123_456, 500);

        var (cardNumber, facilityCode, parityValid) = encoder.Decode(frame);

        await Assert.That(cardNumber).IsEqualTo(123_456u);
        await Assert.That(facilityCode).IsEqualTo((ushort)500);
        await Assert.That(parityValid).IsTrue();
    }

    [Test]
    public async Task Decode_TamperedParityBit_ReturnsParityInvalid()
    {
        var encoder = new CardFormatEncoder(Wiegand26CardMask, Wiegand26Parity1, Wiegand26Parity2);
        var frame = encoder.Encode(12345, 100);
        var tampered = frame ^ 1UL; // flip the OP bit (bit 0)

        var (_, _, parityValid) = encoder.Decode(tampered);

        await Assert.That(parityValid).IsFalse();
    }

    // -------------------------------------------------------------------------
    // Overflow guard — the reference mask algorithm silently truncates; this port doesn't.
    // -------------------------------------------------------------------------

    [Test]
    public async Task Encode_CardNumberExceedsFieldWidth_Throws()
    {
        var encoder = new CardFormatEncoder("CCCCXXXX"); // 4-bit card field, max value 15
        await Assert.That(() => encoder.Encode(16, 0)).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Encode_FacilityCodeExceedsFieldWidth_Throws()
    {
        var encoder = new CardFormatEncoder("CCCCFFFF"); // 4-bit facility field, max value 15
        await Assert.That(() => encoder.Encode(0, 16)).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Encode_NonzeroFacilityCodeOnFacilityLessMask_Throws()
    {
        var encoder = new CardFormatEncoder("CCCCCCCC"); // no 'F' bits
        await Assert.That(() => encoder.Encode(0, 1)).Throws<ArgumentOutOfRangeException>();
    }

    // -------------------------------------------------------------------------
    // Constructor validation
    // -------------------------------------------------------------------------

    [Test]
    public async Task Constructor_NonContiguousCardBits_Throws() =>
        await Assert.That(() => new CardFormatEncoder("CCXXCCXX")).Throws<ArgumentException>();

    [Test]
    public async Task Constructor_NonContiguousFacilityBits_Throws() =>
        await Assert.That(() => new CardFormatEncoder("FFCCCCFF")).Throws<ArgumentException>();

    [Test]
    public async Task Constructor_InvalidCharacter_Throws() =>
        await Assert.That(() => new CardFormatEncoder("CCCCZZZZ")).Throws<ArgumentException>();

    [Test]
    public async Task Constructor_ParityMaskInvalidCharacter_Throws() =>
        await Assert.That(() => new CardFormatEncoder("CCCCXXXX", "PPPZXXXX")).Throws<ArgumentException>();

    [Test]
    public async Task Constructor_MaskExceedsSentinelLength_Throws() =>
        await Assert.That(() => new CardFormatEncoder(new string('C', 66))).Throws<ArgumentException>();

    [Test]
    public async Task Constructor_ParityBitOverCardBit_Throws() =>
        // 'E' (index 3) lands on a 'C' position in "CCCCXXXX".
        await Assert.That(() => new CardFormatEncoder("CCCCXXXX", "PPPEXXXX")).Throws<ArgumentException>();

    [Test]
    public async Task Constructor_ParityBitOverFacilityBit_Throws() =>
        await Assert.That(() => new CardFormatEncoder("FFFFCCCC", "EXXXPPPP")).Throws<ArgumentException>();

    [Test]
    public async Task Encode_NonzeroCardNumberOnCardLessMask_Throws() =>
        // Guarded rather than silently discarded: with no 'C' bits there is nowhere to put it.
        await Assert.That(() => new CardFormatEncoder("XXXXXXXX").Encode(1, 0))
            .Throws<ArgumentOutOfRangeException>();
}
