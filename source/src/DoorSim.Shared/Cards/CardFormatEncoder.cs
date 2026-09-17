using System.Numerics;
using DoorSim.Shared.Models;

namespace DoorSim.Shared.Cards;

/// <summary>
/// Encodes and decodes a Wiegand-style bit frame from a generic mask description, so panel
/// testing isn't limited to the four built-in <see cref="WiegandFormat" /> values.
/// </summary>
/// <remarks>
/// <para>
/// Mask syntax (one character per bit, matching <see cref="CustomCardFormat" />): the card mask
/// uses <c>C</c> for card-number bits, <c>F</c> for facility-code bits, <c>1</c>/<c>0</c> for
/// fixed bits, and <c>X</c> for don't-care; each parity mask uses <c>P</c> for bits folded into
/// that parity group's XOR and an <c>E</c>/<c>O</c> for the parity bit's own position and
/// polarity. Bit position 0 is the <em>last</em> character of each mask string (LSB) — the same
/// convention the original mask-based Wiegand encoder this was ported from uses. The 'C' bits
/// (and the 'F' bits, if any) must each form one contiguous run, since encoding is a single
/// shift-and-mask rather than a bit-by-bit gather/scatter.
/// </para>
/// <para>
/// Tolerances kept deliberately, for compatibility with legacy format definitions: characters
/// that carry no meaning for a given mask kind are ignored rather than rejected — notably
/// <c>P</c>/<c>E</c>/<c>O</c> in a card mask, which real definitions use to document "a parity
/// rule owns this bit"; parity masks shorter or longer than the card mask are right-aligned
/// against it; and a parity mask may carry several <c>E</c>/<c>O</c> characters, no <c>P</c>
/// bits, or no indicator at all. Use <see cref="CardFormatAnalyzer" /> to surface those shapes
/// to a user as graded issues — this type rejects only what it cannot encode without silently
/// producing the wrong credential.
/// </para>
/// </remarks>
public sealed class CardFormatEncoder
{
    /// <summary>The widest frame that fits a <see cref="ulong" />.</summary>
    public const int MaxFrameBits = 64;

    /// <summary>
    /// A 65-character card mask describes a 64-bit frame plus a sentinel bit that is not part of
    /// the frame at all — it surfaces only as the <c>01</c> prefix on <see cref="EncodeHex" />.
    /// </summary>
    public const int SentinelTotalBits = 65;

    private readonly ulong _cardMask;
    private readonly int _cardShift;
    private readonly ulong _facilityMask;
    private readonly int _facilityShift;
    private readonly ulong _fixedOffMask;
    private readonly ulong _fixedOnBits;
    private readonly List<ParityRule> _parityRules = [];

    public CardFormatEncoder(string cardMask, string? parity1Mask = null, string? parity2Mask = null, string? parity3Mask = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(cardMask);

        if (cardMask.Length > SentinelTotalBits)
            throw new ArgumentException($"Card mask cannot exceed {SentinelTotalBits} characters.", nameof(cardMask));

        foreach (var c in cardMask)
        {
            // 'P'/'E'/'O' are legal-but-inert here: legacy definitions use them in the card mask
            // to mark which bits the parity rules own. Anything else is a typo worth catching.
            if (c is not ('C' or 'F' or '0' or '1' or 'X' or 'P' or 'E' or 'O'))
                throw new ArgumentException($"Card mask contains an invalid character: '{c}'.", nameof(cardMask));
        }

        TotalBits = cardMask.Length;
        HasSentinelBit = TotalBits == SentinelTotalBits;

        (_cardMask, _cardShift, CardBitCount) = ParseField(cardMask, 'C');
        (_facilityMask, _facilityShift, FacilityBitCount) = ParseField(cardMask, 'F');
        (_fixedOnBits, _fixedOffMask) = ParseFixedBits(cardMask);

        foreach (var parityMask in new[] { parity1Mask, parity2Mask, parity3Mask })
        {
            if (!string.IsNullOrEmpty(parityMask))
                _parityRules.Add(ParseParityMask(parityMask, cardMask));
        }
    }

    /// <summary>Total bits in the frame (the card mask's length), including the 65-bit sentinel.</summary>
    public int TotalBits { get; }

    /// <summary>
    /// True for a 65-character mask, where bit 64 is a sentinel that never participates in the
    /// frame or in parity and surfaces only as <see cref="EncodeHex" />'s <c>01</c> prefix.
    /// </summary>
    public bool HasSentinelBit { get; }

    /// <summary>Width, in bits, of the contiguous card-number field — 0 if the mask has no 'C' bits.</summary>
    public int CardBitCount { get; }

    /// <summary>Width, in bits, of the contiguous facility-code field — 0 if the mask has no 'F' bits.</summary>
    public int FacilityBitCount { get; }

    /// <summary>How many parity rules this format declares.</summary>
    public int ParityRuleCount => _parityRules.Count;

