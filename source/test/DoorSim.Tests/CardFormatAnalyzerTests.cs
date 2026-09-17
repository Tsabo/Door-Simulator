using DoorSim.Shared.Cards;

namespace DoorSim.Tests;

/// <summary>
/// Rules for <see cref="CardFormatAnalyzer" />, the non-throwing grader the editor renders live and
/// the save path gates on.
/// </summary>
public class CardFormatAnalyzerTests
{
    private const string ValidCardMask = "XFFFFFFFFCCCCCCCCCCCCCCCCX";
    private const string ValidParity1 = "EPPPPPPPPPPPPXXXXXXXXXXXXX";
    private const string ValidParity2 = "XXXXXXXXXXXXXPPPPPPPPPPPPO";

    private static string[] Valid() => [ValidCardMask, ValidParity1, ValidParity2, ""];

    private static FormatAnalysis AnalyzeCard(string cardMask) =>
        CardFormatAnalyzer.Analyze([cardMask, "", "", ""]);

    // -------------------------------------------------------------------------
    // The clean case
    // -------------------------------------------------------------------------

    [Test]
    public async Task Analyze_CleanFormat_ReportsSingleOkIssue()
    {
        var analysis = CardFormatAnalyzer.Analyze(Valid());

        await Assert.That(analysis.Issues.Count).IsEqualTo(1);
        await Assert.That(analysis.Issues[0].Level).IsEqualTo(IssueLevel.Ok);
        await Assert.That(analysis.Verdict).IsEqualTo(IssueLevel.Ok);
        await Assert.That(analysis.IsValid).IsTrue();
    }

    [Test]
    public async Task Analyze_CleanFormat_OkMessageSummarisesFields()
    {
        var message = CardFormatAnalyzer.Analyze(Valid()).Issues[0].Message;

        await Assert.That(message)
            .IsEqualTo(
                "Mask set is consistent. 16 card bits (max 65535), 8 facility bits (max 255), 2 parity rules.");
    }

    [Test]
    public async Task Analyze_CleanFormat_ReportsDerivedWidths()
    {
        var analysis = CardFormatAnalyzer.Analyze(Valid());

        await Assert.That(analysis.CardBits).IsEqualTo(16);
        await Assert.That(analysis.MaxCardNumber).IsEqualTo(65535UL);
        await Assert.That(analysis.FacilityBits).IsEqualTo(8);
        await Assert.That(analysis.MaxFacilityCode).IsEqualTo(255UL);
        await Assert.That(analysis.ParityRuleCount).IsEqualTo(2);
        await Assert.That(analysis.ParityBitPositions).IsEquivalentTo([25, 0]);
    }

    // -------------------------------------------------------------------------
    // Errors
    // -------------------------------------------------------------------------

    [Test]
    public async Task Analyze_NoCardBits_ReportsError() =>
        await AssertIssue(AnalyzeCard("XXXXXXXX"), IssueLevel.Error, "No card bits");

    [Test]
    public async Task Analyze_NonContiguousCardBits_ReportsError() =>
        await AssertIssue(AnalyzeCard("CCXXCCXX"), IssueLevel.Error, "Card bits must form a single contiguous run");

    [Test]
    public async Task Analyze_NonContiguousFacilityBits_ReportsError() =>
        await AssertIssue(AnalyzeCard("FFCCCCFF"), IssueLevel.Error, "Facility code bits must form a single contiguous run");

    [Test]
    public async Task Analyze_InvalidCardMaskCharacter_ReportsError() =>
        await AssertIssue(AnalyzeCard("CCCCZZZZ"), IssueLevel.Error, "invalid character: 'Z'");

    [Test]
    public async Task Analyze_MaskOver65Bits_ReportsError() =>
        await AssertIssue(AnalyzeCard(new string('C', 66)), IssueLevel.Error, "exceeds 64 bits");

    [Test]
    public async Task Analyze_ParityWithPBitsButNoIndicator_ReportsError() =>
        await AssertIssue(
            CardFormatAnalyzer.Analyze([ValidCardMask, "PPPPXXXXXXXXXXXXXXXXXXXXXX", "", ""]),
            IssueLevel.Error, "Parity 1 has P bits but no E or O bit, so nothing is ever written.");

    [Test]
    public async Task Analyze_ParityBitOverCardBit_ReportsError() =>
        await AssertIssue(
            CardFormatAnalyzer.Analyze(["CCCCXXXX", "PPPEXXXX", "", ""]),
            IssueLevel.Error, "writes its parity bit over a card or facility bit");

    [Test]
    public async Task Analyze_TestCardNumberOverflows_ReportsError()
    {
        var analysis = CardFormatAnalyzer.Analyze(Valid(), 65536);

        await AssertIssue(analysis, IssueLevel.Error,
            "Test card number exceeds 65535, the maximum for 16 card bits. Overflow bits leak into neighbouring fields.");
    }

