using DoorSim.Hardware;
using DoorSim.Shared.Cards;
using DoorSim.Validation;

namespace DoorSim.Tests;

/// <summary>
/// Guards the built-in format catalogue. The per-entry tests prove no entry can be unsavable or
/// fail to round-trip; the oracle tests prove the masks agree with <see cref="WiegandTransmitter" />'s
/// bit arithmetic, which is what stops the catalogue drifting from the simulator's own behaviour.
/// </summary>
public class CardFormatCatalogTests
{
    public static IEnumerable<Func<CardFormatTemplate>> AllEntries() =>
        CardFormatCatalog.All.Select<CardFormatTemplate, Func<CardFormatTemplate>>(entry => () => entry);

    // -------------------------------------------------------------------------
    // Every entry, whatever its provenance
    // -------------------------------------------------------------------------

    [Test]
    [MethodDataSource(nameof(AllEntries))]
    public async Task Entry_ReportsNoValidationError(CardFormatTemplate entry)
    {
        // No catalogue entry may be one the save path would reject.
        var analysis = CardFormatAnalyzer.Analyze(entry.ToFormat());

        await Assert.That(analysis.Issues.Any(p => p.Level == IssueLevel.Error)).IsFalse();
        await Assert.That(CardFormatValidation.ValidateDefinition(entry.ToFormat())).IsNull();
    }

    [Test]
    [MethodDataSource(nameof(AllEntries))]
    public async Task Entry_BuildsAnEncoder(CardFormatTemplate entry) =>
        await Assert.That(() => Encoder(entry)).ThrowsNothing();

    [Test]
    [MethodDataSource(nameof(AllEntries))]
    public async Task Entry_BitCountMatchesCardMaskLength(CardFormatTemplate entry)
    {
        var encoder = Encoder(entry);

        await Assert.That(entry.BitCount).IsEqualTo(entry.CardMask.Length);
        await Assert.That(encoder.TotalBits).IsEqualTo(entry.BitCount);
    }

    [Test]
    [MethodDataSource(nameof(AllEntries))]
    public async Task Entry_RoundTripsARepresentativeCredential(CardFormatTemplate entry)
    {
        var encoder = Encoder(entry);

        // Largest value each field can actually hold, so the test exercises the full width.
        var cardNumber = CardFormatEncoder.FieldMaximum(encoder.CardBitCount);
        var facilityCode = CardFormatEncoder.FieldMaximum(encoder.FacilityBitCount);

        var frame = encoder.Encode(cardNumber, facilityCode);
        var (decodedCard, decodedFacility, parityValid) = encoder.DecodeWide(frame);

        await Assert.That(decodedCard).IsEqualTo(cardNumber);
        await Assert.That(decodedFacility).IsEqualTo(facilityCode);
        await Assert.That(parityValid).IsTrue();
    }

    [Test]
    [MethodDataSource(nameof(AllEntries))]
    public async Task Entry_RoundTripsZero(CardFormatTemplate entry)
    {
        var encoder = Encoder(entry);
        var (card, facility, parityValid) = encoder.DecodeWide(encoder.Encode(0, 0));

        await Assert.That(card).IsEqualTo(0UL);
        await Assert.That(facility).IsEqualTo(0UL);
        await Assert.That(parityValid).IsTrue();
    }

    [Test]
    [MethodDataSource(nameof(AllEntries))]
    public async Task Entry_HasNameAndDescription(CardFormatTemplate entry)
    {
        await Assert.That(string.IsNullOrWhiteSpace(entry.Name)).IsFalse();
        await Assert.That(entry.Name.Length).IsLessThanOrEqualTo(CardFormatValidation.MaxNameLength);
        await Assert.That(string.IsNullOrWhiteSpace(entry.Description)).IsFalse();
    }