    /// <summary>
    /// Builds the bit frame for the given credential. Throws <see cref="ArgumentOutOfRangeException" />
    /// if either value overflows its field width — the mask arithmetic doesn't check this itself and
    /// would otherwise silently spill into neighboring bits.
    /// </summary>
    public ulong Encode(uint cardNumber, ushort facilityCode) => Encode((ulong)cardNumber, facilityCode);

    /// <summary>
    /// Wide overload of <see cref="Encode(uint,ushort)" />, for formats whose card field is wider
    /// than 32 bits — the case the 65-bit sentinel exists to serve.
    /// </summary>
    public ulong Encode(ulong cardNumber, ulong facilityCode)
    {
        if (CardBitCount == 0)
        {
            if (cardNumber != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(cardNumber), cardNumber,
                    "This format has no card-number bits — cardNumber must be 0.");
            }
        }
        else if (cardNumber > FieldMaximum(CardBitCount))
        {
            throw new ArgumentOutOfRangeException(nameof(cardNumber), cardNumber,
                $"Card number exceeds the {CardBitCount}-bit card field.");
        }

        if (FacilityBitCount == 0)
        {
            if (facilityCode != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(facilityCode), facilityCode,
                    "This format has no facility-code bits — facilityCode must be 0.");
            }
        }
        else if (facilityCode > FieldMaximum(FacilityBitCount))
        {
            throw new ArgumentOutOfRangeException(nameof(facilityCode), facilityCode,
                $"Facility code exceeds the {FacilityBitCount}-bit facility field.");
        }

        var frame = cardNumber << _cardShift;

        if (_facilityMask != 0)
            frame |= facilityCode << _facilityShift;

        frame |= _fixedOnBits;
        frame &= _fixedOffMask;

        foreach (var rule in _parityRules)
        {
            var bitCount = BitOperations.PopCount(frame & rule.ParityMask);
            var setBit = rule.IsEven
                ? bitCount % 2 != 0
                : bitCount % 2 == 0;

            if (setBit)
                frame |= rule.ParityBit;
        }

        return frame;
    }

    /// <summary>
    /// Encodes a credential and formats it the way the panel-side tooling expects: 12 hex digits,
    /// or the <c>01</c> sentinel prefix plus 16 digits for a 65-bit format.
    /// </summary>
    public string EncodeHex(ulong cardNumber, ulong facilityCode)
    {
        var frame = Encode(cardNumber, facilityCode);

        return HasSentinelBit
            ? "01" + frame.ToString("X16")
            : frame.ToString("X12");
    }

    /// <summary>Extracts the credential and checks parity for a previously-built frame.</summary>
    public (uint CardNumber, ushort FacilityCode, bool ParityValid) Decode(ulong bits)
    {
        var (cardNumber, facilityCode, parityValid) = DecodeWide(bits);

        return ((uint)cardNumber, (ushort)facilityCode, parityValid);
    }

    /// <summary>
    /// Wide overload of <see cref="Decode(ulong)" />, for formats whose fields are too wide to fit
    /// the <see cref="uint" />/<see cref="ushort" /> pair the rest of the pipeline carries.
    /// </summary>
    public (ulong CardNumber, ulong FacilityCode, bool ParityValid) DecodeWide(ulong bits)
    {
        var cardNumber = (bits & _cardMask) >> _cardShift;
        var facilityCode = _facilityMask != 0
            ? (bits & _facilityMask) >> _facilityShift
            : 0UL;

        var parityValid = true;

        foreach (var result in CheckParity(bits))
        {
            if (!result.Ok)
                parityValid = false;
        }

        return (cardNumber, facilityCode, parityValid);
    }

    /// <summary>
    /// Per-rule parity results for a frame, in mask order. <see cref="Decode(ulong)" /> collapses
    /// these to a single bool; a test bench wants to know which rule failed and how many bits it saw.
    /// </summary>
    public IReadOnlyList<ParityResult> CheckParity(ulong bits)
    {
        var results = new List<ParityResult>(_parityRules.Count);

        foreach (var rule in _parityRules)
        {
            var bitsSet = BitOperations.PopCount(bits & rule.ParityMask | bits & rule.ParityBit);
            var expected = rule.IsEven
                ? 0
                : 1;

            results.Add(new ParityResult(
                rule.ParityBit == 0
                    ? -1
                    : BitOperations.TrailingZeroCount(rule.ParityBit),
                rule.IsEven,
                bitsSet,
                (bitsSet & 1) == expected));
        }

        return results;
    }

    /// <summary>
    /// Parses a credential in hex. Accepts the <c>01</c> sentinel prefix, and keeps the low 16
    /// nibbles of an over-long value — anything above bit 63 can't be represented in the frame.
    /// </summary>
    public static bool TryParseHex(string? hex, out ulong bits)
    {
        bits = 0;

        if (string.IsNullOrWhiteSpace(hex))
            return false;

        var trimmed = hex.Trim();

        // "01" + 16 nibbles is the sentinel form EncodeHex emits for a 65-bit format.
        if (trimmed.Length == 18 && trimmed.StartsWith("01", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed[2..];

        if (trimmed.Length > 16)
            trimmed = trimmed[^16..];

        foreach (var c in trimmed)
        {
            if (!Uri.IsHexDigit(c))
                return false;
        }

        bits = Convert.ToUInt64(trimmed, 16);

        return true;
    }

    /// <summary>The largest value a field of the given width can hold.</summary>
    public static ulong FieldMaximum(int fieldBitCount) => fieldBitCount switch
    {
        <= 0 => 0,
        >= MaxFrameBits => ulong.MaxValue,
        _ => (1UL << fieldBitCount) - 1
    };

    /// <summary>Finds the contiguous run of <paramref name="target" /> characters in the mask (LSB = last character).</summary>
    private static (ulong mask, int shift, int bitCount) ParseField(string cardMask, char target)
    {
        ulong mask = 0;
        var shift = -1;
        var maxBit = -1;

        foreach (var (bitPos, c) in EnumerateBits(cardMask))
        {
            if (c != target)
                continue;

            if (shift < 0)
                shift = bitPos;

            maxBit = bitPos;
            mask |= 1UL << bitPos;
        }

        if (shift < 0)
            return (0, 0, 0);

        var bitCount = maxBit - shift + 1;

        if (BitOperations.PopCount(mask) != (uint)bitCount)
            throw new ArgumentException($"Card mask's '{target}' bits must form a single contiguous run.", nameof(cardMask));

        return (mask, shift, bitCount);
    }

    private static (ulong onBits, ulong offMask) ParseFixedBits(string cardMask)
    {
        ulong onBits = 0;
        var offMask = ulong.MaxValue;

        foreach (var (bitPos, c) in EnumerateBits(cardMask))
        {
            if (c == '1')
                onBits |= 1UL << bitPos;
            else if (c == '0')
                offMask &= ~(1UL << bitPos);
        }

        return (onBits, offMask);
    }

    private static ParityRule ParseParityMask(string parityMask, string cardMask)
    {
        ulong parityGroupMask = 0;
        ulong parityBit = 0;
        var isEven = true;

        foreach (var (bitPos, c) in EnumerateBits(parityMask))
        {
            switch (c)
            {
                case 'P':
                    parityGroupMask |= 1UL << bitPos;

                    break;

                case 'E':
                case 'O':
                    // Several indicators are legal in legacy data: the rightmost (lowest bit
                    // position, reached first here) owns the position, and the last one seen
                    // wins the polarity.
                    if (parityBit == 0)
                        parityBit = 1UL << bitPos;

                    isEven = c == 'E';

                    if (CharAtBit(cardMask, bitPos) is 'C' or 'F')
                    {
                        throw new ArgumentException(
                            "The parity bit's own position must not be a card or facility bit in the card mask.",
                            nameof(parityMask));
                    }

                    break;

                // 'C'/'F'/'0'/'1' are legal-but-inert here, mirroring the card mask's tolerance
                // of 'P'/'E'/'O'. 'X' is an explicit don't-care.
                case 'X':
                case 'C':
                case 'F':
                case '0':
                case '1':
                    break;

                default:
                    throw new ArgumentException($"Parity mask contains an invalid character: '{c}'.", nameof(parityMask));
            }
        }

        return new ParityRule(parityBit, parityGroupMask, isEven);
    }

    /// <summary>
    /// Walks a mask from its last character to its first, yielding the bit position each character
    /// describes. Positions at or above <see cref="MaxFrameBits" /> — reachable only via the 65-bit
    /// sentinel — are skipped, since they don't fit the frame and shifting by 64 would wrap to bit 0.
    /// </summary>
    private static IEnumerable<(int BitPos, char Char)> EnumerateBits(string mask)
    {
        for (var i = mask.Length - 1; i >= 0; i--)
        {
            var bitPos = mask.Length - 1 - i;

            if (bitPos >= MaxFrameBits)
                break;

            yield return (bitPos, mask[i]);
        }
    }

    /// <summary>The character describing <paramref name="bitPos" />, right-aligning the mask (LSB = last character).</summary>
    private static char CharAtBit(string mask, int bitPos)
    {
        var index = mask.Length - 1 - bitPos;

        return index >= 0 && index < mask.Length
            ? mask[index]
            : 'X';
    }

    private readonly record struct ParityRule(ulong ParityBit, ulong ParityMask, bool IsEven);
}

/// <summary>One parity rule's outcome for a decoded frame.</summary>
/// <param name="BitIndex">Bit position of the parity bit itself, or -1 if the mask declares none.</param>
/// <param name="IsEven">True for even parity, false for odd.</param>
/// <param name="BitsSet">How many bits the rule saw set, including its own parity bit.</param>
/// <param name="Ok">Whether the rule holds.</param>
public readonly record struct ParityResult(int BitIndex, bool IsEven, int BitsSet, bool Ok);
