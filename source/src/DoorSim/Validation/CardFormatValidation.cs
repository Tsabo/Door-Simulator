namespace DoorSim.Validation;

/// <summary>
/// Domain validation rules for <see cref="CustomCardFormat" /> definitions and credentials sent
/// against them.
/// </summary>
/// <remarks>
/// Mask structure is graded by <see cref="CardFormatAnalyzer" />, which is the same rule set the
/// editor UI shows live — so a format the editor calls valid is a format this accepts. The
/// encoder is then constructed as a backstop, since it deliberately rejects a few shapes it
/// cannot encode at all (a non-contiguous field, an unknown mask character).
/// </remarks>
public static class CardFormatValidation
{
    public const int MaxNameLength = 100;

    /// <summary>
    /// Validates a <see cref="CustomCardFormat" />'s name and mask structure — does not check any
    /// particular credential against it (see <see cref="ValidateCredential" />).
    /// </summary>
    public static string? ValidateDefinition(CustomCardFormat format)
    {
        if (string.IsNullOrWhiteSpace(format.Name))
            return "Format name cannot be empty.";

        if (format.Name.Length > MaxNameLength)
            return $"Format name cannot exceed {MaxNameLength} characters.";

        if (string.IsNullOrEmpty(format.CardMask))
            return "Card mask cannot be empty.";

        var analysis = CardFormatAnalyzer.Analyze(format);
        var firstError = analysis.Issues.FirstOrDefault(p => p.Level == IssueLevel.Error);

        if (firstError is not null)
            return firstError.Message;

        try
        {
            _ = new CardFormatEncoder(format.CardMask, format.Parity1Mask, format.Parity2Mask, format.Parity3Mask);

            return null;
        }
        catch (ArgumentException ex)
        {
            return ex.Message;
        }
    }

    /// <summary>
    /// Validates a card number/facility code pair against a specific format's field widths.
    /// </summary>
    public static string? ValidateCredential(uint cardNumber, ushort facilityCode, CustomCardFormat format)
    {
        try
        {
            var encoder = new CardFormatEncoder(format.CardMask, format.Parity1Mask, format.Parity2Mask, format.Parity3Mask);
            encoder.Encode(cardNumber, facilityCode);

            return null;
        }
        catch (ArgumentException ex)
        {
            return ex.Message;
        }
    }
}
