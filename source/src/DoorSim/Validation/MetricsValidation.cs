namespace DoorSim.Validation;

/// <summary>
/// Bounds checks for telemetry query parameters, so a malformed request can't force
/// an unbounded scan or an enormous bucket series.
/// </summary>
public static class MetricsValidation
{
    public const int MinBucketMinutes = 1;
    public const int MaxBucketMinutes = 1440;

    public const int MinWindowMinutes = 1;
    public const int MaxWindowMinutes = 10_080;

    public const int MinWindowDays = 1;
    public const int MaxWindowDays = 365;

    public const int MinLimit = 1;
    public const int MaxLimit = 1000;

    public static string? ValidateTimeSeries(int bucketMinutes, int windowMinutes)
    {
        if (bucketMinutes is < MinBucketMinutes or > MaxBucketMinutes)
            return $"Bucket size must be between {MinBucketMinutes} and {MaxBucketMinutes} minutes. Provided: {bucketMinutes}.";

        if (windowMinutes is < MinWindowMinutes or > MaxWindowMinutes)
            return $"Window must be between {MinWindowMinutes} and {MaxWindowMinutes} minutes. Provided: {windowMinutes}.";

        if (bucketMinutes > windowMinutes)
            return $"Bucket size ({bucketMinutes} min) cannot exceed the window ({windowMinutes} min).";

        return null;
    }

    public static string? ValidateWindowDays(int windowDays) =>
        windowDays is < MinWindowDays or > MaxWindowDays
            ? $"Window must be between {MinWindowDays} and {MaxWindowDays} days. Provided: {windowDays}."
            : null;

    public static string? ValidateLimit(int limit) =>
        limit is < MinLimit or > MaxLimit
            ? $"Limit must be between {MinLimit} and {MaxLimit}. Provided: {limit}."
            : null;
}