    [Test]
    public async Task Analyze_TestFacilityCodeOverflows_ReportsError() =>
        await AssertIssue(CardFormatAnalyzer.Analyze(Valid(), facilityIn: 256), IssueLevel.Error,
            "Test facility code exceeds 255, the maximum for 8 facility bits.");

    // -------------------------------------------------------------------------
    // Warnings
    // -------------------------------------------------------------------------

    [Test]
    public async Task Analyze_MultipleIndicators_ReportsWarning() =>
        await AssertIssue(
            CardFormatAnalyzer.Analyze(["XXXXXXXX", "XXEOXPPP", "", ""]),
            IssueLevel.Warn, "Parity 1 has 2 parity bits. Only the rightmost is written; the last one seen sets even or odd.");

    [Test]
    public async Task Analyze_ParityWithNoPBits_ReportsWarning() =>
        await AssertIssue(
            CardFormatAnalyzer.Analyze(["CCCCXXXX", "XXXXXXXO", "", ""]),
            IssueLevel.Warn, "Parity 1 has a parity bit but no P bits to check.");

    [Test]
    public async Task Analyze_TwoRulesTargetSameBit_ReportsWarning() =>
        await AssertIssue(
            CardFormatAnalyzer.Analyze(["CCCCXXXX", "PPPPXXXO", "PPPPXXXO", ""]),
            IssueLevel.Warn, "Parity 1 and parity 2 target the same bit.");

    [Test]
    public async Task Analyze_InvalidHexInput_ReportsWarning() =>
        await AssertIssue(CardFormatAnalyzer.Analyze(Valid(), hexIn: "12G4"), IssueLevel.Warn,
            "Decode input is not valid hex.");

