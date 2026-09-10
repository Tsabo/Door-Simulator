namespace DoorSim.Hardware;

/// <summary>
/// Handles low-level Wiegand D0/D1 bit-stream transmission.
/// Owned by <see cref="DoorSimulator" /> — not registered in DI.
/// All timing uses Stopwatch + SpinWait; no Thread.Sleep.
/// </summary>
internal sealed class WiegandTransmitter(GpioController? gpio, int d0Pin, int d1Pin)
{
    private const long PulseUs = 50; // Data pulse width (µs)
    private const long BitGapUs = 2_000; // Inter-bit gap (µs)
    private const long MessageGapUs = 50_000; // Inter-message gap (µs)

    // -------------------------------------------------------------------------
    // Public send methods
    // -------------------------------------------------------------------------

    /// <summary>Transmit a credential from a <see cref="CardEntry" />.</summary>
    public void Send(CardEntry card) =>
        Send(card.CardNumber, card.FacilityCode, card.Format);

    /// <summary>Transmit a credential from raw values.</summary>
    public void Send(uint cardNumber, ushort facilityCode, WiegandFormat format)
    {
        var (frame, bitCount) = format switch
        {
            WiegandFormat.Wiegand26        => (BuildWiegand26(facilityCode, (ushort)cardNumber), 26),
            WiegandFormat.Wiegand34        => (BuildWiegand34(facilityCode, cardNumber), 34),
            WiegandFormat.Wiegand37        => (BuildWiegand37(cardNumber), 37),
            WiegandFormat.HidCorporate1000 => (BuildHidCorporate1000(facilityCode, cardNumber), 35),
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };

        TransmitBits(frame, bitCount);
    }

    // -------------------------------------------------------------------------
    // Bit-frame builders (internal for testability)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Wiegand26: [EP(1)][FC(8)][CN(16)][OP(1)] = 26 bits.
    /// EP = even parity of first 12 data bits: FC[7:0] + CN[15:12].
    /// OP = odd  parity of last  12 data bits: CN[11:0].
    /// FC is clamped to 8 bits; values > 255 use the lower 8 bits.
    /// </summary>
    internal static ulong BuildWiegand26(ushort fc, ushort cn)
    {
        // bit25=EP, bits24-17=FC[7:0], bits16-1=CN, bit0=OP
        // Mask FC to 8 bits to prevent overflow into the EP bit (bit 25).
        ulong frame = (ulong)(fc & 0xFF) << 17 | (ulong)cn << 1;

        var epBits = (uint)(frame >> 13 & 0xFFF); // bits 24-13 (FC + CN[15:12])
        var opBits = (uint)(frame >> 1 & 0xFFF); // bits 12-1  (CN[11:0])

        var ep = (ulong)(BitOperations.PopCount(epBits) % 2 != 0
            ? 1
            : 0); // make total even

        var op = (ulong)(BitOperations.PopCount(opBits) % 2 == 0
            ? 1
            : 0); // make total odd

        return frame | ep << 25 | op;
    }

    /// <summary>
    /// Wiegand34: [EP(1)][FC(8)][CN(24)][OP(1)] = 34 bits.
    /// EP = even parity of FC[7:0] + CN[23:16].
    /// OP = odd  parity of CN[15:0].
    /// FC is clamped to 8 bits; values > 255 use the lower 8 bits.
    /// </summary>
    internal static ulong BuildWiegand34(ushort fc, uint cn)
    {
        // bit33=EP, bits32-25=FC[7:0], bits24-1=CN, bit0=OP
        // Mask FC to 8 bits to prevent overflow into the EP bit (bit 33).
        ulong frame = (ulong)(fc & 0xFF) << 25 | (ulong)cn << 1;

        var epBits = (uint)(frame >> 17 & 0xFFFF); // bits 32-17 (FC + CN[23:16])
        var opBits = (uint)(frame >> 1 & 0xFFFF); // bits 16-1  (CN[15:0])

        var ep = (ulong)(BitOperations.PopCount(epBits) % 2 != 0
            ? 1
            : 0);

        var op = (ulong)(BitOperations.PopCount(opBits) % 2 == 0
            ? 1
            : 0);

        return frame | ep << 33 | op;
    }

    /// <summary>
    /// Wiegand37 (no FC): [EP(1)][CN(35)][OP(1)] = 37 bits.
    /// EP = even parity of CN[34:17] (top 18 bits).
    /// OP = odd  parity of CN[16:0]  (bottom 17 bits).
    /// Note: CardNumber is uint (32-bit), so CN bits 34-32 are always 0.
    /// </summary>
    internal static ulong BuildWiegand37(uint cn)
    {
        // bit36=EP, bits35-1=CN, bit0=OP
        ulong frame = (ulong)cn << 1;

        var epBits = frame >> 18 & 0x3FFFF; // bits 35-18 (top 18 data bits)
        var opBits = frame >> 1 & 0x1FFFF; // bits 17-1  (bottom 17 data bits)

        var ep = (ulong)(BitOperations.PopCount((uint)epBits) % 2 != 0
            ? 1
            : 0);

        var op = (ulong)(BitOperations.PopCount((uint)opBits) % 2 == 0
            ? 1
            : 0);

        return frame | ep << 36 | op;
    }

    /// <summary>
    /// HID Corporate 1000 (H10302): [0(1)][0(1)][FC(12)][CN(20)][0(1)] = 35 bits.
    /// No parity. Positions are 0-indexed from MSB:
    ///   pos 0  → bit 34 (LSB-0): leading 0
    ///   pos 1  → bit 33 (LSB-0): 0 (unused)
    ///   pos 2–13  → bits 32–21 (LSB-0): FC (12 bits) → fc &lt;&lt; 21
    ///   pos 14–33 → bits 20–1  (LSB-0): CN (20 bits) → cn &lt;&lt; 1
    ///   pos 34 → bit 0  (LSB-0): trailing 0
    /// FC is 12-bit (0–4095); CN is 20-bit (0–1 048 575).
    /// </summary>
    internal static ulong BuildHidCorporate1000(ushort fc, uint cn)
    {
        return (ulong)(fc & 0xFFF) << 21 | (ulong)(cn & 0xFFFFF) << 1;
    }

    // -------------------------------------------------------------------------
    // Transmission
    // -------------------------------------------------------------------------

    private void TransmitBits(ulong frame, int bitCount)
    {
        if (gpio is null)
            return; // Non-Linux dev environment — skip GPIO

        // Transmit MSB first
        for (var i = bitCount - 1; i >= 0; i--)
        {
            var bit = frame >> i & 1;
            var pin = bit == 0
                ? d0Pin
                : d1Pin;

            gpio.Write(pin, PinValue.Low);
            SpinDelayMicroseconds(PulseUs);
            gpio.Write(pin, PinValue.High);
            SpinDelayMicroseconds(BitGapUs);
        }

        // Inter-message gap (minus the BitGap already elapsed after the last bit)
        SpinDelayMicroseconds(MessageGapUs - BitGapUs);
    }

    private static void SpinDelayMicroseconds(long microseconds)
    {
        var ticks = microseconds * Stopwatch.Frequency / 1_000_000L;
        var start = Stopwatch.GetTimestamp();
        // Thread.SpinWait emits PAUSE hints and never yields to the OS scheduler,
        // unlike SpinWait.SpinOnce which calls Thread.Sleep(0) after ~10 iterations.
        while (Stopwatch.GetTimestamp() - start < ticks)
            Thread.SpinWait(1);
    }
}
