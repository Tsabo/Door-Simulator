using DoorSim.Shared.Cards;

namespace DoorSim.Tests;

/// <summary>
/// Mask shapes the original mask-based <c>CardEncoder</c> accepted, which the first port of
/// <see cref="CardFormatEncoder" /> wrongly rejected. <see cref="CardFormatAnalyzer" /> grades these
/// for the user; the encoder itself must still build them.
/// </summary>
public class CardFormatLegacyToleranceTests
{
    // 26 bits: [EP(1)][FC(8)][CN(16)][OP(1)]
    private const string Wiegand26CardMask = "XFFFFFFFFCCCCCCCCCCCCCCCCX";
    private const string Wiegand26Parity1 = "EPPPPPPPPPPPPXXXXXXXXXXXXX";
    private const string Wiegand26Parity2 = "XXXXXXXXXXXXXPPPPPPPPPPPPO";

    // The legacy 36-bit sample, verbatim from the design handoff. Its card mask uses 'P' markers
    // and its parity masks are 35/34/35 characters against a 36-character card mask.
    private const string Sample36CardMask = "1PPFFFFFFFFFFFFCCCCCCCCCCCCCCCCCCCCP";
    private const string Sample36Parity1 = "XEPPXPPXPPXPPXPPXPPXPPXPPXPPXPPXPPX";
    private const string Sample36Parity2 = "PPXPPXPPXPPXPPXPPXPPXPPXPPXPPXPPXO";
    private const string Sample36Parity3 = "OPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPP";

    private static CardFormatEncoder Sample36() =>
        new(Sample36CardMask, Sample36Parity1, Sample36Parity2, Sample36Parity3);

    private static CardFormatEncoder Wiegand26() =>
        new(Wiegand26CardMask, Wiegand26Parity1, Wiegand26Parity2);

    // -------------------------------------------------------------------------
    // Legacy tolerance
    // -------------------------------------------------------------------------

    [Test]
    [Arguments('P')]
    [Arguments('E')]
    [Arguments('O')]
    public async Task Constructor_CardMaskParityMarker_TreatedAsDontCare(char marker)
    {
        // Legacy definitions mark parity-owned bits in the card mask. They carry no
        // card/facility/fixed meaning, so the frame must match a plain 'X'.
        var marked = new CardFormatEncoder($"{marker}FFFFCCC");
        var plain = new CardFormatEncoder("XFFFFCCC");

        await Assert.That(marked.Encode(5, 9)).IsEqualTo(plain.Encode(5, 9));
        await Assert.That(marked.CardBitCount).IsEqualTo(3);
        await Assert.That(marked.FacilityBitCount).IsEqualTo(4);
    }

    [Test]
    public async Task Constructor_ParityMaskCardTokens_TreatedAsDontCare()
    {
        var mixed = new CardFormatEncoder("XXXXCCCC", "EPPPCCCC");
        var plain = new CardFormatEncoder("XXXXCCCC", "EPPPXXXX");

        await Assert.That(mixed.Encode(5, 0)).IsEqualTo(plain.Encode(5, 0));
    }

    [Test]
    public async Task Constructor_ShortParityMask_IsRightAligned()
    {
        // A 4-character parity mask against an 8-bit frame covers bits 0-3 only; the high
        // bits are simply excluded from the range rather than making the mask invalid.
        var encoder = new CardFormatEncoder("CCCCXXXX", "PPPO");
        var results = encoder.CheckParity(encoder.Encode(0, 0));

        await Assert.That(results.Count).IsEqualTo(1);
        await Assert.That(results[0].BitIndex).IsEqualTo(0);
        await Assert.That(results[0].Ok).IsTrue();
    }

    [Test]
    public async Task Constructor_ParityMaskMissingIndicator_BuildsRuleThatWritesNothing()
    {
        var encoder = new CardFormatEncoder("CCCCXXXX", "PPPPXXXX");

        await Assert.That(encoder.ParityRuleCount).IsEqualTo(1);
        await Assert.That(encoder.CheckParity(0)[0].BitIndex).IsEqualTo(-1);
    }

    [Test]
    public async Task Constructor_ParityMaskNoPBits_IsAllowed()
    {
        var encoder = new CardFormatEncoder("CCCCXXXX", "XXXXXXXO");

        await Assert.That(encoder.ParityRuleCount).IsEqualTo(1);

        // Odd parity over an empty range always has to set its own bit.
        await Assert.That(encoder.Encode(0, 0)).IsEqualTo(1UL);
    }

