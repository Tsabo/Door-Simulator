using System.Numerics;
using DoorSim.Shared.Models;

namespace DoorSim.Shared.Cards;

/// <summary>How badly a <see cref="FormatIssue" /> reflects on a format definition.</summary>
public enum IssueLevel
{
    /// <summary>Nothing wrong — the summary row shown when a format is clean.</summary>
    Ok,

    /// <summary>Worth knowing, but a legitimate way to define a format.</summary>
    Notice,

    /// <summary>Almost certainly an authoring mistake, but it still encodes.</summary>
    Warn,

    /// <summary>The format is unusable, or encodes the wrong credential.</summary>
    Error
}

/// <summary>One finding about a format definition.</summary>
public sealed record FormatIssue(IssueLevel Level, string Message);

/// <summary>
/// Everything <see cref="CardFormatAnalyzer" /> derives from a mask set in one pass, so a caller
/// doesn't re-parse the masks to render field widths beside the issue list.
/// </summary>
public sealed record FormatAnalysis(
    IReadOnlyList<FormatIssue> Issues,
    int CardBits,
    ulong MaxCardNumber,
    int FacilityBits,
    ulong MaxFacilityCode,
    int ParityRuleCount,
    IReadOnlyList<int> ParityBitPositions)
{
    /// <summary>The worst level present, which is the overall verdict.</summary>
    public IssueLevel Verdict => Issues.Count == 0
        ? IssueLevel.Ok
        : Issues.Max(p => p.Level);

    /// <summary>True when nothing in the mask set is an outright error.</summary>
    public bool IsValid => Verdict != IssueLevel.Error;
}

/// <summary>
/// Grades a mask set without throwing, so an editor can render a half-finished format and a save
/// path can reject a bad one using the same rules.
/// </summary>
/// <remarks>
/// <see cref="CardFormatEncoder" /> is deliberately tolerant — it accepts legacy mask shapes and
/// only rejects what it cannot encode — and its constructor throws, which an editor can't use for
/// live feedback. This type is the other half of that split: it parses the masks itself, never
/// throws, and reports every problem at once rather than just the first.
/// </remarks>
public static class CardFormatAnalyzer
{
    private const string CardTokens = "CFX10PEO";
    private const string ParityTokens = "PEOXCF10";

    /// <summary>Card-mask tokens a user can author (the legacy-only markers are not offered).</summary>
    public const string CardPaintTokens = "CF10X";

    /// <summary>Parity-mask tokens a user can author.</summary>
    public const string ParityPaintTokens = "PEOX";

