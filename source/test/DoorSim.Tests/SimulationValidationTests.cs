using DoorSim.Shared.Models;
using DoorSim.Validation;

namespace DoorSim.Tests;

public class SimulationValidationTests
{
    [Test]
    [Arguments(0)]
    [Arguments(-1)]
    public async Task DoorEventRequest_InvalidReaderId_ReturnsError(int readerId)
    {
        var req = new DoorEventRequest(readerId, DoorEventType.CardReadOnly, 1);
        var error = SimulationValidation.Validate(req);

        await Assert.That(error).IsNotNull();
        await Assert.That(error).Contains("ReaderId must be greater than zero");
    }

    [Test]
    [Arguments(DoorEventType.CardReadOnly)]
    [Arguments(DoorEventType.AccessCycle)]
    public async Task DoorEventRequest_MissingCardEntryId_ReturnsError(DoorEventType type)
    {
        var req = new DoorEventRequest(1, type);
        var error = SimulationValidation.Validate(req);

        await Assert.That(error).IsNotNull();
        await Assert.That(error).Contains("CardEntryId is required");
    }

    [Test]
    public async Task DoorEventRequest_EgressCycle_WithoutCard_IsValid()
    {
        var req = new DoorEventRequest(1, DoorEventType.EgressCycle);
        var error = SimulationValidation.Validate(req);

        await Assert.That(error).IsNull();
    }

    [Test]
    [Arguments(0)]
    [Arguments(-5)]
    public async Task RawCardRequest_InvalidReaderId_ReturnsError(int readerId)
    {
        var req = new RawCardRequest(readerId, 100, 10, WiegandFormat.Wiegand26);
        var error = SimulationValidation.Validate(req);

        await Assert.That(error).IsNotNull();
        await Assert.That(error).Contains("ReaderId must be greater than zero");
    }

    [Test]
    public async Task RawCardRequest_InvalidCardRange_ReturnsError()
    {
        var req = new RawCardRequest(1, 70_000, 10, WiegandFormat.Wiegand26);
        var error = SimulationValidation.Validate(req);

        await Assert.That(error).IsNotNull();
        await Assert.That(error).Contains("card number must be between 0 and 65,535");
    }

    [Test]
    public async Task RawCardRequest_Valid_ReturnsNull()
    {
        var req = new RawCardRequest(1, 1000, 10, WiegandFormat.Wiegand26);
        var error = SimulationValidation.Validate(req);

        await Assert.That(error).IsNull();
    }

    [Test]
    [Arguments(0)]
    [Arguments(-1)]
    public async Task RawDoorEventRequest_InvalidReaderId_ReturnsError(int readerId)
    {
        var req = new RawDoorEventRequest(readerId, DoorEventType.CardReadOnly, 100, 10, WiegandFormat.Wiegand26);
        var error = SimulationValidation.Validate(req);

        await Assert.That(error).IsNotNull();
        await Assert.That(error).Contains("ReaderId must be greater than zero");
    }

    [Test]
    public async Task RawDoorEventRequest_EgressCycle_IgnoresCardBounds()
    {
        var req = new RawDoorEventRequest(1, DoorEventType.EgressCycle, 999_999, 999, WiegandFormat.Wiegand26);
        var error = SimulationValidation.Validate(req);

        await Assert.That(error).IsNull();
    }

    [Test]
    [Arguments(0)]
    [Arguments(-1)]
    public async Task RawBitsRequest_InvalidReaderId_ReturnsError(int readerId)
    {
        var req = new RawBitsRequest(readerId, "1010");
        var error = SimulationValidation.Validate(req);

        await Assert.That(error).IsNotNull();
        await Assert.That(error).Contains("ReaderId must be greater than zero");
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    public async Task RawBitsRequest_EmptyBits_ReturnsError(string? bits)
    {
        var req = new RawBitsRequest(1, bits!);
        var error = SimulationValidation.Validate(req);

        await Assert.That(error).IsNotNull();
        await Assert.That(error).Contains("Bits cannot be empty");
    }

    [Test]
    [Arguments("10000000000010100011100100000002")]
    [Arguments("1010a01")]
    [Arguments("10 10")]
    public async Task RawBitsRequest_InvalidCharacters_ReturnsError(string bits)
    {
        var req = new RawBitsRequest(1, bits);
        var error = SimulationValidation.Validate(req);

        await Assert.That(error).IsNotNull();
        await Assert.That(error).Contains("Bits must contain only '0' and '1' characters");
    }

    [Test]
    public async Task RawBitsRequest_Valid_ReturnsNull()
    {
        var req = new RawBitsRequest(1, "10000000000010100011100100000000");
        var error = SimulationValidation.Validate(req);

        await Assert.That(error).IsNull();
    }
}
