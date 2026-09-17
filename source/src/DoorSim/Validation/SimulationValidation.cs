namespace DoorSim.Validation;

/// <summary>
/// Domain validation rules for simulation requests.
/// </summary>
public static class SimulationValidation
{
    /// <summary>
    /// Validates a library-backed <see cref="DoorEventRequest" />.
    /// </summary>
    public static string? Validate(DoorEventRequest request)
    {
        if (request.ReaderId <= 0)
            return "ReaderId must be greater than zero.";

        if (!Enum.IsDefined(request.EventType))
            return $"Invalid event type: {request.EventType}.";

        if (request.EventType == DoorEventType.EgressCycle)
            return null;

        if (request.CardEntryId is null or <= 0)
            return "CardEntryId is required and must be greater than zero for this event type.";

        return null;
    }

    /// <summary>
    /// Validates a <see cref="RawCardRequest" />.
    /// </summary>
    public static string? Validate(RawCardRequest request)
    {
        if (request.ReaderId <= 0)
            return "ReaderId must be greater than zero.";

        return CardValidation.ValidateCredential(request.CardNumber, request.FacilityCode, request.Format, request.CustomFormatId);
    }

    /// <summary>
    /// Validates a <see cref="RawDoorEventRequest" />.
    /// </summary>
    public static string? Validate(RawDoorEventRequest request)
    {
        if (request.ReaderId <= 0)
            return "ReaderId must be greater than zero.";

        if (!Enum.IsDefined(request.EventType))
            return $"Invalid event type: {request.EventType}.";

        if (request.EventType != DoorEventType.EgressCycle)
            return CardValidation.ValidateCredential(request.CardNumber, request.FacilityCode, request.Format, request.CustomFormatId);

        return null;
    }

    /// <summary>
    /// Validates a <see cref="RawBitsRequest" />.
    /// </summary>
    public static string? Validate(RawBitsRequest request)
    {
        if (request.ReaderId <= 0)
            return "ReaderId must be greater than zero.";

        if (string.IsNullOrWhiteSpace(request.Bits))
            return "Bits cannot be empty.";

        if (request.Bits.Length > 1024)
            return "Bits cannot exceed 1024 characters.";

        foreach (var c in request.Bits)
        {
            if (c is not ('0' or '1'))
                return "Bits must contain only '0' and '1' characters.";
        }

        return null;
    }
}
