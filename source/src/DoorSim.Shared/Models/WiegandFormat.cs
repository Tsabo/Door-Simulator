using System.ComponentModel;

namespace DoorSim.Shared.Models;

[Description("Wiegand bit-frame format used when encoding a card read.")]
public enum WiegandFormat
{
    [Description("26-bit format — 8-bit facility code, 16-bit card number. The most common legacy Wiegand format.")]
    Wiegand26,

    [Description("34-bit format — 8-bit facility code, 24-bit card number.")]
    Wiegand34,

    [Description("37-bit format — no facility code, 35-bit card number (card number up to ~4 billion).")]
    Wiegand37,

    [Description("HID Corporate 1000 — 12-bit facility code, 20-bit card number, 35 bits total, no parity.")]
    HidCorporate1000,
}
