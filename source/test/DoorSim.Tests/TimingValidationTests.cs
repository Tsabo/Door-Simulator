using DoorSim.Shared.Models;
using DoorSim.Validation;

namespace DoorSim.Tests;

public class TimingValidationTests
{
    [Test]
    public async Task Validate_DefaultSettings_IsValid()
    {
        var settings = new SimulationTimingSettings();
        var error = TimingValidation.Validate(settings);

        await Assert.That(error).IsNull();
    }

    [Test]
    [Arguments(-1, 5000, 500, 2000)]
    [Arguments(60001, 5000, 500, 2000)]
    public async Task Validate_CardToDoorDelayOutOfRange_ReturnsError(int c, int d, int r, int q)
    {
        var settings = new SimulationTimingSettings(c, d, r, q);
        var error = TimingValidation.Validate(settings);

        await Assert.That(error).IsNotNull();
        await Assert.That(error).Contains("Card-to-door delay");
    }

    [Test]
    [Arguments(500, 50, 500, 2000)]
    [Arguments(500, 300001, 500, 2000)]
    public async Task Validate_DoorOpenMsOutOfRange_ReturnsError(int c, int d, int r, int q)
    {
        var settings = new SimulationTimingSettings(c, d, r, q);
        var error = TimingValidation.Validate(settings);

        await Assert.That(error).IsNotNull();
        await Assert.That(error).Contains("Door open duration");
    }

    [Test]
    [Arguments(500, 5000, -1, 2000)]
    [Arguments(500, 5000, 60001, 2000)]
    public async Task Validate_RexLeadMsOutOfRange_ReturnsError(int c, int d, int r, int q)
    {
        var settings = new SimulationTimingSettings(c, d, r, q);
        var error = TimingValidation.Validate(settings);

        await Assert.That(error).IsNotNull();
        await Assert.That(error).Contains("REX lead time");
    }

    [Test]
    [Arguments(500, 5000, 500, 50)]
    [Arguments(500, 5000, 500, 60001)]
    public async Task Validate_QuickRexMsOutOfRange_ReturnsError(int c, int d, int r, int q)
    {
        var settings = new SimulationTimingSettings(c, d, r, q);
        var error = TimingValidation.Validate(settings);

        await Assert.That(error).IsNotNull();
        await Assert.That(error).Contains("Quick REX duration");
    }

    [Test]
    [Arguments(-1)]
    [Arguments(60001)]
    public async Task Validate_QueueItemDelayMsOutOfRange_ReturnsError(int delay)
    {
        var settings = new SimulationTimingSettings(QueueItemDelayMs: delay);
        var error = TimingValidation.Validate(settings);

        await Assert.That(error).IsNotNull();
        await Assert.That(error).Contains("Queue item delay");
    }
}
