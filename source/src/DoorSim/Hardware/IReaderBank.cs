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
}