    /// <summary>
    /// Grades a mask set, optionally also checking a pair of test values and a decode input against
    /// it. <paramref name="masks" /> is index 0 = card mask, 1..3 = parity masks.
    /// </summary>
    public static FormatAnalysis Analyze(IReadOnlyList<string?> masks,
        ulong cardIn = 0,
        ulong facilityIn = 0,
        string? hexIn = null)
    {
        var issues = new List<FormatIssue>();
        var cardMask = masks.Count > 0
            ? masks[0] ?? ""
            : "";

        var parityMasks = new List<string>();

        for (var i = 1; i < masks.Count && i <= 3; i++)
        {
            var mask = masks[i];

            // An all-X mask is the editor's way of saying "no rule here", and is stored as null.
            // It must not count as a parity rule, or the editor and the encoder disagree.
            if (!string.IsNullOrEmpty(mask) && mask.Any(c => c is 'P' or 'E' or 'O'))
                parityMasks.Add(mask);
        }

        var totalBits = cardMask.Length;
        var card = ParseRun(cardMask, 'C');
        var facility = ParseRun(cardMask, 'F');
        var maxCard = CardFormatEncoder.FieldMaximum(card.BitCount);
        var maxFacility = CardFormatEncoder.FieldMaximum(facility.BitCount);

        // Structure of the card mask.
        foreach (var c in cardMask)
        {
            if (!CardTokens.Contains(c))
            {
                issues.Add(new FormatIssue(IssueLevel.Error,
                    $"Card mask contains an invalid character: '{c}'."));

                break;
            }
        }

        if (card.Mask == 0)
        {
            issues.Add(new FormatIssue(IssueLevel.Error,
                "No card bits. Paint at least one C in the card mask."));
        }
        else if (!card.Contiguous)
        {
            issues.Add(new FormatIssue(IssueLevel.Error,
                "Card bits must form a single contiguous run — encoding is one shift-and-mask, so a split field decodes to a different number."));
        }

        if (facility.Mask != 0 && !facility.Contiguous)
        {
            issues.Add(new FormatIssue(IssueLevel.Error,
                "Facility code bits must form a single contiguous run — encoding is one shift-and-mask, so a split field decodes to a different number."));
        }

        if (totalBits > CardFormatEncoder.SentinelTotalBits)
        {
            issues.Add(new FormatIssue(IssueLevel.Error,
                "Card mask exceeds 64 bits. Only 64-bit values encode, plus the 65-bit sentinel case."));
        }
        else if (totalBits == CardFormatEncoder.SentinelTotalBits)
        {
            issues.Add(new FormatIssue(IssueLevel.Notice,
                "65-bit mask: hex output is prefixed with the 01 sentinel instead of the usual 12 digits."));
        }

        if (facility.Mask == 0)
        {
            issues.Add(new FormatIssue(IssueLevel.Notice,
                "No facility code bits. Every credential decodes with a facility code of 0."));
        }

        // Structure of each parity mask.
        var parityBitPositions = new List<int>();
        ulong covered = 0;

        for (var i = 0; i < parityMasks.Count; i++)
        {
            var mask = parityMasks[i];
            var n = i + 1;
            var rule = ParseParity(mask);

            foreach (var c in mask)
            {
                if (!ParityTokens.Contains(c))
                {
                    issues.Add(new FormatIssue(IssueLevel.Error,
                        $"Parity {n} contains an invalid character: '{c}'."));

                    break;
                }
            }

            if (rule.Bit == 0)
            {
                if (rule.Mask != 0)
                {
                    issues.Add(new FormatIssue(IssueLevel.Error,
                        $"Parity {n} has P bits but no E or O bit, so nothing is ever written."));
                }
            }
            else
            {
                parityBitPositions.Add(BitOperations.TrailingZeroCount(rule.Bit));

                if (rule.Mask == 0)
                {
                    issues.Add(new FormatIssue(IssueLevel.Warn,
                        $"Parity {n} has a parity bit but no P bits to check."));
                }

                if ((rule.Bit & (card.Mask | facility.Mask)) != 0)
                {
                    issues.Add(new FormatIssue(IssueLevel.Error,
                        $"Parity {n} writes its parity bit over a card or facility bit."));
                }

                // The design handoff also specifies a "parity bit is inside its own P range"
                // warning. It is unreachable by construction and is deliberately not implemented:
                // each mask position contributes to either the P group or the parity bit, never
                // both, so the two can never overlap. (The HTML prototype has the same dead rule.)
            }

            if (rule.IndicatorCount > 1)
            {
                issues.Add(new FormatIssue(IssueLevel.Warn,
                    $"Parity {n} has {rule.IndicatorCount} parity bits. Only the rightmost is written; the last one seen sets even or odd."));
            }

            covered |= rule.Mask | rule.Bit;
        }

        for (var i = 0; i < parityBitPositions.Count; i++)
        {
            for (var j = i + 1; j < parityBitPositions.Count; j++)
            {
                if (parityBitPositions[i] != parityBitPositions[j])
                    continue;

                issues.Add(new FormatIssue(IssueLevel.Warn,
                    $"Parity {i + 1} and parity {j + 1} target the same bit. The second rule writes over the first."));
            }
        }

        var payload = card.Mask | facility.Mask;
        var unprotected = payload & ~covered;

        if (parityMasks.Count > 0 && unprotected != 0)
        {
            issues.Add(new FormatIssue(IssueLevel.Notice,
                $"{BitOperations.PopCount(unprotected)} payload bits sit outside every parity range and are unprotected."));
        }

        // The test values on the bench, which overflow into neighbouring fields if too wide.
        if (cardIn > maxCard)
        {
            issues.Add(new FormatIssue(IssueLevel.Error,
                $"Test card number exceeds {maxCard}, the maximum for {card.BitCount} card bits. Overflow bits leak into neighbouring fields."));
        }

        if (facility.BitCount > 0 && facilityIn > maxFacility)
        {
            issues.Add(new FormatIssue(IssueLevel.Error,
                $"Test facility code exceeds {maxFacility}, the maximum for {facility.BitCount} facility bits."));
        }
        else if (facility.BitCount == 0 && facilityIn != 0)
        {
            issues.Add(new FormatIssue(IssueLevel.Error,
                "Test facility code must be 0 — this format has no facility code bits."));
        }

        // An empty decode box is simply nothing to check, not a problem worth a warning.
        if (!string.IsNullOrWhiteSpace(hexIn) && !CardFormatEncoder.TryParseHex(hexIn, out _))
            issues.Add(new FormatIssue(IssueLevel.Warn, "Decode input is not valid hex."));

        if (issues.Count == 0)
        {
            issues.Add(new FormatIssue(IssueLevel.Ok,
                $"Mask set is consistent. {card.BitCount} card bits (max {maxCard}), " +
                $"{facility.BitCount} facility bits (max {maxFacility}), {parityMasks.Count} parity rules."));
        }

        return new FormatAnalysis(issues, card.BitCount, maxCard, facility.BitCount, maxFacility,
            parityMasks.Count, parityBitPositions);
    }

