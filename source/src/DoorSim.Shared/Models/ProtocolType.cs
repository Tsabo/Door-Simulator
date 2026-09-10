using System.ComponentModel;

namespace DoorSim.Shared.Models;

[Description("Card-read transport a door uses.")]
public enum ProtocolType
{
    [Description("Card reads are sent over Wiegand D0/D1 GPIO lines.")]
    Wiegand,

    [Description("Card reads are sent as an OSDP peripheral device (PD) over RS-485.")]
    Osdp,
}
