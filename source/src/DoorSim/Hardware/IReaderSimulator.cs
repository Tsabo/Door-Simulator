namespace DoorSim.Hardware;

/// <summary>
/// Abstracts a single simulated reader, regardless of protocol (Wiegand or OSDP).
/// </summary>
public interface IReaderSimulator
{
    /// <summary>The door configuration this simulator was built from.</summary>
    DoorConfiguration Config { get; }

    /// <summary>True when the reader's transport is connected and responding.</summary>
    /// <remarks>Always true for Wiegand (GPIO has no handshake). For OSDP, reflects the OSDP.Net Device.IsConnected state.</remarks>
    bool IsConnected { get; }

    /// <summary>
    /// The last LED and buzzer state commanded by the panel.
    /// Always <c>null</c> for Wiegand (one-directional protocol — panel sends no feedback).
    /// For OSDP, reflects the most recent <c>osdp_LED</c> and <c>osdp_BUZ</c> commands.
    /// </summary>
    ReaderLedState? LedState { get; }

    /// <summary>True while the DPS relay is asserted (door open signal active).</summary>
    bool IsDoorOpen { get; }

    /// <summary>True while the REX relay is asserted (egress/motion signal active).</summary>
    bool IsRexActive { get; }

    /// <summary>Transmit a Wiegand credential from the card library.</summary>
    Task SendCardAsync(CardEntry card);

    /// <summary>Transmit a credential from raw values — no library entry required.</summary>
    Task SendCardAsync(uint cardNumber, ushort facilityCode, WiegandFormat format);

    /// <summary>Transmit a credential from raw values using a user-defined custom format.</summary>
    Task SendCardAsync(uint cardNumber, ushort facilityCode, CustomCardFormat format);

    /// <summary>Transmit an arbitrary raw bit-stream — no format calculation.</summary>
    Task SendBitsAsync(string bits);

    /// <summary>Full access cycle: card read → door opens → door closes.</summary>
    Task SimulateAccessCycleAsync(CardEntry card, int cardToDoorDelayMs, int doorOpenMs);

    /// <summary>Full access cycle from raw values — no library entry required.</summary>
    Task SimulateAccessCycleAsync(uint cardNumber, ushort facilityCode, WiegandFormat format, int cardToDoorDelayMs, int doorOpenMs);

    /// <summary>Full access cycle from raw values using a user-defined custom format.</summary>
    Task SimulateAccessCycleAsync(uint cardNumber, ushort facilityCode, CustomCardFormat format, int cardToDoorDelayMs, int doorOpenMs);

    /// <summary>Full egress cycle: REX trips → door opens → door closes → REX resets.</summary>
    Task SimulateEgressCycleAsync(int rexLeadMs, int doorOpenMs);

    /// <summary>DPS LOW — NC relay opens, door open.</summary>
    Task OpenDoorAsync();

    /// <summary>DPS HIGH — NC relay closes, door closed.</summary>
    Task CloseDoorAsync();

    /// <summary>REX LOW — NC relay opens, motion detected.</summary>
    Task TripRexAsync();

    /// <summary>REX HIGH — NC relay closes, REX reset.</summary>
    Task ResetRexAsync();
}
