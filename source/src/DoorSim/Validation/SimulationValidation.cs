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

        return CardValidation.ValidateCredential(request.CardNumber, request.FacilityCode, request.Format);
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
            return CardValidation.ValidateCredential(request.CardNumber, request.FacilityCode, request.Format);

        return null;
    }
}
