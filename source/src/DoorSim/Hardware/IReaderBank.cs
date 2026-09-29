using System.Diagnostics.CodeAnalysis;

namespace DoorSim.Hardware;

/// <summary>
/// Manages the set of active <see cref="IReaderSimulator" /> instances.
/// </summary>
public interface IReaderBank
{
    /// <summary>All currently active door IDs.</summary>
    IReadOnlyCollection<int> ActiveDoorIds { get; }

    /// <summary>
    /// Returns the simulator for the given door ID.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Door ID not found.</exception>
    IReaderSimulator GetReader(int doorId);

    /// <summary>
    /// Tries to get the simulator for the given door ID without throwing.
    /// </summary>
    bool TryGetReader(int doorId, [NotNullWhen(true)] out IReaderSimulator? simulator);

    /// <summary>
    /// Returns true if the simulator for the given door ID exists in the active bank.
    /// </summary>
    bool ContainsReader(int doorId);

    /// <summary>
    /// Why a configured door has no active simulator on this host (e.g. a Wiegand door without GPIO),
    /// or null when the door is active or simply unknown.
    /// </summary>
    string? GetUnavailableReason(int doorId) => null;
}
