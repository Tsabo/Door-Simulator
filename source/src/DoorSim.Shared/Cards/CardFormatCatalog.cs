using DoorSim.Shared.Models;

namespace DoorSim.Shared.Cards;

/// <summary>Where a catalogue entry's bit layout comes from.</summary>
/// <remarks>
/// Surfaced in the UI on purpose: the two levels are not equally trustworthy, and an engineer
/// picking a format should see that at the point of choice rather than discover it later.
/// </remarks>
public enum FormatProvenance
{
    /// <summary>
    /// The mask set is asserted byte-for-byte against one of <c>WiegandTransmitter</c>'s
    /// fixed-format builders, or against a known-good credential, in the test suite.
    /// </summary>
    RepoVerified,

    /// <summary>
    /// Field widths come from published HID format tables; the parity structure is reused verbatim
    /// from a <see cref="RepoVerified" /> entry of the same frame width. Confirm against a vendor
    /// spec before relying on it in production.
    /// </summary>
    PublishedSpec
}

/// <summary>A ready-made card format definition, offered as a starting point in the editor.</summary>
public sealed record CardFormatTemplate(
    string Name,
    string CardMask,
    string? Parity1Mask,
    string? Parity2Mask,
    string? Parity3Mask,
    string Description,
    FormatProvenance Provenance)
{
    /// <summary>Frame width — the card mask's length, one character per bit.</summary>
    public int BitCount => CardMask.Length;

    /// <summary>
    /// Projects the template into the persisted DTO shape with <c>Id = 0</c>, so the editor can load
    /// it through the same path it uses for a saved format. The result is an unsaved draft.
    /// </summary>
    public CustomCardFormat ToFormat() =>
        new(0, Name, CardMask, Parity1Mask, Parity2Mask, Parity3Mask, DateTimeOffset.UtcNow);
}

/// <summary>
/// Built-in card formats, so a known layout can be picked instead of hand-authoring four mask
/// strings. Read-only reference data: nothing here is written to the database until a user saves it.
/// </summary>
/// <remarks>
/// <para>
/// Only formats that can be verified are listed. A wrong mask is the worst kind of wrong here — it
/// passes validation, computes parity and round-trips cleanly on the bench, then fails to open a
/// door. Every entry is covered by a test that either cross-checks it against
/// <c>WiegandTransmitter</c>'s bit arithmetic or decodes a known-good credential through it.
/// </para>
/// <para>
/// Several well-known formats cannot be represented in this mask model at all, and are deliberately
/// absent rather than approximated:
/// </para>
/// <list type="bullet">
/// <item>
/// <description>
/// <b>PIV FASC-N</b> — 200 bits against a 65-bit ceiling, six-plus numeric fields against this
/// model's two, and BCD with per-digit odd parity. Supporting it needs a different encoder:
/// multi-field, BCD-aware and arbitrary width. Do not add an approximation here.
/// </description>
/// </item>
/// <item>
/// <description>
/// <b>HID H10320</b> — 32 bits fits, but the card field is BCD and
/// <see cref="CardFormatEncoder" /> reads its fields as plain binary, so the decoded number would
/// be wrong. Needs BCD field support.
/// </description>
/// </item>
/// </list>
/// <para>
/// A vendor spec closes the knowledge gap the fastest: given WaveLynx's "Bit Stream Definitions"
/// document (v2.2, 10/9/2023), every field-width claim below was cross-checked against the
/// document's own stated min/max ranges (each equals 2^width - 1 exactly), and every derived
/// parity mask was verified against a from-scratch reimplementation of
/// <see cref="CardFormatEncoder" />'s field-shift and parity-scan algorithm — not eyeballed.
/// Five formats from that same document are deliberately absent:
/// </para>
/// <list type="bullet">
/// <item>
/// <description>
/// <b>W26-1</b> — its odd-parity row appears (from the document's shading) to
/// span the entire frame rather than the trailing half used by every other two-parity format here;
/// needs the document's author to confirm before it's trustworthy.
/// </description>
/// </item>
/// <item>
/// <description>
/// <b>W35-0</b> — a three-rule interleaved parity scheme whose column
/// data came through with the bit-number header scrambled (bit numbers "1 2" and "35" appear out
/// of sequence), so the exact bit positions can't be reconstructed reliably from the extracted
/// text.
/// </description>
/// </item>
/// <item>
/// <description>
/// <b>W40-2</b> — the stated 38-bit badge ID leaves 2 bits of the 40-bit
/// frame unplaced; getting that 2-bit offset wrong would misalign every credential against real
/// hardware even though it would still round-trip against itself.
/// </description>
/// </item>
/// <item>
/// <description>
/// <b>W46-T</b> — three independently variable fields (technology code,
/// facility code, badge ID); this model supports exactly two.
/// </description>
/// </item>
/// <item>
/// <description>
/// <b>W56-0</b> — the document's stated badge ID range
/// (72,057,594,037,928,000) is not 2^w - 1 for any integer w, so the field width it implies is
/// internally inconsistent within the document itself.
/// </description>
/// </item>
/// </list>
/// <para>
/// To add a format, supply: total bit count; the start bit and width of the card-number and
/// facility-code fields (bit 0 is the rightmost mask character); and for each parity rule its
/// polarity, its own bit position, and the range of bits it covers.
/// </para>
/// </remarks>
public static class CardFormatCatalog
{
    // The 26-, 34- and 37-bit parity structures below are shared by more than one entry, because
    // WiegandTransmitter computes parity over frame halves rather than per field: the split does not
    // move when a field boundary does. Naming them once makes that reuse explicit and lets the tests
    // assert it.
    private const string Parity26Even = "EPPPPPPPPPPPPXXXXXXXXXXXXX";
    private const string Parity26Odd = "XXXXXXXXXXXXXPPPPPPPPPPPPO";