    [Test]
    [MethodDataSource(nameof(AllEntries))]
    public async Task Entry_UnverifiedEntryCarriesACaveat(CardFormatTemplate entry)
    {
        if (entry.Provenance != FormatProvenance.PublishedSpec)
            return;

        // The provenance is only useful if the description says what to do about it.
        await Assert.That(entry.Description).Contains("Confirm against");
    }

    // -------------------------------------------------------------------------
    // Catalogue integrity
    // -------------------------------------------------------------------------

    [Test]
    public async Task Catalogue_NamesAreUnique()
    {
        var names = CardFormatCatalog.All.Select(p => p.Name).ToList();

        await Assert.That(names.Distinct(StringComparer.OrdinalIgnoreCase).Count()).IsEqualTo(names.Count);
    }

    [Test]
    public async Task Catalogue_NoTwoEntriesShareAnIdenticalMaskSet()
    {
        // Catches a copy-paste that changed the name but not the masks.
        var maskSets = CardFormatCatalog.All
            .Select(p => string.Join('|', p.CardMask, p.Parity1Mask, p.Parity2Mask, p.Parity3Mask))
            .ToList();

        await Assert.That(maskSets.Distinct().Count()).IsEqualTo(maskSets.Count);
    }

    [Test]
    public async Task Catalogue_DefaultIsThe26BitStandard()
    {
        await Assert.That(CardFormatCatalog.Default.BitCount).IsEqualTo(26);
        await Assert.That(CardFormatCatalog.Default.Name).Contains("H10301");
        await Assert.That(CardFormatCatalog.All).Contains(CardFormatCatalog.Default);
    }

    [Test]
    public async Task Catalogue_ToFormatProducesAnUnsavedDraft()
    {
        var dto = CardFormatCatalog.Default.ToFormat();

        await Assert.That(dto.Id).IsEqualTo(0);
        await Assert.That(dto.Name).IsEqualTo(CardFormatCatalog.Default.Name);
        await Assert.That(dto.CardMask).IsEqualTo(CardFormatCatalog.Default.CardMask);
    }

    [Test]
    public async Task Catalogue_NoEntryExceedsTheFrameCeiling()
    {
        foreach (var entry in CardFormatCatalog.All)
            await Assert.That(entry.BitCount).IsLessThanOrEqualTo(CardFormatEncoder.SentinelTotalBits);
    }

    // -------------------------------------------------------------------------
    // Oracle tests — the catalogue must agree with WiegandTransmitter's arithmetic
    // -------------------------------------------------------------------------

    [Test]
    [Arguments((ushort)0, 0u)]
    [Arguments((ushort)1, 1u)]
    [Arguments((ushort)100, 12345u)]
    [Arguments((ushort)255, 65535u)]
    public async Task Wiegand26Entry_MatchesBuildWiegand26(ushort fc, uint cn)
    {
        var frame = Encoder(Entry("Wiegand 26-bit (H10301)")).Encode(cn, fc);

        await Assert.That(frame).IsEqualTo(WiegandTransmitter.BuildWiegand26(fc, (ushort)cn));
    }

    [Test]
    [Arguments((ushort)0, 0u)]
    [Arguments((ushort)1, 1u)]
    [Arguments((ushort)123, 999999u)]
    [Arguments((ushort)255, 16777215u)]
    public async Task Wiegand34Entry_MatchesBuildWiegand34(ushort fc, uint cn)
    {
        var frame = Encoder(Entry("Wiegand 34-bit (8-bit facility code)")).Encode(cn, fc);

        await Assert.That(frame).IsEqualTo(WiegandTransmitter.BuildWiegand34(fc, cn));
    }

    [Test]
    [Arguments(0u)]
    [Arguments(1u)]
    [Arguments(12345u)]
    [Arguments(4294967295u)]
    public async Task Wiegand37Entry_MatchesBuildWiegand37(uint cn)
    {
        var frame = Encoder(Entry("Wiegand 37-bit (H10302)")).Encode(cn, 0);

        await Assert.That(frame).IsEqualTo(WiegandTransmitter.BuildWiegand37(cn));
    }

