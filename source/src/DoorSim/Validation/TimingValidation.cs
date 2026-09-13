namespace DoorSim.Validation;

/// <summary>
/// Domain validation rules for simulation timing settings.
/// </summary>
public static class TimingValidation
{
    public const int MinCardToDoorDelayMs = 0;
    public const int MaxCardToDoorDelayMs = 60_000;

    public const int MinDoorOpenMs = 100;
    public const int MaxDoorOpenMs = 300_000;

    public const int MinRexLeadMs = 0;
    public const int MaxRexLeadMs = 60_000;

    public const int MinQuickRexMs = 100;
    public const int MaxQuickRexMs = 60_000;

    public const int MinQueueItemDelayMs = 0;
    public const int MaxQueueItemDelayMs = 60_000;

    /// <summary>Zero is allowed and means "never purge telemetry".</summary>
    public const int MinMetricsRetentionDays = 0;

    public const int MaxMetricsRetentionDays = 3650;

    /// <summary>
    /// Validates timing settings to prevent invalid simulation parameters.
    /// </summary>
    public static string? Validate(SimulationTimingSettings settings)
    {
        if (settings.CardToDoorDelayMs is < MinCardToDoorDelayMs or > MaxCardToDoorDelayMs)
            return $"Card-to-door delay must be between {MinCardToDoorDelayMs} and {MaxCardToDoorDelayMs} ms. Provided: {settings.CardToDoorDelayMs}.";

        if (settings.DoorOpenMs is < MinDoorOpenMs or > MaxDoorOpenMs)
            return $"Door open duration must be between {MinDoorOpenMs} and {MaxDoorOpenMs} ms. Provided: {settings.DoorOpenMs}.";

        if (settings.RexLeadMs is < MinRexLeadMs or > MaxRexLeadMs)
            return $"REX lead time must be between {MinRexLeadMs} and {MaxRexLeadMs} ms. Provided: {settings.RexLeadMs}.";

        if (settings.QuickRexMs is < MinQuickRexMs or > MaxQuickRexMs)
            return $"Quick REX duration must be between {MinQuickRexMs} and {MaxQuickRexMs} ms. Provided: {settings.QuickRexMs}.";

        if (settings.MetricsRetentionDays is < MinMetricsRetentionDays or > MaxMetricsRetentionDays)
            return $"Metrics retention must be between {MinMetricsRetentionDays} and {MaxMetricsRetentionDays} days. Provided: {settings.MetricsRetentionDays}.";

        if (settings.QueueItemDelayMs is < MinQueueItemDelayMs or > MaxQueueItemDelayMs)
            return $"Queue item delay must be between {MinQueueItemDelayMs} and {MaxQueueItemDelayMs} ms. Provided: {settings.QueueItemDelayMs}.";

        return null;
    }
}
