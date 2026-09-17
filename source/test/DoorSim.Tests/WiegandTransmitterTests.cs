using System.Numerics;
using DoorSim.Hardware;

namespace DoorSim.Tests;

/// <summary>
/// Tests for Wiegand bit-frame building in <see cref="WiegandTransmitter" />.
/// These tests run without GPIO hardware.
/// </summary>
public class WiegandTransmitterTests
{
    // -------------------------------------------------------------------------
    // Wiegand26
    // -------------------------------------------------------------------------

    [Test]
    public async Task BuildWiegand26_FrameIs26Bits()
    {
        var frame = WiegandTransmitter.BuildWiegand26(1, 1);
        await Assert.That(frame).IsLessThan(1UL << 26);
    }

    [Test]
    [Arguments((ushort)0, (ushort)0)]
    [Arguments((ushort)1, (ushort)1)]
    [Arguments((ushort)255, (ushort)65535)]
    [Arguments((ushort)100, (ushort)12345)]
    public async Task BuildWiegand26_EvenParityBitIsCorrect(ushort fc, ushort cn)
    {
        var frame = WiegandTransmitter.BuildWiegand26(fc, cn);
        var ep = frame >> 25 & 1;
        var dataBits = frame >> 13 & 0xFFF; // bits 24-13
        var total = BitOperations.PopCount((uint)dataBits) + (int)ep;
        await Assert.That(total % 2).IsEqualTo(0);
    }

    [Test]
    [Arguments((ushort)0, (ushort)0)]
    [Arguments((ushort)1, (ushort)1)]
    [Arguments((ushort)255, (ushort)65535)]
    [Arguments((ushort)100, (ushort)12345)]
    public async Task BuildWiegand26_OddParityBitIsCorrect(ushort fc, ushort cn)
    {
        var frame = WiegandTransmitter.BuildWiegand26(fc, cn);
        var op = frame & 1;
        var dataBits = frame >> 1 & 0xFFF; // bits 12-1
        var total = BitOperations.PopCount((uint)dataBits) + (int)op;
        await Assert.That(total % 2).IsNotEqualTo(0);
    }

    [Test]
    public async Task BuildWiegand26_DataBitsRoundTrip()
    {
        ushort fc = 0xAB;
        ushort cn = 0x1234;
        var frame = WiegandTransmitter.BuildWiegand26(fc, cn);

        var extractedFc = (ushort)(frame >> 17 & 0xFF);
        var extractedCn = (ushort)(frame >> 1 & 0xFFFF);

        await Assert.That(extractedFc).IsEqualTo(fc);
        await Assert.That(extractedCn).IsEqualTo(cn);
    }

    // -------------------------------------------------------------------------
    // Wiegand34
    // -------------------------------------------------------------------------

    [Test]
    public async Task BuildWiegand34_FrameIs34Bits()
    {
        var frame = WiegandTransmitter.BuildWiegand34(1, 1);
        await Assert.That(frame).IsLessThan(1UL << 34);
    }

    [Test]
    [Arguments((ushort)0, 0u)]
    [Arguments((ushort)255, 16_777_215u)]
    [Arguments((ushort)100, 54321u)]
    public async Task BuildWiegand34_EvenParityBitIsCorrect(ushort fc, uint cn)
    {
        var frame = WiegandTransmitter.BuildWiegand34(fc, cn);
        var ep = frame >> 33 & 1;
        var dataBits = (uint)(frame >> 17 & 0xFFFF);
        var total = BitOperations.PopCount(dataBits) + (int)ep;
        await Assert.That(total % 2).IsEqualTo(0);
    }

    [Test]
    [Arguments((ushort)0, 0u)]
    [Arguments((ushort)255, 16_777_215u)]
    [Arguments((ushort)100, 54321u)]
    public async Task BuildWiegand34_OddParityBitIsCorrect(ushort fc, uint cn)
    {
        var frame = WiegandTransmitter.BuildWiegand34(fc, cn);
        var op = frame & 1;
        var dataBits = (uint)(frame >> 1 & 0xFFFF);
        var total = BitOperations.PopCount(dataBits) + (int)op;
        await Assert.That(total % 2).IsNotEqualTo(0);
    }

    [Test]
    public async Task BuildWiegand34_DataBitsRoundTrip()
    {
        ushort fc = 0xAB;
        uint cn = 0xABCDEF & 0xFFFFFF;
        var frame = WiegandTransmitter.BuildWiegand34(fc, cn);

        var extractedFc = (ushort)(frame >> 25 & 0xFF);
        var extractedCn = (uint)(frame >> 1 & 0xFFFFFF);

        await Assert.That(extractedFc).IsEqualTo(fc);
        await Assert.That(extractedCn).IsEqualTo(cn);
    }