    [Test]
    public async Task Constructor_ParityMaskTwoIndicators_RightmostOwnsPositionLastSeenOwnsPolarity()
    {
        // Reversed, bit 4 ('O') is reached before bit 5 ('E'): bit 4 owns the position, and
        // 'E' — the last one seen — sets the polarity to even.
        var encoder = new CardFormatEncoder("XXXXXXXX", "XXEOXPPP");
        var result = encoder.CheckParity(0b0000_0111)[0];

        await Assert.That(result.BitIndex).IsEqualTo(4);
        await Assert.That(result.IsEven).IsTrue();
    }

    [Test]
    public async Task Constructor_TwoParityMasksTargetSameBit_IsAllowed()
    {
        var encoder = new CardFormatEncoder("CCCCXXXX", "PPPPXXXO", "PPPPXXXO");

        await Assert.That(encoder.ParityRuleCount).IsEqualTo(2);
    }

    [Test]
    public async Task Constructor_NoCardBits_IsAllowed()
    {
        var encoder = new CardFormatEncoder("XXXXXXXX");

        await Assert.That(encoder.CardBitCount).IsEqualTo(0);
        await Assert.That(encoder.Encode(0, 0)).IsEqualTo(0UL);
    }

    [Test]
    public async Task Constructor_ParityBitOverFixedBit_IsAllowed()
    {
        // Only a card or facility bit is out of bounds for a parity bit; a fixed 1/0 is fine,
        // which is what the 36-bit sample's third rule relies on.
        var encoder = new CardFormatEncoder("1XXXCCCC", "OPPPXXXX");

        await Assert.That(encoder.ParityRuleCount).IsEqualTo(1);
    }

    // -------------------------------------------------------------------------
    // The 36-bit sample format
    // -------------------------------------------------------------------------

    [Test]
    public async Task Sample36_DecodesKnownCredential()
    {
        CardFormatEncoder.TryParseHex("000000084E6018E6", out var bits);

        var (cardNumber, facilityCode, parityValid) = Sample36().Decode(bits);

        await Assert.That(cardNumber).IsEqualTo(3187u);
        await Assert.That(facilityCode).IsEqualTo((ushort)627);
        await Assert.That(parityValid).IsTrue();
    }

    [Test]
    public async Task Sample36_ParityBitsSitAtExpectedPositions()
    {
        var results = Sample36().CheckParity(0);

        await Assert.That(results.Count).IsEqualTo(3);
        await Assert.That(results[0].BitIndex).IsEqualTo(33);
        await Assert.That(results[0].IsEven).IsTrue();
        await Assert.That(results[1].BitIndex).IsEqualTo(0);
        await Assert.That(results[1].IsEven).IsFalse();
        await Assert.That(results[2].BitIndex).IsEqualTo(34);
        await Assert.That(results[2].IsEven).IsFalse();
    }

    [Test]
    public async Task Sample36_RoundTrips()
    {
        var encoder = Sample36();
        var (cardNumber, facilityCode, parityValid) = encoder.Decode(encoder.Encode(3187u, 627));

        await Assert.That(cardNumber).IsEqualTo(3187u);
        await Assert.That(facilityCode).IsEqualTo((ushort)627);
        await Assert.That(parityValid).IsTrue();
    }

    [Test]
    public async Task Sample36_FieldWidths()
    {
        var encoder = Sample36();

        await Assert.That(encoder.TotalBits).IsEqualTo(36);
        await Assert.That(encoder.CardBitCount).IsEqualTo(20);
        await Assert.That(encoder.FacilityBitCount).IsEqualTo(12);
        await Assert.That(encoder.HasSentinelBit).IsFalse();
    }

    // -------------------------------------------------------------------------
    // Per-rule parity results
    // -------------------------------------------------------------------------

    [Test]
    public async Task CheckParity_ValidFrame_AllRulesPass()
    {
        var encoder = Wiegand26();
        var results = encoder.CheckParity(encoder.Encode(12345, 100));

        await Assert.That(results.Count).IsEqualTo(2);

        foreach (var result in results)
            await Assert.That(result.Ok).IsTrue();
    }

    [Test]
    public async Task CheckParity_TamperedFrame_IdentifiesTheFailingRule()
    {
        var encoder = Wiegand26();

        // Flipping bit 0 breaks only the odd-parity rule that owns it.
        var results = encoder.CheckParity(encoder.Encode(12345, 100) ^ 1UL);

        await Assert.That(results[0].Ok).IsTrue();
        await Assert.That(results[1].Ok).IsFalse();
        await Assert.That(results[1].BitIndex).IsEqualTo(0);
    }