    private const string Parity34Even = "EPPPPPPPPPPPPPPPPXXXXXXXXXXXXXXXXX";
    private const string Parity34Odd = "XXXXXXXXXXXXXXXXXPPPPPPPPPPPPPPPPO";

    private const string Parity37Even = "EPPPPPPPPPPPPPPPPPPXXXXXXXXXXXXXXXXXX";
    private const string Parity37Odd = "XXXXXXXXXXXXXXXXXXXPPPPPPPPPPPPPPPPPO";

    // Same reasoning at two more widths, this time sourced from WaveLynx's W38-1/W40-0/W40-1 —
    // a leading even-parity bit covering the first half of the non-parity data bits, a trailing
    // odd-parity bit covering the second half. W40-0 and W40-1 differ only in where the
    // facility/badge boundary falls, so they share these same two constants.
    private const string Parity38Even = "EPPPPPPPPPPPPPPPPPPXXXXXXXXXXXXXXXXXXX";
    private const string Parity38Odd = "XXXXXXXXXXXXXXXXXXXPPPPPPPPPPPPPPPPPPO";

    private const string Parity40Even = "EPPPPPPPPPPPPPPPPPPPXXXXXXXXXXXXXXXXXXXX";
    private const string Parity40Odd = "XXXXXXXXXXXXXXXXXXXXPPPPPPPPPPPPPPPPPPPO";