    [Test]
    [Arguments((ushort)0, 0u)]
    [Arguments((ushort)1, 1u)]
    [Arguments((ushort)4095, 1048575u)]
    [Arguments((ushort)627, 3187u)]
    public async Task Corporate1000Entry_MatchesBuildHidCorporate1000(ushort fc, uint cn)
    {
        var frame = Encoder(Entry("HID Corporate 1000 35-bit (no parity)")).Encode(cn, fc);

        await Assert.That(frame).IsEqualTo(WiegandTransmitter.BuildHidCorporate1000(fc, cn));
    }

    [Test]
    public async Task Sample36Entry_DecodesItsDocumentedCredential()
    {
        // The description quotes this credential, so the description is under test too.
        var entry = Entry("Sample 36-bit (interleaved parity)");
        CardFormatEncoder.TryParseHex("000000084E6018E6", out var bits);

        var (cardNumber, facilityCode, parityValid) = Encoder(entry).Decode(bits);

        await Assert.That(cardNumber).IsEqualTo(3187u);
        await Assert.That(facilityCode).IsEqualTo((ushort)627);
        await Assert.That(parityValid).IsTrue();
        await Assert.That(entry.Description).Contains("000000084E6018E6");
    }

    // -------------------------------------------------------------------------
    // The PublishedSpec claim: only the field split is unverified
    // -------------------------------------------------------------------------

    [Test]
    [Arguments("HID 34-bit (H10306)", "Wiegand 34-bit (8-bit facility code)")]
    [Arguments("HID 37-bit with facility code (H10304)", "Wiegand 37-bit (H10302)")]
    public async Task PublishedSpecEntry_ReusesAVerifiedParityStructure(string publishedName, string verifiedName)
    {
        var published = Entry(publishedName);
        var verified = Entry(verifiedName);

        await Assert.That(published.Provenance).IsEqualTo(FormatProvenance.PublishedSpec);
        await Assert.That(verified.Provenance).IsEqualTo(FormatProvenance.RepoVerified);

        // Same frame width and byte-identical parity masks: the field split is the only difference.
        await Assert.That(published.BitCount).IsEqualTo(verified.BitCount);
        await Assert.That(published.Parity1Mask).IsEqualTo(verified.Parity1Mask);
        await Assert.That(published.Parity2Mask).IsEqualTo(verified.Parity2Mask);
        await Assert.That(published.Parity3Mask).IsEqualTo(verified.Parity3Mask);
        await Assert.That(published.CardMask).IsNotEqualTo(verified.CardMask);
    }

    [Test]
    [Arguments("HID 34-bit (H10306)", 16, 16)]
    [Arguments("HID 37-bit with facility code (H10304)", 16, 19)]
    [Arguments("Wiegand 26-bit (H10301)", 8, 16)]
    [Arguments("Wiegand 34-bit (8-bit facility code)", 8, 24)]
    [Arguments("Wiegand 37-bit (H10302)", 0, 35)]
    [Arguments("HID Corporate 1000 35-bit (no parity)", 12, 20)]
    [Arguments("Sample 36-bit (interleaved parity)", 12, 20)]
    [Arguments("WaveLynx W30-T", 4, 26)]
    [Arguments("WaveLynx W32-5 (MyPass Mobile Credential)", 0, 32)]
    [Arguments("WaveLynx W36-8", 5, 31)]
    [Arguments("WaveLynx W38-1", 8, 28)]
    [Arguments("WaveLynx W38-2", 8, 30)]
    [Arguments("WaveLynx W40-0", 10, 28)]
    [Arguments("WaveLynx W40-1", 19, 19)]
    [Arguments("WaveLynx W56-1", 24, 32)]
    [Arguments("WaveLynx W64-T", 8, 53)]
    public async Task Entry_FieldWidthsAreAsDocumented(string name, int facilityBits, int cardBits)
    {
        var encoder = Encoder(Entry(name));

        await Assert.That(encoder.FacilityBitCount).IsEqualTo(facilityBits);
        await Assert.That(encoder.CardBitCount).IsEqualTo(cardBits);
    }

