using DoorSim.Shared.Models;
using DoorSim.Validation;

namespace DoorSim.Tests;

public class CardFormatValidationTests
{
    private const string ValidCardMask = "XFFFFFFFFCCCCCCCCCCCCCCCCX";
    private const string ValidParity1 = "EPPPPPPPPPPPPXXXXXXXXXXXXX";
    private const string ValidParity2 = "XXXXXXXXXXXXXPPPPPPPPPPPPO";

    private static CustomCardFormat ValidFormat() =>
        new(0, "Test Format", ValidCardMask, ValidParity1, ValidParity2, null, DateTimeOffset.UtcNow);

    [Test]
    public async Task ValidateDefinition_ValidFormat_ReturnsNull()
    {
        var error = CardFormatValidation.ValidateDefinition(ValidFormat());
        await Assert.That(error).IsNull();
    }

    [Test]
    public async Task ValidateDefinition_EmptyName_ReturnsError()
    {
        var error = CardFormatValidation.ValidateDefinition(ValidFormat() with { Name = "" });
        await Assert.That(error).IsNotNull();
        await Assert.That(error).Contains("name cannot be empty");
    }

    [Test]
    public async Task ValidateDefinition_NameTooLong_ReturnsError()
    {
        var error = CardFormatValidation.ValidateDefinition(ValidFormat() with { Name = new string('A', 101) });
        await Assert.That(error).IsNotNull();
        await Assert.That(error).Contains("cannot exceed 100 characters");
    }

    [Test]
    public async Task ValidateDefinition_NoCardBits_ReturnsError()
    {
        var format = ValidFormat() with { CardMask = "XXXXXXXX", Parity1Mask = null, Parity2Mask = null };
        var error = CardFormatValidation.ValidateDefinition(format);

        await Assert.That(error).IsNotNull();
        await Assert.That(error).Contains("No card bits");
    }

    [Test]
    public async Task ValidateDefinition_NonContiguousCardBits_ReturnsError()
    {
        var format = ValidFormat() with { CardMask = "CCXXCCXX", Parity1Mask = null, Parity2Mask = null };
        var error = CardFormatValidation.ValidateDefinition(format);

        await Assert.That(error).IsNotNull();
        await Assert.That(error).Contains("contiguous");
    }

    [Test]
    public async Task ValidateDefinition_ShortParityMask_IsAccepted()
    {
        // A parity mask shorter than the card mask is right-aligned against it, excluding the
        // high bits from the range. Legacy definitions rely on this, so it must not be rejected.
        var format = ValidFormat() with { Parity1Mask = "PPPO", Parity2Mask = null };

        await Assert.That(CardFormatValidation.ValidateDefinition(format)).IsNull();
    }

    [Test]
    public async Task ValidateDefinition_ParityMaskWithPBitsButNoIndicator_ReturnsError()
    {
        // The encoder tolerates this shape; the analyzer is what stops it reaching the database,
        // since such a rule silently never writes anything.
        var error = CardFormatValidation.ValidateDefinition(ValidFormat() with { Parity1Mask = "PPXX" });

        await Assert.That(error).IsNotNull();
        await Assert.That(error).Contains("no E or O bit");
    }

    [Test]
    public async Task ValidateCredential_ValidCredential_ReturnsNull()
    {
        var error = CardFormatValidation.ValidateCredential(12345, 100, ValidFormat());
        await Assert.That(error).IsNull();
    }

    [Test]
    public async Task ValidateCredential_CardNumberOutOfRange_ReturnsError()
    {
        var error = CardFormatValidation.ValidateCredential(70_000, 100, ValidFormat());
        await Assert.That(error).IsNotNull();
        await Assert.That(error).Contains("exceeds");
    }

    [Test]
    public async Task ValidateCredential_NonzeroFacilityCodeOnFacilityLessFormat_ReturnsError()
    {
        var format = new CustomCardFormat(0, "No FC",
            "XCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCX",
            "EPPPPPPPPPPPPPPPPPPXXXXXXXXXXXXXXXXXX",
            "XXXXXXXXXXXXXXXXXXXPPPPPPPPPPPPPPPPPO",
            null, DateTimeOffset.UtcNow);

        var error = CardFormatValidation.ValidateCredential(100, 5, format);

        await Assert.That(error).IsNotNull();
        await Assert.That(error).Contains("no facility-code bits");
    }
}
