using DoorSim.Shared.Models;
using DoorSim.Validation;

namespace DoorSim.Tests;

public class CardValidationTests
{
    [Test]
    public async Task Validate_EmptyLabel_ReturnsError()
    {
        var card = new CardEntry(0, "", 10, 100, WiegandFormat.Wiegand26, DateTimeOffset.UtcNow);
        var error = CardValidation.Validate(card);

        await Assert.That(error).IsNotNull();
        await Assert.That(error).Contains("label cannot be empty");
    }

    [Test]
    public async Task Validate_LabelTooLong_ReturnsError()
    {
        var card = new CardEntry(0, new string('A', 101), 10, 100, WiegandFormat.Wiegand26, DateTimeOffset.UtcNow);
        var error = CardValidation.Validate(card);

        await Assert.That(error).IsNotNull();
        await Assert.That(error).Contains("cannot exceed 100 characters");
    }

    [Test]
    [Arguments(0, 0)]
    [Arguments(255, 65535)]
    [Arguments(100, 1234)]
    public async Task Validate_Wiegand26_ValidRange_ReturnsNull(ushort fc, uint cn)
    {
        var error = CardValidation.ValidateCredential(cn, fc, WiegandFormat.Wiegand26);
        await Assert.That(error).IsNull();
    }

    [Test]
    [Arguments(256, 100)]
    [Arguments(1000, 100)]
    public async Task Validate_Wiegand26_FacilityCodeOutOfRange_ReturnsError(ushort fc, uint cn)
    {
        var error = CardValidation.ValidateCredential(cn, fc, WiegandFormat.Wiegand26);
        await Assert.That(error).IsNotNull();
        await Assert.That(error).Contains("facility code must be between 0 and 255");
    }

    [Test]
    [Arguments(10, 65536u)]
    [Arguments(10, 100000u)]
    public async Task Validate_Wiegand26_CardNumberOutOfRange_ReturnsError(ushort fc, uint cn)
    {
        var error = CardValidation.ValidateCredential(cn, fc, WiegandFormat.Wiegand26);
        await Assert.That(error).IsNotNull();
        await Assert.That(error).Contains("card number must be between 0 and 65,535");
    }

    [Test]
    [Arguments(0, 0)]
    [Arguments(255, 16777215)]
    public async Task Validate_Wiegand34_ValidRange_ReturnsNull(ushort fc, uint cn)
    {
        var error = CardValidation.ValidateCredential(cn, fc, WiegandFormat.Wiegand34);
        await Assert.That(error).IsNull();
    }

    [Test]
    [Arguments(256, 100)]
    public async Task Validate_Wiegand34_FacilityCodeOutOfRange_ReturnsError(ushort fc, uint cn)
    {
        var error = CardValidation.ValidateCredential(cn, fc, WiegandFormat.Wiegand34);
        await Assert.That(error).IsNotNull();
        await Assert.That(error).Contains("facility code must be between 0 and 255");
    }

    [Test]
    [Arguments(10, 16777216u)]
    public async Task Validate_Wiegand34_CardNumberOutOfRange_ReturnsError(ushort fc, uint cn)
    {
        var error = CardValidation.ValidateCredential(cn, fc, WiegandFormat.Wiegand34);
        await Assert.That(error).IsNotNull();
        await Assert.That(error).Contains("card number must be between 0 and 16,777,215");
    }

    [Test]
    [Arguments(0, 0)]
    [Arguments(4095, 1048575)]
    public async Task Validate_HidCorporate1000_ValidRange_ReturnsNull(ushort fc, uint cn)
    {
        var error = CardValidation.ValidateCredential(cn, fc, WiegandFormat.HidCorporate1000);
        await Assert.That(error).IsNull();
    }

    [Test]
    [Arguments(4096, 100)]
    public async Task Validate_HidCorporate1000_FacilityCodeOutOfRange_ReturnsError(ushort fc, uint cn)
    {
        var error = CardValidation.ValidateCredential(cn, fc, WiegandFormat.HidCorporate1000);
        await Assert.That(error).IsNotNull();
        await Assert.That(error).Contains("facility code must be between 0 and 4,095");
    }

    [Test]
    [Arguments(10, 1048576u)]
    public async Task Validate_HidCorporate1000_CardNumberOutOfRange_ReturnsError(ushort fc, uint cn)
    {
        var error = CardValidation.ValidateCredential(cn, fc, WiegandFormat.HidCorporate1000);
        await Assert.That(error).IsNotNull();
        await Assert.That(error).Contains("card number must be between 0 and 1,048,575");
    }

    [Test]
    public async Task Validate_Custom_MissingCustomFormatId_ReturnsError()
    {
        var error = CardValidation.ValidateCredential(1234, 100, WiegandFormat.Custom);
        await Assert.That(error).IsNotNull();
        await Assert.That(error).Contains("CustomFormatId is required");
    }

    [Test]
    public async Task Validate_Custom_ZeroCustomFormatId_ReturnsError()
    {
        var error = CardValidation.ValidateCredential(1234, 100, WiegandFormat.Custom, 0);
        await Assert.That(error).IsNotNull();
        await Assert.That(error).Contains("CustomFormatId is required");
    }

    [Test]
    public async Task Validate_Custom_WithCustomFormatId_ReturnsNull()
    {
        // Bit-width range checking is deferred until the format definition is loaded from the DB.
        var error = CardValidation.ValidateCredential(1234, 100, WiegandFormat.Custom, 5);
        await Assert.That(error).IsNull();
    }

    [Test]
    public async Task Validate_CardEntry_Custom_MissingCustomFormatId_ReturnsError()
    {
        var card = new CardEntry(0, "Custom Card", 100, 1234, WiegandFormat.Custom, DateTimeOffset.UtcNow);
        var error = CardValidation.Validate(card);

        await Assert.That(error).IsNotNull();
        await Assert.That(error).Contains("CustomFormatId is required");
    }
}
