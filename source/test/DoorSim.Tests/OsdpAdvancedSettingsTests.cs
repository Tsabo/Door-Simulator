using System.Reflection;
using DoorSim.Data;
using DoorSim.Data.Entities;
using DoorSim.Hardware;
using DoorSim.Services;
using DoorSim.Shared.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DoorSim.Tests;

/// <summary>
/// Tests for the advanced OSDP settings: <see cref="OsdpAdvancedDefaults" /> helpers, the
/// <see cref="OsdpCapabilityMapper" /> wire bytes, entity mapping, and
/// <see cref="DoorConfigService" /> validation/persistence.
/// </summary>
/// <remarks>
/// <see cref="LoggingDevice" />'s handlers are deliberately untested: OSDP.Net grants no
/// InternalsVisibleTo and its <c>HandleCommand</c> is internal, so the overrides are
/// unreachable from here. That is why all the decision logic lives in
/// <see cref="OsdpCapabilityMapper" />, whose output this file pins byte for byte.
/// </remarks>
public class OsdpAdvancedSettingsTests
{
    // -------------------------------------------------------------------------
    // OsdpAdvancedDefaults
    // -------------------------------------------------------------------------

    [Test]
    public async Task ResolveConnectionTimeout_NullFallsBackToDefault() =>
        await Assert.That(OsdpAdvancedDefaults.ResolveConnectionTimeout(null))
            .IsEqualTo(TimeSpan.FromSeconds(20));

    [Test]
    public async Task ResolveReplyTimeout_NullFallsBackToDefault() =>
        await Assert.That(OsdpAdvancedDefaults.ResolveReplyTimeout(null))
            .IsEqualTo(TimeSpan.FromMilliseconds(2000));

    [Test]
    [Arguments(1)]
    [Arguments(45)]
    [Arguments(120)]
    public async Task ResolveConnectionTimeout_ExplicitValueIsUsed(int seconds) =>
        await Assert.That(OsdpAdvancedDefaults.ResolveConnectionTimeout(seconds))
            .IsEqualTo(TimeSpan.FromSeconds(seconds));

    [Test]
    [Arguments(50)]
    [Arguments(500)]
    [Arguments(10000)]
    public async Task ResolveReplyTimeout_ExplicitValueIsUsed(int milliseconds) =>
        await Assert.That(OsdpAdvancedDefaults.ResolveReplyTimeout(milliseconds))
            .IsEqualTo(TimeSpan.FromMilliseconds(milliseconds));

