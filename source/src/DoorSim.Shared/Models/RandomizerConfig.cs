namespace DoorSim.Shared.Models;

/// <summary>Configuration DTO for the future randomizer service.</summary>
public record RandomizerConfig(
    int[] ReaderIds,
    int EventCount,
    int MinDelayMs,
    int MaxDelayMs,
    int AccessCycleWeight,
    int EgressCycleWeight,
    int CardReadOnlyWeight);