    /// <summary>Convenience overload for grading a persisted format definition.</summary>
    public static FormatAnalysis Analyze(CustomCardFormat format) =>
        Analyze([format.CardMask, format.Parity1Mask ?? "", format.Parity2Mask ?? "", format.Parity3Mask ?? ""]);

    /// <summary>
    /// Pads <paramref name="mask" /> left with 'X' to <paramref name="bits" />, or truncates it from
    /// the left. Masks are stored right-aligned because bit 0 is the rightmost character, so a short
    /// parity mask simply excludes the high bits rather than being invalid.
    /// </summary>
    public static string PadMask(string? mask, int bits)
    {
        if (bits <= 0)
            return "";

        if (string.IsNullOrEmpty(mask))
            return new string('X', bits);

        if (mask.Length == bits)
            return mask;

        return mask.Length > bits
            ? mask[^bits..]
            : mask.PadLeft(bits, 'X');
    }

    private static (ulong Mask, int BitCount, bool Contiguous) ParseRun(string mask, char target)
    {
        ulong bits = 0;
        var low = -1;
        var high = -1;

        for (var bitPos = 0; bitPos < mask.Length && bitPos < CardFormatEncoder.MaxFrameBits; bitPos++)
        {
            if (mask[mask.Length - 1 - bitPos] != target)
                continue;

            if (low < 0)
                low = bitPos;

            high = bitPos;
            bits |= 1UL << bitPos;
        }

        if (low < 0)
            return (0, 0, true);

        var span = high - low + 1;

        return (bits, span, BitOperations.PopCount(bits) == span);
    }

    private static (ulong Bit, ulong Mask, bool IsEven, int IndicatorCount) ParseParity(string mask)
    {
        ulong bit = 0;
        ulong group = 0;
        var isEven = true;
        var indicators = 0;

        for (var bitPos = 0; bitPos < mask.Length && bitPos < CardFormatEncoder.MaxFrameBits; bitPos++)
        {
            var c = mask[mask.Length - 1 - bitPos];

            switch (c)
            {
                case 'P':
                    group |= 1UL << bitPos;

                    break;

                case 'E':
                case 'O':
                    indicators++;

                    if (bit == 0)
                        bit = 1UL << bitPos;

                    isEven = c == 'E';

                    break;
            }
        }

        return (bit, group, isEven, indicators);
    }
}