    [Test]
    [Arguments("00-00-01", 0x00, 0x00, 0x01)]
    [Arguments("FF-A1-02", 0xFF, 0xA1, 0x02)]
    [Arguments("000001", 0x00, 0x00, 0x01)]
    [Arguments("00 00 01", 0x00, 0x00, 0x01)]
    [Arguments("0x0000FF", 0x00, 0x00, 0xFF)]
    [Arguments("ff:a1:02", 0xFF, 0xA1, 0x02)]
    public async Task TryParseVendorCode_AcceptsSupportedForms(string text, byte b0, byte b1, byte b2)
    {
        await Assert.That(OsdpAdvancedDefaults.TryParseVendorCode(text, out var vendorCode)).IsTrue();
        await Assert.That(vendorCode).IsEquivalentTo(new[] { b0, b1, b2 });
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    public async Task TryParseVendorCode_NullOrBlankYieldsStockCode(string? text)
    {
        await Assert.That(OsdpAdvancedDefaults.TryParseVendorCode(text, out var vendorCode)).IsTrue();
        await Assert.That(vendorCode).IsEquivalentTo(new byte[] { 0x00, 0x00, 0x01 });
    }

    [Test]
    [Arguments("00-00")]
    [Arguments("ZZ-00-01")]
    [Arguments("00-00-01-02")]
    [Arguments("0000010")]
    [Arguments("not hex")]
    public async Task TryParseVendorCode_RejectsMalformed(string text) =>
        await Assert.That(OsdpAdvancedDefaults.TryParseVendorCode(text, out _)).IsFalse();

    [Test]
    public async Task FormatVendorCode_ProducesDashedHex() =>
        await Assert.That(OsdpAdvancedDefaults.FormatVendorCode([0x00, 0x00, 0x01])).IsEqualTo("00-00-01");

    [Test]
    [Arguments(1440, 0xA0, 0x05)]
    [Arguments(255, 255, 0)]
    [Arguments(256, 0, 1)]
    [Arguments(65535, 255, 255)]
    [Arguments(1, 1, 0)]
    public async Task SplitMessageSize_PutsLsbInComplianceAndMsbInNumberOf(int size, byte compliance, byte numberOf)
    {
        var (actualCompliance, actualNumberOf) = OsdpAdvancedDefaults.SplitMessageSize(size);
        await Assert.That(actualCompliance).IsEqualTo(compliance);
        await Assert.That(actualNumberOf).IsEqualTo(numberOf);
    }

    [Test]
    public async Task IsLegalLevel_NullIsAlwaysLegal() =>
        await Assert.That(OsdpAdvancedDefaults.IsLegalLevel(OsdpAdvancedDefaults.LedControlLevels, null)).IsTrue();

    // -------------------------------------------------------------------------
    // OsdpCapabilityMapper — osdp_CAP
    // -------------------------------------------------------------------------

    /// <summary>
    /// The upgrade-safety guard: a door with no advanced settings must advertise exactly the
    /// four capabilities, at exactly the bytes, the simulator sent before this feature existed.
    /// </summary>
    [Test]
    public async Task Capabilities_DefaultDoorMatchesLegacyFourEntries()
    {
        var capabilities = OsdpCapabilityMapper.BuildCapabilities(MakeOsdpDoor()).BuildData();

        await Assert.That(capabilities)
            .IsEquivalentTo(new byte[]
            {
                3, 1, 0, // CardDataFormat        — raw bits up to 1024
                4, 1, 1, // ReaderLEDControl      — 1 LED, on/off only
                8, 1, 0, // CheckCharacterSupport — CRC-16
                9, 0, 0 // CommunicationSecurity — no AES
            });
    }

    [Test]
    public async Task Capabilities_AreEmittedInAscendingFunctionCode()
    {
        var door = MakeOsdpDoor() with
        {
            OsdpCapOsdpVersion = 2,
            OsdpCapContactStatusCompliance = 1,
            OsdpCapDownstreamReaders = 1,
            OsdpCapOutputControlCompliance = 1,
            OsdpCapAudibleOutputCompliance = 1,
            OsdpCapTextOutputCompliance = 1,
            OsdpCapReceiveBufferSize = 256,
            OsdpCapLargestCombinedMessageSize = 512
        };

        var functionCodes = OsdpCapabilityMapper.BuildCapabilities(door)
            .BuildData()
            .Where((_, index) => index % 3 == 0)
            .ToArray();

        await Assert.That(functionCodes).IsEquivalentTo(new byte[] { 1, 2, 3, 4, 5, 6, 8, 9, 10, 11, 13, 16 });
    }

    [Test]
    public async Task Capabilities_OptInCapabilitiesAbsentFromDefaultDoor()
    {
        var functionCodes = FunctionCodes(MakeOsdpDoor());

        // ContactStatusMonitoring, OutputControl, ReaderAudibleOutput, ReaderTextOutput,
        // ReceiveBufferSize, LargestCombinedMessageSize, Readers, OSDPVersion.
        foreach (var absent in new byte[] { 1, 2, 5, 6, 10, 11, 13, 16 })
            await Assert.That(functionCodes).DoesNotContain(absent);
    }

    [Test]
    public async Task Capabilities_ContactStatusNumberOfFallsBackToTwoInputs()
    {
        var door = MakeOsdpDoor() with { OsdpCapContactStatusCompliance = 2 };
        await Assert.That(CapabilityBytes(door, 1)).IsEquivalentTo(new byte[] { 1, 2, 2 });
    }

    [Test]
    public async Task Capabilities_OutputControlNumberOfFallsBackToOneOutput()
    {
        var door = MakeOsdpDoor() with { OsdpCapOutputControlCompliance = 1 };
        await Assert.That(CapabilityBytes(door, 2)).IsEquivalentTo(new byte[] { 2, 1, 1 });
    }

    [Test]
    public async Task Capabilities_ExplicitNumberOfOverridesTheFallback()
    {
        var door = MakeOsdpDoor() with
        {
            OsdpCapContactStatusCompliance = 3,
            OsdpCapContactStatusInputs = 8
        };

        await Assert.That(CapabilityBytes(door, 1)).IsEquivalentTo(new byte[] { 1, 3, 8 });
    }

    /// <summary>
    /// Text output level 0 ("no text display support") is a legal declaration and is
    /// deliberately different from omitting the capability entirely — the distinction the
    /// nullable compliance exists to express.
    /// </summary>
    [Test]
    public async Task Capabilities_TextOutputComplianceZeroIsAdvertisedNotOmitted()
    {
        var door = MakeOsdpDoor() with { OsdpCapTextOutputCompliance = 0 };

        await Assert.That(FunctionCodes(door)).Contains(6);
        await Assert.That(CapabilityBytes(door, 6)).IsEquivalentTo(new byte[] { 6, 0, 0 });
    }

    [Test]
    public async Task Capabilities_AudibleOutputNumberOfIsAlwaysZero()
    {
        var door = MakeOsdpDoor() with { OsdpCapAudibleOutputCompliance = 2 };
        await Assert.That(CapabilityBytes(door, 5)).IsEquivalentTo(new byte[] { 5, 2, 0 });
    }

    [Test]
    [Arguments(false, false, (byte)0, (byte)0)]
    [Arguments(true, false, (byte)1, (byte)0)]
    [Arguments(true, true, (byte)1, (byte)1)]
    public async Task Capabilities_CommunicationSecurityEncodesAesBits(bool aes128, bool defaultKey, byte compliance, byte numberOf)
    {
        var door = MakeOsdpDoor() with
        {
            OsdpCapDeclareAes128 = aes128,
            OsdpCapDeclareDefaultAesKey = defaultKey
        };

        await Assert.That(CapabilityBytes(door, 9)).IsEquivalentTo(new[] { (byte)9, compliance, numberOf });
    }

    [Test]
    public async Task Capabilities_ReadersComplianceIsForcedToZero()
    {
        var door = MakeOsdpDoor() with { OsdpCapDownstreamReaders = 4 };
        await Assert.That(CapabilityBytes(door, 13)).IsEquivalentTo(new byte[] { 13, 0, 4 });
    }

    [Test]
    public async Task Capabilities_MessageSizesSplitAcrossComplianceAndNumberOf()
    {
        var door = MakeOsdpDoor() with
        {
            OsdpCapReceiveBufferSize = 1440,
            OsdpCapLargestCombinedMessageSize = 256
        };

        await Assert.That(CapabilityBytes(door, 10)).IsEquivalentTo(new byte[] { 10, 0xA0, 0x05 });
        await Assert.That(CapabilityBytes(door, 11)).IsEquivalentTo(new byte[] { 11, 0x00, 0x01 });
    }

    [Test]
    public async Task Capabilities_StockCapabilitiesRespectOverrides()
    {
        var door = MakeOsdpDoor() with
        {
            OsdpCapCardDataFormatCompliance = 3,
            OsdpCapLedControlCompliance = 4,
            OsdpCapLedsPerReader = 3,
            OsdpCapCheckCharacterCompliance = 0
        };

        await Assert.That(CapabilityBytes(door, 3)).IsEquivalentTo(new byte[] { 3, 3, 0 });
        await Assert.That(CapabilityBytes(door, 4)).IsEquivalentTo(new byte[] { 4, 4, 3 });
        await Assert.That(CapabilityBytes(door, 8)).IsEquivalentTo(new byte[] { 8, 0, 0 });
    }

    [Test]
    public async Task Capabilities_ManufacturerCommandReplyDoesNotAffectCapabilities()
    {
        var ack = OsdpCapabilityMapper.BuildCapabilities(MakeOsdpDoor()).BuildData();
        var nak = OsdpCapabilityMapper
            .BuildCapabilities(MakeOsdpDoor() with { OsdpNakManufacturerCommand = true })
            .BuildData();

        await Assert.That(nak).IsEquivalentTo(ack);
    }

    // -------------------------------------------------------------------------
    // OsdpCapabilityMapper — osdp_ID
    // -------------------------------------------------------------------------

    /// <summary>
    /// The other upgrade-safety guard: an unmodified door's osdp_ID reply must be
    /// byte-identical to the hardcoded one the simulator sent before this feature.
    /// </summary>
    [Test]
    public async Task Identification_DefaultDoorMatchesLegacyIdReport()
    {
        var door = MakeOsdpDoor() with { Id = 7 };
        var identification = OsdpCapabilityMapper.BuildIdentification(door).BuildData();

        await Assert.That(identification)
            .IsEquivalentTo(new byte[]
            {
                0x00, 0x00, 0x01, // vendor code
                1, // model number
                1, // hardware version
                7, 0x00, 0x00, 0x00, // serial number, little-endian door id
                1, 0, 0 // firmware major.minor.build
            });
    }

    [Test]
    public async Task Identification_OverridesAreApplied()
    {
        var door = MakeOsdpDoor() with
        {
            Id = 7,
            OsdpIdVendorCode = "AA-BB-CC",
            OsdpIdModelNumber = 42,
            OsdpIdHardwareVersion = 9,
            OsdpIdSerialNumber = 1,
            OsdpIdFirmwareMajor = 5,
            OsdpIdFirmwareMinor = 4,
            OsdpIdFirmwareBuild = 3
        };

        var identification = OsdpCapabilityMapper.BuildIdentification(door).BuildData();

        await Assert.That(identification)
            .IsEquivalentTo(new byte[]
            {
                0xAA, 0xBB, 0xCC,
                42,
                9,
                1, 0x00, 0x00, 0x00,
                5, 4, 3
            });
    }

    [Test]
    public async Task Identification_MalformedVendorCodeFallsBackToStock()
    {
        // Validation rejects this at the API boundary; a bad value already in the database
        // must never take the simulator down.
        var door = MakeOsdpDoor() with { OsdpIdVendorCode = "nonsense" };
        var identification = OsdpCapabilityMapper.BuildIdentification(door).BuildData();

        await Assert.That(identification.Take(3)).IsEquivalentTo(new byte[] { 0x00, 0x00, 0x01 });
    }

    [Test]
    public async Task ClientIdentification_VendorCodeAndSerialMatchIdReport()
    {
        var door = MakeOsdpDoor() with
        {
            Id = 3,
            OsdpIdVendorCode = "AA-BB-CC",
            OsdpIdSerialNumber = 99
        };

        var client = OsdpCapabilityMapper.BuildClientIdentification(door).ToBytes();
        var identification = OsdpCapabilityMapper.BuildIdentification(door).BuildData();

        // cUID layout is vendor code then serial number, the same leading bytes as osdp_ID
        // minus the model/version pair in between.
        await Assert.That(client.Take(3)).IsEquivalentTo(identification.Take(3));
        await Assert.That(client.Skip(3).Take(4)).IsEquivalentTo(new byte[] { 99, 0, 0, 0 });
    }

    [Test]
    public async Task ClientIdentification_SerialNumberFallsBackToDoorId()
    {
        var client = OsdpCapabilityMapper.BuildClientIdentification(MakeOsdpDoor() with { Id = 5 }).ToBytes();
        await Assert.That(client.Skip(3).Take(4)).IsEquivalentTo(new byte[] { 5, 0, 0, 0 });
    }

    // -------------------------------------------------------------------------
    // Entity mapping round-trip
    // -------------------------------------------------------------------------

    /// <summary>
    /// Reflection-driven so a field added to the DTO but forgotten in ApplyDto or ToDto fails
    /// here rather than silently failing to persist.
    /// </summary>
    [Test]
    public async Task EntityRoundTrip_PreservesEveryField()
    {
        var dto = MakeFullyPopulatedDoor();
        var roundTripped = DoorConfigEntity.FromDto(dto).ToDto();

        foreach (var property in typeof(DoorConfiguration).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var expected = property.GetValue(dto);
            var actual = property.GetValue(roundTripped);
            await Assert.That(actual).IsEqualTo(expected).Because($"{property.Name} did not survive the round trip");
        }
    }

    [Test]
    public async Task EntityRoundTrip_PreservesNullAdvancedFields()
    {
        var roundTripped = DoorConfigEntity.FromDto(MakeOsdpDoor()).ToDto();

        await Assert.That(roundTripped.OsdpCapContactStatusCompliance).IsNull();
        await Assert.That(roundTripped.OsdpCapDeclareAes128).IsFalse();
        await Assert.That(roundTripped.OsdpIdVendorCode).IsNull();
        await Assert.That(roundTripped.OsdpComsetHandling).IsEqualTo(OsdpComsetBehavior.Ignore);
        await Assert.That(roundTripped.OsdpConnectionTimeoutSeconds).IsNull();
        await Assert.That(roundTripped.OsdpReplyTimeoutMilliseconds).IsNull();
    }

    [Test]
    [Arguments(OsdpComsetBehavior.Ignore)]
    [Arguments(OsdpComsetBehavior.AcceptAddress)]
    [Arguments(OsdpComsetBehavior.Nak)]
    public async Task EntityRoundTrip_PreservesComsetBehaviour(OsdpComsetBehavior behavior)
    {
        var dto = MakeOsdpDoor() with { OsdpComsetHandling = behavior };
        var roundTripped = DoorConfigEntity.FromDto(dto).ToDto();
        await Assert.That(roundTripped.OsdpComsetHandling).IsEqualTo(behavior);
    }

    /// <summary>
    /// The schema differ adds the COMSET column as NULL to every pre-existing row, so a null
    /// or blank value must resolve to the original behaviour rather than failing to load.
    /// </summary>
    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    [Arguments("something-unrecognised")]
    public async Task Entity_UnsetComsetBehaviourFallsBackToIgnore(string? stored)
    {
        var entity = DoorConfigEntity.FromDto(MakeOsdpDoor());
        entity.OsdpComsetHandling = stored;

        await Assert.That(entity.ToDto().OsdpComsetHandling).IsEqualTo(OsdpComsetBehavior.Ignore);
    }

    // -------------------------------------------------------------------------
    // DoorConfigService validation
    // -------------------------------------------------------------------------

    [Test]
    [Arguments("OsdpCapCardDataFormatCompliance", (byte)4)]
    [Arguments("OsdpCapLedControlCompliance", (byte)5)]
    [Arguments("OsdpCapCheckCharacterCompliance", (byte)2)]
    [Arguments("OsdpCapContactStatusCompliance", (byte)5)]
    [Arguments("OsdpCapOutputControlCompliance", (byte)5)]
    [Arguments("OsdpCapAudibleOutputCompliance", (byte)3)]
    [Arguments("OsdpCapTextOutputCompliance", (byte)4)]
    [Arguments("OsdpCapOsdpVersion", (byte)3)]
    public async Task Create_RejectsIllegalComplianceLevel(string propertyName, byte value)
    {
        using var ctx = NewDb();
        var svc = new DoorConfigService(ctx.Db);

        var (result, error) = await svc.CreateAsync(WithProperty(MakeOsdpDoor(), propertyName, value));

        await Assert.That(result).IsNull();
        await Assert.That(error).IsNotNull();
        await Assert.That(error!).Contains("compliance level");
    }

    /// <summary>
    /// Iterates the same tables the edit dialog renders, so the dialog cannot offer a value
    /// the server rejects.
    /// </summary>
    [Test]
    public async Task Create_AcceptsEveryLegalComplianceLevel()
    {
        (string Property, OsdpCapabilityLevel[] Levels)[] cases =
        [
            ("OsdpCapCardDataFormatCompliance", OsdpAdvancedDefaults.CardDataFormatLevels),
            ("OsdpCapLedControlCompliance", OsdpAdvancedDefaults.LedControlLevels),
            ("OsdpCapCheckCharacterCompliance", OsdpAdvancedDefaults.CheckCharacterLevels),
            ("OsdpCapContactStatusCompliance", OsdpAdvancedDefaults.ContactStatusLevels),
            ("OsdpCapOutputControlCompliance", OsdpAdvancedDefaults.OutputControlLevels),
            ("OsdpCapAudibleOutputCompliance", OsdpAdvancedDefaults.AudibleOutputLevels),
            ("OsdpCapTextOutputCompliance", OsdpAdvancedDefaults.TextOutputLevels),
            ("OsdpCapOsdpVersion", OsdpAdvancedDefaults.OsdpVersionLevels)
        ];

        foreach (var (property, levels) in cases)
        {
            foreach (var level in levels)
            {
                using var ctx = NewDb();
                var svc = new DoorConfigService(ctx.Db);

                var (result, error) = await svc.CreateAsync(WithProperty(MakeOsdpDoor(), property, level.Value));

                await Assert.That(error).IsNull().Because($"{property} = {level.Value} should be legal");
                await Assert.That(result).IsNotNull();
            }
        }
    }

    [Test]
    [Arguments("OsdpCapContactStatusInputs")]
    [Arguments("OsdpCapOutputControlCount")]
    [Arguments("OsdpCapLedsPerReader")]
    public async Task Create_RejectsZeroDeclaredCount(string propertyName)
    {
        using var ctx = NewDb();
        var svc = new DoorConfigService(ctx.Db);

        // Pair the count with a compliance level so the orphan check isn't what trips first.
        var dto = MakeOsdpDoor() with
        {
            OsdpCapContactStatusCompliance = 1,
            OsdpCapOutputControlCompliance = 1
        };

        var (result, error) = await svc.CreateAsync(WithProperty(dto, propertyName, (byte)0));

        await Assert.That(result).IsNull();
        await Assert.That(error!).Contains("at least 1");
    }

    [Test]
    [Arguments("OsdpCapContactStatusInputs")]
    [Arguments("OsdpCapOutputControlCount")]
    [Arguments("OsdpCapTextOutputDisplays")]
    public async Task Create_RejectsDeclaredCountWithoutCompliance(string propertyName)
    {
        using var ctx = NewDb();
        var svc = new DoorConfigService(ctx.Db);

        var (result, error) = await svc.CreateAsync(WithProperty(MakeOsdpDoor(), propertyName, (byte)2));

        await Assert.That(result).IsNull();
        await Assert.That(error!).Contains("compliance level");
    }

    [Test]
    public async Task Create_RejectsDefaultAesKeyWithoutAes128()
    {
        using var ctx = NewDb();
        var svc = new DoorConfigService(ctx.Db);

        var dto = MakeOsdpDoor() with { OsdpCapDeclareDefaultAesKey = true };
        var (result, error) = await svc.CreateAsync(dto);

        await Assert.That(result).IsNull();
        await Assert.That(error!).Contains("AES-128 support");
    }

    [Test]
    public async Task Create_AcceptsDefaultAesKeyWithAes128()
    {
        using var ctx = NewDb();
        var svc = new DoorConfigService(ctx.Db);

        var dto = MakeOsdpDoor() with
        {
            OsdpCapDeclareAes128 = true,
            OsdpCapDeclareDefaultAesKey = true
        };

        var (_, error) = await svc.CreateAsync(dto);
        await Assert.That(error).IsNull();
    }

    [Test]
    [Arguments("00-00")]
    [Arguments("ZZ-00-01")]
    [Arguments("00-00-01-02")]
    public async Task Create_RejectsMalformedVendorCode(string vendorCode)
    {
        using var ctx = NewDb();
        var svc = new DoorConfigService(ctx.Db);

        var (result, error) = await svc.CreateAsync(MakeOsdpDoor() with { OsdpIdVendorCode = vendorCode });

        await Assert.That(result).IsNull();
        await Assert.That(error!).Contains("vendor code");
    }

    [Test]
    [Arguments(0)]
    [Arguments(-1)]
    [Arguments(65536)]
    public async Task Create_RejectsOutOfRangeMessageSize(int size)
    {
        using var ctx = NewDb();
        var svc = new DoorConfigService(ctx.Db);

        var (result, error) = await svc.CreateAsync(MakeOsdpDoor() with { OsdpCapReceiveBufferSize = size });

        await Assert.That(result).IsNull();
        await Assert.That(error!).Contains("Receive buffer size");
    }

    [Test]
    [Arguments(0)]
    [Arguments(121)]
    public async Task Create_RejectsOutOfRangeConnectionTimeout(int seconds)
    {
        using var ctx = NewDb();
        var svc = new DoorConfigService(ctx.Db);

        var (result, error) = await svc.CreateAsync(
            MakeOsdpDoor() with { OsdpConnectionTimeoutSeconds = seconds });

        await Assert.That(result).IsNull();
        await Assert.That(error!).Contains("connection timeout");
    }

    [Test]
    [Arguments(49)]
    [Arguments(10001)]
    public async Task Create_RejectsOutOfRangeReplyTimeout(int milliseconds)
    {
        using var ctx = NewDb();
        var svc = new DoorConfigService(ctx.Db);

        var (result, error) = await svc.CreateAsync(
            MakeOsdpDoor() with { OsdpReplyTimeoutMilliseconds = milliseconds });

        await Assert.That(result).IsNull();
        await Assert.That(error!).Contains("reply timeout");
    }

    /// <summary>
    /// A reply timeout at or above the connection timeout guarantees the link reports itself
    /// disconnected — the documented root cause of a real production bug. The mixed cases
    /// matter: the rule compares resolved values, so setting only one side still trips it.
    /// </summary>
    [Test]
    [Arguments(5, 5000)]
    [Arguments(5, 6000)]
    [Arguments(1, 2000)] // reply left at its 2000ms default
    [Arguments(2, 2000)]
    public async Task Create_RejectsReplyTimeoutNotShorterThanConnectionTimeout(int connectionSeconds, int? replyMilliseconds)
    {
        using var ctx = NewDb();
        var svc = new DoorConfigService(ctx.Db);

        var dto = MakeOsdpDoor() with
        {
            OsdpConnectionTimeoutSeconds = connectionSeconds,
            OsdpReplyTimeoutMilliseconds = replyMilliseconds
        };

        var (result, error) = await svc.CreateAsync(dto);

        await Assert.That(result).IsNull();
        await Assert.That(error!).Contains("shorter than the connection timeout");
    }

    [Test]
    public async Task Create_AcceptsFullyPopulatedAdvancedDoor()
    {
        using var ctx = NewDb();
        var svc = new DoorConfigService(ctx.Db);

        var (result, error) = await svc.CreateAsync(MakeFullyPopulatedDoor() with { Id = 0 });

        await Assert.That(error).IsNull();
        await Assert.That(result).IsNotNull();
    }

    [Test]
    public async Task Wiegand_AdvancedOsdpFieldsAreNotValidated()
    {
        using var ctx = NewDb();
        var svc = new DoorConfigService(ctx.Db);

        // Nonsense advanced values on a Wiegand door are irrelevant — the fields are unused.
        var dto = MakeOsdpDoor() with
        {
            Protocol = ProtocolType.Wiegand,
            D0Pin = 4,
            D1Pin = 17,
            OsdpAddress = null,
            OsdpSerialPort = null,
            OsdpCapLedControlCompliance = 99,
            OsdpCapDeclareDefaultAesKey = true,
            OsdpIdVendorCode = "nonsense",
            OsdpConnectionTimeoutSeconds = 9999
        };

        var (result, error) = await svc.CreateAsync(dto);

        await Assert.That(error).IsNull();
        await Assert.That(result).IsNotNull();
    }

    /// <summary>
    /// Reflection-driven guard over the update path, which historically hand-copied every
    /// field: an omission there is invisible, since create works and only update no-ops.
    /// </summary>
    [Test]
    public async Task Update_PersistsEveryAdvancedField()
    {
        using var ctx = NewDb();
        var svc = new DoorConfigService(ctx.Db);

        var (created, createError) = await svc.CreateAsync(MakeOsdpDoor());
        await Assert.That(createError).IsNull();

        var target = MakeFullyPopulatedDoor() with { Id = created!.Id };
        var (updated, error) = await svc.UpdateAsync(created.Id, target);

        await Assert.That(error).IsNull();
        await Assert.That(updated).IsNotNull();

        // Confirm it actually reached the database, not just the returned DTO.
        var reloaded = await svc.GetAsync(created.Id);

        foreach (var property in typeof(DoorConfiguration).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var expected = property.GetValue(target);
            var actual = property.GetValue(reloaded);
            await Assert.That(actual).IsEqualTo(expected).Because($"{property.Name} was not persisted by UpdateAsync");
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Create_RoundTripsManufacturerCommandReply(bool nak)
    {
        using var ctx = NewDb();
        var svc = new DoorConfigService(ctx.Db);

        var (created, error) = await svc.CreateAsync(
            MakeOsdpDoor() with { OsdpNakManufacturerCommand = nak });

        await Assert.That(error).IsNull();
        await Assert.That(created!.OsdpNakManufacturerCommand).IsEqualTo(nak);

        var reloaded = await svc.GetAsync(created.Id);
        await Assert.That(reloaded!.OsdpNakManufacturerCommand).IsEqualTo(nak);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>The three capability bytes for one function code, or empty if not advertised.</summary>
    private static byte[] CapabilityBytes(DoorConfiguration door, byte functionCode)
    {
        var data = OsdpCapabilityMapper.BuildCapabilities(door).BuildData();

        for (var i = 0; i < data.Length; i += 3)
        {
            if (data[i] == functionCode)
                return [data[i], data[i + 1], data[i + 2]];
        }

        return [];
    }

    private static byte[] FunctionCodes(DoorConfiguration door) =>
    [
        .. OsdpCapabilityMapper.BuildCapabilities(door)
            .BuildData()
            .Where((_, index) => index % 3 == 0)
    ];

    /// <summary>Sets one advanced property by name, so a table-driven test can span fields.</summary>
    private static DoorConfiguration WithProperty(DoorConfiguration door, string propertyName, object value)
    {
        // Records expose a copy constructor only through `with`, so go via the entity, which
        // is mutable and shares the same property names.
        var entity = DoorConfigEntity.FromDto(door);
        var property = typeof(DoorConfigEntity).GetProperty(propertyName)
                       ?? throw new ArgumentException($"No such property: {propertyName}", nameof(propertyName));

        property.SetValue(entity, Convert.ChangeType(value, Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType));

        return entity.ToDto();
    }

    private static DoorConfiguration MakeOsdpDoor() => new(
        0,
        "Test Reader",
        ProtocolType.Osdp,
        null,
        null,
        0,
        "/dev/ttyRS485_1_1",
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null);

    /// <summary>
    /// Every advanced field set to a distinct, legal, non-default value, so a reflection sweep
    /// can tell a dropped field from a coincidentally-matching default.
    /// </summary>
    private static DoorConfiguration MakeFullyPopulatedDoor() => MakeOsdpDoor() with
    {
        OsdpBaudRate = 19200,
        OsdpNakManufacturerCommand = true,

        OsdpCapContactStatusCompliance = 2,
        OsdpCapContactStatusInputs = 4,
        OsdpCapOutputControlCompliance = 3,
        OsdpCapOutputControlCount = 5,
        OsdpCapAudibleOutputCompliance = 2,
        OsdpCapTextOutputCompliance = 3,
        OsdpCapTextOutputDisplays = 6,
        OsdpCapCardDataFormatCompliance = 3,
        OsdpCapLedControlCompliance = 4,
        OsdpCapLedsPerReader = 7,
        OsdpCapCheckCharacterCompliance = 0,
        OsdpCapDeclareAes128 = true,
        OsdpCapDeclareDefaultAesKey = true,
        OsdpCapReceiveBufferSize = 1440,
        OsdpCapLargestCombinedMessageSize = 2048,
        OsdpCapOsdpVersion = 2,
        OsdpCapDownstreamReaders = 8,

        OsdpIdVendorCode = "AA-BB-CC",
        OsdpIdModelNumber = 42,
        OsdpIdHardwareVersion = 9,
        OsdpIdSerialNumber = 123456,
        OsdpIdFirmwareMajor = 5,
        OsdpIdFirmwareMinor = 4,
        OsdpIdFirmwareBuild = 3,

        OsdpComsetHandling = OsdpComsetBehavior.AcceptAddress,
        OsdpConnectionTimeoutSeconds = 30,
        OsdpReplyTimeoutMilliseconds = 1500
    };

    private static DbFixture NewDb() => new();

    /// <summary>In-memory SQLite context; the connection must outlive the DbContext.</summary>
    private sealed class DbFixture : IDisposable
    {
        private readonly SqliteConnection _connection;

        public DbFixture()
        {
            _connection = new SqliteConnection("Filename=:memory:");
            _connection.Open();
            Db = new DoorSimDbContext(
                new DbContextOptionsBuilder<DoorSimDbContext>()
                    .UseSqlite(_connection)
                    .Options);

            Db.Database.EnsureCreated();
        }

        public DoorSimDbContext Db { get; }

        public void Dispose()
        {
            Db.Dispose();
            _connection.Dispose();
        }
    }
}