    // -------------------------------------------------------------------------
    // Wiegand37
    // -------------------------------------------------------------------------

    [Test]
    public async Task BuildWiegand37_FrameIs37Bits()
    {
        var frame = WiegandTransmitter.BuildWiegand37(1);
        await Assert.That(frame).IsLessThan(1UL << 37);
    }

    [Test]
    [Arguments(0u)]
    [Arguments(1u)]
    [Arguments(uint.MaxValue)]
    [Arguments(12_345_678u)]
    public async Task BuildWiegand37_EvenParityBitIsCorrect(uint cn)
    {
        var frame = WiegandTransmitter.BuildWiegand37(cn);
        var ep = frame >> 36 & 1;
        var dataBits = frame >> 18 & 0x3FFFF;
        var total = BitOperations.PopCount((uint)dataBits) + (int)ep;
        await Assert.That(total % 2).IsEqualTo(0);
    }

    [Test]
    [Arguments(0u)]
    [Arguments(1u)]
    [Arguments(uint.MaxValue)]
    [Arguments(12_345_678u)]
    public async Task BuildWiegand37_OddParityBitIsCorrect(uint cn)
    {
        var frame = WiegandTransmitter.BuildWiegand37(cn);
        var op = frame & 1;
        var dataBits = frame >> 1 & 0x1FFFF;
        var total = BitOperations.PopCount((uint)dataBits) + (int)op;
        await Assert.That(total % 2).IsNotEqualTo(0);
    }

    [Test]
    public async Task BuildWiegand37_DataBitsRoundTrip()
    {
        uint cn = 0xDEAD_BEEF;
        var frame = WiegandTransmitter.BuildWiegand37(cn);
        var extractedCn = (uint)(frame >> 1 & 0xFFFF_FFFF);
        await Assert.That(extractedCn).IsEqualTo(cn);
    }

    // -------------------------------------------------------------------------
    // HID Corporate 1000 — 35-bit, no parity
    // -------------------------------------------------------------------------

    [Test]
    public async Task BuildHidCorporate1000_FrameIs35Bits()
    {
        var frame = WiegandTransmitter.BuildHidCorporate1000(1, 1);
        await Assert.That(frame).IsLessThan(1UL << 35);
    }

    [Test]
    public async Task BuildHidCorporate1000_DataBitsRoundTrip()
    {
        ushort fc = 0xABC & 0xFFF; // 12-bit
        uint cn = 0xABCDE & 0xFFFFF; // 20-bit
        var frame = WiegandTransmitter.BuildHidCorporate1000(fc, cn);

        // FC sits at bits 32-21 (LSB-0); CN sits at bits 20-1 (LSB-0)
        var extractedFc = (ushort)(frame >> 21 & 0xFFF);
        var extractedCn = (uint)(frame >> 1 & 0xFFFFF);

        await Assert.That(extractedFc).IsEqualTo(fc);
        await Assert.That(extractedCn).IsEqualTo(cn);
    }

    [Test]
    [Arguments((ushort)0, 0u)]
    [Arguments((ushort)1, 1u)]
    [Arguments((ushort)4095, 1_048_575u)]
    [Arguments((ushort)500, 123_456u)]
    public async Task BuildHidCorporate1000_NonDataBitsAreZero(ushort fc, uint cn)
    {
        var frame = WiegandTransmitter.BuildHidCorporate1000(fc, cn);
        // Bit 34 (MSB), bit 33 (unused), and bit 0 (LSB) must be 0
        await Assert.That(frame >> 33 & 0b11UL).IsEqualTo(0UL);
        await Assert.That(frame & 1UL).IsEqualTo(0UL);
    }

    [Test]
    public async Task BuildHidCorporate1000_FacilityCodeClampedTo12Bits()
    {
        // Passing a value > 12-bit max — only lower 12 bits should be encoded
        ushort fc = 0xFFFF;
        var frame = WiegandTransmitter.BuildHidCorporate1000(fc, 0);
        var extractedFc = frame >> 21 & 0xFFF;
        await Assert.That(extractedFc).IsEqualTo(0xFFFUL);
        await Assert.That(frame).IsLessThan(1UL << 35);
    }

    [Test]
    public async Task BuildHidCorporate1000_CardNumberClampedTo20Bits()
    {
        // Passing a value > 20-bit max — only lower 20 bits should be encoded
        uint cn = 0xFFFF_FFFF;
        var frame = WiegandTransmitter.BuildHidCorporate1000(0, cn);
        var extractedCn = frame >> 1 & 0xFFFFF;
        await Assert.That(extractedCn).IsEqualTo(0xFFFFFUL);
        await Assert.That(frame).IsLessThan(1UL << 35);
    }
}
