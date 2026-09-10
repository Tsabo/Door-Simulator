namespace DoorSim.Hardware;

/// <summary>
/// Manages the set of active <see cref="IReaderSimulator"/> instances.
/// </summary>
public interface IReaderBank
{
    /// <summary>
    /// Returns the simulator for the given door ID.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Door ID not found.</exception>
    IReaderSimulator GetReader(int doorId);

    /// <summary>All currently active door IDs.</summary>
    IReadOnlyCollection<int> ActiveDoorIds { get; }
}