    [Test]
    public async Task Analyze_ValidHexInput_ReportsNoHexWarning()
    {
        var analysis = CardFormatAnalyzer.Analyze(Valid(), hexIn: "00000000ABCD");

        await Assert.That(analysis.Issues.Any(p => p.Message.Contains("not valid hex"))).IsFalse();
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    public async Task Analyze_BlankHexInput_ReportsNoHexWarning(string? hexIn)
    {
        // An empty decode box is nothing to check, not a problem — warning on it would put the
        // whole page at "check" before the user has typed anything.
        var analysis = CardFormatAnalyzer.Analyze(Valid(), hexIn: hexIn);

        await Assert.That(analysis.Verdict).IsEqualTo(IssueLevel.Ok);
    }

    [Test]
    public async Task Analyze_AllXParityMask_IsNotCountedAsARule()
    {
        // The editor pads unused parity rows to all-X and persists them as null, so an all-X mask
        // must not count — otherwise the stat line and the encoder disagree.
        var analysis = CardFormatAnalyzer.Analyze(
            [ValidCardMask, ValidParity1, ValidParity2, new string('X', 26)]);

        await Assert.That(analysis.ParityRuleCount).IsEqualTo(2);
        await Assert.That(analysis.Verdict).IsEqualTo(IssueLevel.Ok);
    }

    [Test]
    public async Task Analyze_ParityRuleCount_MatchesTheEncoder()
    {
        var masks = new[] { ValidCardMask, ValidParity1, ValidParity2, new string('X', 26) };
        var analysis = CardFormatAnalyzer.Analyze(masks);

        // Blank rows are persisted as null, which is what the encoder is handed.
        var encoder = new CardFormatEncoder(masks[0], masks[1], masks[2]);

        await Assert.That(analysis.ParityRuleCount).IsEqualTo(encoder.ParityRuleCount);
    }

    // -------------------------------------------------------------------------
    // Notices
    // -------------------------------------------------------------------------

    [Test]
    public async Task Analyze_SentinelLengthMask_ReportsNotice() =>
        await AssertIssue(AnalyzeCard("1" + new string('C', 64)), IssueLevel.Notice,
            "65-bit mask: hex output is prefixed with the 01 sentinel instead of the usual 12 digits.");

    [Test]
    public async Task Analyze_NoFacilityBits_ReportsNotice() =>
        await AssertIssue(AnalyzeCard("CCCCCCCC"), IssueLevel.Notice,
            "No facility code bits. Every credential decodes with a facility code of 0.");

    [Test]
    public async Task Analyze_UnprotectedPayloadBits_ReportsNotice() =>
        // Only bits 0-3 are covered, leaving the top of the card field unprotected.
        await AssertIssue(
            CardFormatAnalyzer.Analyze(["CCCCCCCX", "PPPPXXXO", "", ""]),
            IssueLevel.Notice, "payload bits sit outside every parity range and are unprotected.");

    [Test]
    public async Task Analyze_NoParityRules_ReportsNoUnprotectedNotice()
    {
        // With no parity rules at all there is nothing to be unprotected relative to.
        var analysis = AnalyzeCard("FFFFCCCC");

        await Assert.That(analysis.Issues.Any(p => p.Message.Contains("unprotected"))).IsFalse();
    }

    // -------------------------------------------------------------------------
    // Verdict is the worst level present
    // -------------------------------------------------------------------------

    [Test]
    public async Task Verdict_ErrorOutranksWarningAndNotice()
    {
        var analysis = CardFormatAnalyzer.Analyze(["CCCCXXXX", "PPPEXXXX", "", ""]);

        await Assert.That(analysis.Verdict).IsEqualTo(IssueLevel.Error);
        await Assert.That(analysis.IsValid).IsFalse();
    }

    [Test]
    public async Task Verdict_WarningOutranksNotice()
    {
        var analysis = CardFormatAnalyzer.Analyze(["CCCCXXXX", "XXXXXXXO", "", ""]);

        await Assert.That(analysis.Verdict).IsEqualTo(IssueLevel.Warn);
        await Assert.That(analysis.IsValid).IsTrue();
    }

    // -------------------------------------------------------------------------
    // Padding
    // -------------------------------------------------------------------------

    [Test]
    [Arguments("PPO", 6, "XXXPPO")]
    [Arguments("PPO", 3, "PPO")]
    [Arguments("XXXPPO", 3, "PPO")]
    [Arguments(null, 4, "XXXX")]
    [Arguments("", 4, "XXXX")]
    public async Task PadMask_RightAlignsToBitCount(string? mask, int bits, string expected) =>
        await Assert.That(CardFormatAnalyzer.PadMask(mask, bits)).IsEqualTo(expected);

    // -------------------------------------------------------------------------
    // Drift guard: the editor must never call a format valid that cannot be built
    // -------------------------------------------------------------------------

    [Test]
    [Arguments("CCXXCCXX", "", "")]
    [Arguments("FFCCCCFF", "", "")]
    [Arguments("CCCCZZZZ", "", "")]
    [Arguments("CCCCXXXX", "PPPZXXXX", "")]
    [Arguments("CCCCXXXX", "PPPEXXXX", "")]
    [Arguments("FFFFCCCC", "EXXXPPPP", "")]
    [Arguments("XFFFFFFFFCCCCCCCCCCCCCCCCX", "EPPPPPPPPPPPPXXXXXXXXXXXXX", "XXXXXXXXXXXXXPPPPPPPPPPPPO")]
    [Arguments("XXXXXXXX", "", "")]
    [Arguments("1PPFFFFFFFFFFFFCCCCCCCCCCCCCCCCCCCCP", "XEPPXPPXPPXPPXPPXPPXPPXPPXPPXPPXPPX", "PPXPPXPPXPPXPPXPPXPPXPPXPPXPPXPPXO")]
    [Arguments("CCCCXXXX", "PPPPXXXO", "PPPPXXXO")]
    public async Task Analyze_WhenNoErrorReported_EncoderAlwaysBuilds(string cardMask, string parity1, string parity2)
    {
        var analysis = CardFormatAnalyzer.Analyze([cardMask, parity1, parity2, ""]);

        if (analysis.Issues.Any(p => p.Level == IssueLevel.Error))
            return;

        // No error means the editor shows this as saveable, so it had better construct.
        await Assert.That(() => new CardFormatEncoder(cardMask, parity1, parity2)).ThrowsNothing();
    }

    [Test]
    [Arguments("CCXXCCXX", "", "")]
    [Arguments("FFCCCCFF", "", "")]
    [Arguments("CCCCZZZZ", "", "")]
    [Arguments("CCCCXXXX", "PPPZXXXX", "")]
    [Arguments("CCCCXXXX", "PPPEXXXX", "")]
    [Arguments("FFFFCCCC", "EXXXPPPP", "")]
    public async Task Analyze_WhenEncoderThrows_AlwaysReportsError(string cardMask, string parity1, string parity2)
    {
        var analysis = CardFormatAnalyzer.Analyze([cardMask, parity1, parity2, ""]);

        await Assert.That(() => new CardFormatEncoder(cardMask, parity1, parity2)).Throws<ArgumentException>();
        await Assert.That(analysis.Issues.Any(p => p.Level == IssueLevel.Error)).IsTrue();
    }

    private static async Task AssertIssue(FormatAnalysis analysis, IssueLevel level, string messageContains)
    {
        var match = analysis.Issues.FirstOrDefault(p => p.Message.Contains(messageContains));

        await Assert.That(match).IsNotNull();
        await Assert.That(match!.Level).IsEqualTo(level);
    }
}
