using DoorSim.Shared.Models;
using DoorSim.Validation;

namespace DoorSim.Tests;

public class MetricsValidationTests
{
    [Test]
    public async Task ValidateTimeSeries_Defaults_IsValid()
    {
        var error = MetricsValidation.ValidateTimeSeries(5, 60);

        await Assert.That(error).IsNull();
    }

    [Test]
    [Arguments(0, 60)]
    [Arguments(1441, 5000)]
    public async Task ValidateTimeSeries_BucketOutOfRange_ReturnsError(int bucketMinutes, int windowMinutes)
    {
        var error = MetricsValidation.ValidateTimeSeries(bucketMinutes, windowMinutes);

        await Assert.That(error).IsNotNull();
        await Assert.That(error).Contains("Bucket size");
    }

    [Test]
    [Arguments(5, 0)]
    [Arguments(5, 10081)]
    public async Task ValidateTimeSeries_WindowOutOfRange_ReturnsError(int bucketMinutes, int windowMinutes)
    {
        var error = MetricsValidation.ValidateTimeSeries(bucketMinutes, windowMinutes);

        await Assert.That(error).IsNotNull();
        await Assert.That(error).Contains("Window");
    }

    [Test]
    public async Task ValidateTimeSeries_BucketLargerThanWindow_ReturnsError()
    {
        var error = MetricsValidation.ValidateTimeSeries(120, 60);

        await Assert.That(error).IsNotNull();
        await Assert.That(error).Contains("cannot exceed");
    }

    [Test]
    [Arguments(0)]
    [Arguments(366)]
    public async Task ValidateWindowDays_OutOfRange_ReturnsError(int windowDays)
    {
        var error = MetricsValidation.ValidateWindowDays(windowDays);

        await Assert.That(error).IsNotNull();
    }

    [Test]
    [Arguments(0)]
    [Arguments(1001)]
    public async Task ValidateLimit_OutOfRange_ReturnsError(int limit)
    {
        var error = MetricsValidation.ValidateLimit(limit);

        await Assert.That(error).IsNotNull();
    }

    [Test]
    public async Task ValidateLimit_InRange_IsValid() => await Assert.That(MetricsValidation.ValidateLimit(100)).IsNull();

    [Test]
    [Arguments(-1)]
    [Arguments(3651)]
    public async Task Validate_MetricsRetentionOutOfRange_ReturnsError(int retentionDays)
    {
        var settings = new SimulationTimingSettings(MetricsRetentionDays: retentionDays);
        var error = TimingValidation.Validate(settings);

        await Assert.That(error).IsNotNull();
        await Assert.That(error).Contains("Metrics retention");
    }

    [Test]
    public async Task Validate_MetricsRetentionZero_IsValid()
    {
        var settings = new SimulationTimingSettings(MetricsRetentionDays: 0);

        await Assert.That(TimingValidation.Validate(settings)).IsNull();
    }
}
