namespace DoorSim.Validation;

/// <summary>
/// Domain validation rules for card credentials and library entries.
/// </summary>
public static class CardValidation
{
    public const int MaxLabelLength = 100;

    /// <summary>
    /// Validates a <see cref="CardEntry" /> for persistence or simulation.
    /// </summary>
    public static string? Validate(CardEntry card)
    {
        if (string.IsNullOrWhiteSpace(card.Label))
            return "Card label cannot be empty.";

        if (card.Label.Length > MaxLabelLength)
            return $"Card label cannot exceed {MaxLabelLength} characters.";

        return ValidateCredential(card.CardNumber, card.FacilityCode, card.Format);
    }

    /// <summary>
    /// Validates raw credential fields against the bit-width limitations of the specified <see cref="WiegandFormat" />.
    /// </summary>
    public static string? ValidateCredential(uint cardNumber, ushort facilityCode, WiegandFormat format)
    {
        if (!Enum.IsDefined(format))
            return $"Invalid Wiegand format: {format}.";

        return format switch
        {
            WiegandFormat.Wiegand26 => ValidateWiegand26(facilityCode, cardNumber),
            WiegandFormat.Wiegand34 => ValidateWiegand34(facilityCode, cardNumber),
            WiegandFormat.Wiegand37 => ValidateWiegand37(cardNumber),
            WiegandFormat.HidCorporate1000 => ValidateHidCorporate1000(facilityCode, cardNumber),
            var _ => $"Unsupported Wiegand format: {format}.",
        };
    }

    private static string? ValidateWiegand26(ushort facilityCode, uint cardNumber)
    {
        if (facilityCode > 255)
            return $"Wiegand 26 facility code must be between 0 and 255 (8 bits). Provided: {facilityCode}.";

        if (cardNumber > 65_535)
            return $"Wiegand 26 card number must be between 0 and 65,535 (16 bits). Provided: {cardNumber}.";

        return null;
    }

    private static string? ValidateWiegand34(ushort facilityCode, uint cardNumber)
    {
        if (facilityCode > 255)
            return $"Wiegand 34 facility code must be between 0 and 255 (8 bits). Provided: {facilityCode}.";

        if (cardNumber > 16_777_215)
            return $"Wiegand 34 card number must be between 0 and 16,777,215 (24 bits). Provided: {cardNumber}.";

        return null;
    }

    private static string? ValidateWiegand37(uint cardNumber)
    {
        _ = cardNumber;
        // 35 data bits total; uint maxValue (4,294,967,295) is 32 bits, safely within 35 bits.
        return null;
    }

    private static string? ValidateHidCorporate1000(ushort facilityCode, uint cardNumber)
    {
        if (facilityCode > 4_095)
            return $"HID Corporate 1000 facility code must be between 0 and 4,095 (12 bits). Provided: {facilityCode}.";

        if (cardNumber > 1_048_575)
            return $"HID Corporate 1000 card number must be between 0 and 1,048,575 (20 bits). Provided: {cardNumber}.";

        return null;
    }
}