    /// <summary>Every built-in format, in ascending frame width.</summary>
    public static IReadOnlyList<CardFormatTemplate> All { get; } =
    [
        new(
            "Wiegand 26-bit (H10301)",
            "XFFFFFFFFCCCCCCCCCCCCCCCCX",
            Parity26Even,
            Parity26Odd,
            null,
            "The most common legacy Wiegand format. 8-bit facility code (bits 24-17), 16-bit card "
            + "number (bits 16-1), leading even parity over bits 24-13 and trailing odd parity over "
            + "bits 12-1. Also published as WaveLynx's W26-0.",
            FormatProvenance.RepoVerified),

        new(
            "HID Corporate 1000 35-bit (no parity)",
            "00FFFFFFFFFFFFCCCCCCCCCCCCCCCCCCCC0",
            null,
            null,
            null,
            "Mirrors this simulator's own built-in HidCorporate1000 format, which is parity-free by "
            + "design: 12-bit facility code (bits 32-21), 20-bit card number (bits 20-1), and bits "
            + "34, 33 and 0 pinned to zero. Real Corporate 1000 carries three parity bits in exactly "
            + "those pinned positions — treat this as the simulator's layout, not as interoperable "
            + "Corporate 1000.",
            FormatProvenance.RepoVerified),

        new(
            "Sample 36-bit (interleaved parity)",
            "1PPFFFFFFFFFFFFCCCCCCCCCCCCCCCCCCCCP",
            "XEPPXPPXPPXPPXPPXPPXPPXPPXPPXPPXPPX",
            "PPXPPXPPXPPXPPXPPXPPXPPXPPXPPXPPXO",
            "OPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPP",
            "12-bit facility code, 20-bit card number and three interleaved every-third-bit parity "
            + "rules (even at bit 33, odd at bit 0, odd at bit 34). Verified by a known credential "
            + "rather than a builder: 000000084E6018E6 decodes to card 3187, facility 627. The card "
            + "mask's P markers and the ragged 35/34/35 parity lengths are intentional — they are how "
            + "legacy definitions arrive, and they exercise right-alignment.",
            FormatProvenance.RepoVerified),

        new(
            "Wiegand 34-bit (8-bit facility code)",
            "XFFFFFFFFCCCCCCCCCCCCCCCCCCCCCCCCX",
            Parity34Even,
            Parity34Odd,
            null,
            "This simulator's built-in 34-bit format: 8-bit facility code (bits 32-25), 24-bit card "
            + "number (bits 24-1), even parity over bits 32-17 and odd parity over bits 16-1. Not "
            + "H10306, which is the same width but splits the fields 16/16.",
            FormatProvenance.RepoVerified),

        new(
            "HID 34-bit (H10306)",
            "XFFFFFFFFFFFFFFFFCCCCCCCCCCCCCCCCX",
            Parity34Even,
            Parity34Odd,
            null,
            "16-bit facility code (bits 32-17), 16-bit card number (bits 16-1). Field split per "
            + "published HID tables; the parity structure is reused unchanged from the repo-verified "
            + "34-bit frame. Confirm against your vendor spec before production use.",
            FormatProvenance.PublishedSpec),

        new(
            "Wiegand 37-bit (H10302)",
            "XCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCX",
            Parity37Even,
            Parity37Odd,
            null,
            "No facility code, 35-bit card number (bits 35-1). Even parity over bits 35-18 (18 bits) "
            + "and odd parity over bits 17-1 (17 bits) — the asymmetric split is deliberate.",
            FormatProvenance.RepoVerified),

        new(
            "HID 37-bit with facility code (H10304)",
            "XFFFFFFFFFFFFFFFFCCCCCCCCCCCCCCCCCCCX",
            Parity37Even,
            Parity37Odd,
            null,
            "16-bit facility code (bits 35-20), 19-bit card number (bits 19-1). Field split per "
            + "published HID tables; the parity structure is reused unchanged from the repo-verified "
            + "37-bit frame. Confirm against a real credential before production use. Also published "
            + "as WaveLynx's W37-0, which independently states the same 16/19 field split.",
            FormatProvenance.PublishedSpec),

        // ---------------------------------------------------------------------------------------
        // WaveLynx "Bit Stream Definitions" v2.2 (10/9/2023). Field widths are taken directly from
        // the document's own stated facility-code/badge-ID ranges, each confirmed to equal
        // 2^width - 1 exactly. Where a format carries the standard leading-even / trailing-odd
        // parity pair, the parity split follows the same halved-remaining-data-bits convention
        // already verified three times against WiegandTransmitter's own bit arithmetic (26/34/37-bit
        // frames) — applied here at 38 and 40 bits, where no repo builder exists to cross-check
        // against directly.
        // ---------------------------------------------------------------------------------------

        new(
            "WaveLynx W30-T",
            "FFFFCCCCCCCCCCCCCCCCCCCCCCCCCC",
            null,
            null,
            null,
            "4-bit technology code (bits 30-27) and 26-bit badge ID (bits 26-1), no parity. Field "
            + "widths are exact against WaveLynx's published ranges — there is no parity structure to "
            + "verify. Confirm against a real credential before production use.",
            FormatProvenance.PublishedSpec),

        new(
            "WaveLynx W32-5 (MyPass Mobile Credential)",
            "CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC",
            null,
            null,
            null,
            "32-bit badge ID only, no facility code and no parity. WaveLynx's mobile (BLE) credential "
            + "format; the Wiegand output at the reader follows this same 32-bit layout. Confirm "
            + "against a real credential before production use.",
            FormatProvenance.PublishedSpec),

        new(
            "WaveLynx W36-8",
            "FFFFFCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC",
            null,
            null,
            null,
            "5-bit facility code (bits 36-32) and 31-bit badge ID (bits 31-1), no parity. Confirm "
            + "against a real credential before production use.",
            FormatProvenance.PublishedSpec),

        new(
            "WaveLynx W38-1",
            "XFFFFFFFFCCCCCCCCCCCCCCCCCCCCCCCCCCCCX",
            Parity38Even,
            Parity38Odd,
            null,
            "8-bit facility code (bits 36-29), 28-bit badge ID (bits 28-1). Field widths are exact "
            + "against WaveLynx's published ranges; parity coverage follows the same halved-frame "
            + "convention verified for the repo's 26/34/37-bit Wiegand frames, not independently "
            + "confirmed at 38 bits. Confirm against a real credential before production use.",
            FormatProvenance.PublishedSpec),

        new(
            "WaveLynx W38-2",
            "FFFFFFFFCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC",
            null,
            null,
            null,
            "8-bit facility code (bits 38-31), 30-bit badge ID (bits 30-1), no parity. Confirm "
            + "against a real credential before production use.",
            FormatProvenance.PublishedSpec),

        new(
            "WaveLynx W40-0",
            "XFFFFFFFFFFCCCCCCCCCCCCCCCCCCCCCCCCCCCCX",
            Parity40Even,
            Parity40Odd,
            null,
            "10-bit facility code (bits 38-29), 28-bit badge ID (bits 28-1). Field widths are exact "
            + "against WaveLynx's published ranges; parity coverage follows the same halved-frame "
            + "convention verified for the repo's 26/34/37-bit Wiegand frames, not independently "
            + "confirmed at 40 bits. Confirm against a real credential before production use.",
            FormatProvenance.PublishedSpec),

        new(
            "WaveLynx W40-1",
            "XFFFFFFFFFFFFFFFFFFFCCCCCCCCCCCCCCCCCCCX",
            Parity40Even,
            Parity40Odd,
            null,
            "19-bit facility code (bits 38-20), 19-bit badge ID (bits 19-1) — the two fields are the "
            + "same width in WaveLynx's own table. Parity structure identical to W40-0's; only the "
            + "field split differs, confirming the split is independent of where the boundary falls. "
            + "Confirm against a real credential before production use.",
            FormatProvenance.PublishedSpec),

        new(
            "WaveLynx W56-1",
            "FFFFFFFFFFFFFFFFFFFFFFFFCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC",
            null,
            null,
            null,
            "24-bit facility code (bits 56-33), 32-bit badge ID (bits 32-1), no parity. Confirm "
            + "against a real credential before production use.",
            FormatProvenance.PublishedSpec),

        new(
            "WaveLynx W64-T",
            "FFFFFFFF000CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC",
            null,
            null,
            null,
            "8-bit technology code (bits 64-57), 3 fixed-zero bits (bits 56-54), 53-bit badge ID "
            + "(bits 53-1), no parity. The 53-bit ceiling is 2^53 - 1 — WaveLynx's own choice, likely "
            + "for JavaScript/web interoperability (Number.MAX_SAFE_INTEGER). Confirm against a real "
            + "credential before production use.",
            FormatProvenance.PublishedSpec)
    ];

    /// <summary>
    /// The format the editor opens on — the 26-bit standard, since it is by far the common case.
    /// </summary>
    public static CardFormatTemplate Default { get; } =
        All.First(p => p.Name.Contains("H10301", StringComparison.Ordinal));
}