    // -------------------------------------------------------------------------
    // WaveLynx-sourced entries
    // -------------------------------------------------------------------------

    [Test]
    [Arguments((ushort)0, 0u)]
    [Arguments((ushort)15, 67108863u)]
    [Arguments((ushort)10, 12345u)]
    public async Task W30T_HasNoParityRules(ushort tech, uint badge)
    {
        var encoder = Encoder(Entry("WaveLynx W30-T"));

        await Assert.That(encoder.ParityRuleCount).IsEqualTo(0);

        var (decodedBadge, decodedTech, _) = encoder.Decode(encoder.Encode(badge, tech));

        await Assert.That(decodedBadge).IsEqualTo(badge);
        await Assert.That(decodedTech).IsEqualTo(tech);
    }

    [Test]
    public async Task W325_HasNoFacilityFieldAndNoParity()
    {
        var encoder = Encoder(Entry("WaveLynx W32-5 (MyPass Mobile Credential)"));

        await Assert.That(encoder.FacilityBitCount).IsEqualTo(0);
        await Assert.That(encoder.ParityRuleCount).IsEqualTo(0);
        await Assert.That(encoder.CardBitCount).IsEqualTo(32);
    }

    [Test]
    public async Task W40Pair_ShareIdenticalParityMasksDespiteDifferentFieldSplits()
    {
        // W40-0 (10 FC + 28 badge) and W40-1 (19 FC + 19 badge) are two different vendor-published
        // field splits of the same 40-bit frame. If the parity masks below are byte-identical while
        // the card masks differ, that confirms the derived parity split depends only on total frame
        // width, not on where the facility/badge boundary falls — the same property already proven
        // for the repo-verified 34- and 37-bit frames.
        var w40_0 = Entry("WaveLynx W40-0");
        var w40_1 = Entry("WaveLynx W40-1");

        await Assert.That(w40_0.BitCount).IsEqualTo(w40_1.BitCount);
        await Assert.That(w40_0.Parity1Mask).IsEqualTo(w40_1.Parity1Mask);
        await Assert.That(w40_0.Parity2Mask).IsEqualTo(w40_1.Parity2Mask);
        await Assert.That(w40_0.CardMask).IsNotEqualTo(w40_1.CardMask);
    }

    [Test]
    [Arguments((ushort)0, 0ul)]
    [Arguments((ushort)255, 9007199254740991ul)]
    public async Task W64T_FixedZeroBitsStayClearAtMaxFieldValues(ushort tech, ulong badge)
    {
        var encoder = Encoder(Entry("WaveLynx W64-T"));
        var frame = encoder.Encode(badge, tech);

        // The three fixed-zero bits sit at PDF bit positions 9-11 of a 64-bit frame, i.e. our bit
        // positions 55, 54 and 53 (bit 0 = rightmost). They must never be set regardless of how wide
        // the technology code or badge ID fields are driven.
        foreach (var bitPos in new[] { 55, 54, 53 })
            await Assert.That((frame & 1UL << bitPos) == 0).IsTrue();

        var (decodedBadge, decodedTech, _) = encoder.DecodeWide(frame);

        await Assert.That(decodedBadge).IsEqualTo(badge);
        await Assert.That(decodedTech).IsEqualTo(tech);
    }

    [Test]
    public async Task Catalogue_ContainsSixteenEntries() =>
        // Pins the count so an accidental duplicate or dropped entry is caught immediately, rather
        // than surfacing only as an obscure downstream test failure.
        await Assert.That(CardFormatCatalog.All.Count).IsEqualTo(16);

    private static CardFormatTemplate Entry(string name) =>
        CardFormatCatalog.All.Single(p => p.Name == name);

    private static CardFormatEncoder Encoder(CardFormatTemplate entry) =>
        new(entry.CardMask, entry.Parity1Mask, entry.Parity2Mask, entry.Parity3Mask);
}
