using System.ComponentModel;

namespace DoorSim.Shared.Models;

[Description("Lifecycle status of a reader's most recent simulated event.")]
public enum SimulationStatus
{
    [Description("No simulation is currently running for this reader.")]
    Idle,

    [Description("A simulated event is currently in progress.")]
    Running,

    [Description("The most recent simulated event completed successfully.")]
    Success,

    [Description("The most recent simulated event failed — see server logs for details.")]
    Error,
}
