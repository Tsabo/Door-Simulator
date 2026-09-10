namespace DoorSim.Shared.Models;

public enum WiegandFormat
{
    Wiegand26,        // 8-bit FC, 16-bit card number
    Wiegand34,        // 8-bit FC, 24-bit card number
    Wiegand37,        // No FC,   35-bit card number (uint max ≈ 4 billion)
    HidCorporate1000, // 12-bit FC, 20-bit card number, 35 bits total, no parity
}