    [Test]
    public async Task CheckParity_ReportsBitsSetIncludingTheParityBit()
    {
        var results = Wiegand26().CheckParity(Wiegand26().Encode(12345, 100));

        // An even rule must see an even count, an odd rule an odd count.
        await Assert.That(results[0].BitsSet % 2).IsEqualTo(0);
        await Assert.That(results[1].BitsSet % 2).IsEqualTo(1);
    }

    // -------------------------------------------------------------------------
    // Hex, and the 65-bit sentinel
    // -------------------------------------------------------------------------

    [Test]
    public async Task EncodeHex_NonSentinelFormat_PadsTo12Digits()
    {
        var hex = Wiegand26().EncodeHex(12345, 100);

        await Assert.That(hex.Length).IsEqualTo(12);
        await Assert.That(hex).IsEqualTo(Wiegand26().Encode(12345, 100).ToString("X12"));
    }

    [Test]
    public async Task EncodeHex_SentinelFormat_PrefixesWith01()
    {
        // 65 characters: a fixed sentinel bit at bit 64 plus a 64-bit card field.
        var hex = new CardFormatEncoder("1" + new string('C', 64)).EncodeHex(1, 0);

        await Assert.That(hex.Length).IsEqualTo(18);
        await Assert.That(hex).IsEqualTo("010000000000000001");
    }

    [Test]
    public async Task SentinelBit_DoesNotAliasOntoBitZero()
    {
        // The original shifted 1UL << 64 for this bit, which C# masks to 1UL << 0 — silently
        // corrupting bit 0. The sentinel must stay out of the frame entirely.
        var encoder = new CardFormatEncoder("1" + new string('C', 64));

        await Assert.That(encoder.HasSentinelBit).IsTrue();
        await Assert.That(encoder.TotalBits).IsEqualTo(65);
        await Assert.That(encoder.Encode(0, 0)).IsEqualTo(0UL);
    }

    [Test]
    public async Task SentinelBit_ExcludedFromParity()
    {
        // The sentinel is not a frame bit, so a parity rule covering the whole mask must not
        // count it.
        var encoder = new CardFormatEncoder("1" + new string('X', 63) + "C", "O" + new string('P', 64));

        await Assert.That(encoder.CheckParity(0)[0].BitsSet).IsEqualTo(0);
    }

    [Test]
    public async Task Encode_SixtyFourBitCardField_RoundTripsWideValue()
    {
        var encoder = new CardFormatEncoder(new string('C', 64));
        var (cardNumber, _, _) = encoder.DecodeWide(encoder.Encode(ulong.MaxValue, 0));

        await Assert.That(cardNumber).IsEqualTo(ulong.MaxValue);
    }

    [Test]
    [Arguments("84E6018E6", 0x84E6018E6UL)]
    [Arguments("000000084E6018E6", 0x84E6018E6UL)]
    [Arguments("  84E6018E6  ", 0x84E6018E6UL)]
    [Arguments("ffffffffffffffff", ulong.MaxValue)]
    public async Task TryParseHex_ValidInput_Parses(string input, ulong expected)
    {
        await Assert.That(CardFormatEncoder.TryParseHex(input, out var bits)).IsTrue();
        await Assert.That(bits).IsEqualTo(expected);
    }

    [Test]
    public async Task TryParseHex_OverLongInput_KeepsLow16Nibbles()
    {
        // 17 nibbles: the leading '9' falls off rather than the trailing one.
        await Assert.That(CardFormatEncoder.TryParseHex("9" + new string('A', 16), out var bits)).IsTrue();
        await Assert.That(bits).IsEqualTo(0xAAAAAAAAAAAAAAAAUL);
    }

    [Test]
    public async Task TryParseHex_SentinelPrefixedInput_StripsThePrefix()
    {
        await Assert.That(CardFormatEncoder.TryParseHex("010000000000000001", out var bits)).IsTrue();
        await Assert.That(bits).IsEqualTo(1UL);
    }

    [Test]
    [Arguments("")]
    [Arguments("   ")]
    [Arguments("12G4")]
    [Arguments("0x1234")]
    [Arguments(null)]
    public async Task TryParseHex_InvalidInput_ReturnsFalse(string? input) =>
        await Assert.That(CardFormatEncoder.TryParseHex(input, out _)).IsFalse();
}
