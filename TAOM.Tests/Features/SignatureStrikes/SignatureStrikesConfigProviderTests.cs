using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.SignatureStrikes;

namespace TAOM.Tests.Features.SignatureStrikes;

/// <summary>
/// One test per validation rule (csharp-architecture.md "Config Providers MUST Validate"). The
/// syntax cases are the easy half; the half that matters is a value an author actually types:
/// a NaN, a radius in centimetres, an inner radius outside the outer one, a direction name the
/// service would never match.
/// </summary>
[TestClass]
public class SignatureStrikesConfigProviderTests
{
    private string _tempDir = null!;
    private string _featureDir = null!;
    private IModLogger _logger = null!;
    private SignatureStrikesConfigProvider _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "TAOM_SignatureStrikes_" + Path.GetRandomFileName());
        _featureDir = Path.Combine(_tempDir, "signature_strikes");
        Directory.CreateDirectory(_featureDir);

        var pathService = Substitute.For<IPathService>();
        pathService.ModuleDataPath.Returns(_tempDir);
        _logger = Substitute.For<IModLogger>();

        _sut = new SignatureStrikesConfigProvider(pathService, _logger);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private void WriteConfig(string json) =>
        File.WriteAllText(Path.Combine(_featureDir, "signature_strikes_config.json"), json);

    // A full, valid file with one field swapped in by the caller.
    private void WriteConfigWith(string overrideJson) => WriteConfig(@"{
        ""enabled"": true,
        ""heroIds"": [""lord_1_17""],
        ""races"": [""sauron""],
        ""slamCooldownSeconds"": 20,
        ""sweepCooldownSeconds"": 12,
        ""shieldBlockedMultiplier"": 0.25,
        ""strikes"": {
            ""Overhead"": { ""kind"": ""Slam"", ""outerRadius"": 4.0, ""innerRadius"": 1.5, ""damageFraction"": 0.6, ""worldHitBaseDamage"": 60, ""magnitude"": 80, ""knockDown"": true, ""knockBack"": false, ""fearMorale"": 15 },
            ""Left"": { ""kind"": ""Sweep"", ""outerRadius"": 3.0, ""innerRadius"": 1.5, ""damageFraction"": 0.25, ""worldHitBaseDamage"": 0, ""magnitude"": 60, ""knockDown"": false, ""knockBack"": true, ""fearMorale"": 0 }
        }," + overrideJson + "}");

    private void AssertRejected(string fieldFragment)
    {
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains(fieldFragment)));
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("contained invalid values")));
    }

    // ---- Syntax-level failures --------------------------------------------

    [TestMethod]
    public void GetConfig_MissingFile_ReturnsDefaultsAndWarns()
    {
        var config = _sut.GetConfig();

        Assert.IsTrue(config.Enabled);
        Assert.AreEqual(20f, config.SlamCooldownSeconds, 0.001f);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("not found")));
    }

    [TestMethod]
    public void GetConfig_MalformedJson_ReturnsDefaultsAndLogsError()
    {
        WriteConfig("{ this is not json ");

        var config = _sut.GetConfig();

        Assert.IsTrue(config.Enabled);
        _logger.Received().LogError(Arg.Is<string>(m => m.Contains("parse")));
    }

    [TestMethod]
    public void GetConfig_EmptyObject_ReturnsDefaults()
    {
        WriteConfig("{}");

        var config = _sut.GetConfig();

        Assert.AreEqual(20f, config.SlamCooldownSeconds, 0.001f);
        Assert.AreEqual(12f, config.SweepCooldownSeconds, 0.001f);
        Assert.IsTrue(config.Strikes.ContainsKey("Overhead"));
        Assert.IsTrue(config.Strikes.ContainsKey("Left"));
        Assert.IsTrue(config.Strikes.ContainsKey("Right"));
    }

    [TestMethod]
    public void GetConfig_ValidFile_LoadsWithoutRejection()
    {
        WriteConfigWith(@"""_comment"": ""x""");

        var config = _sut.GetConfig();

        Assert.AreEqual(2, config.Strikes.Count);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
        _logger.Received().LogInfo(Arg.Is<string>(m => m.Contains("Loaded")));
    }

    [TestMethod]
    public void GetConfig_EnabledFalse_PassesThrough()
    {
        WriteConfig(@"{ ""enabled"": false }");

        Assert.IsFalse(_sut.GetConfig().Enabled);
    }

    // ---- Top-level floats --------------------------------------------------

    [TestMethod]
    public void GetConfig_NaNSlamCooldown_RevertsAndWarns()
    {
        WriteConfigWith(@"""slamCooldownSeconds"": NaN");

        var config = _sut.GetConfig();

        Assert.AreEqual(20f, config.SlamCooldownSeconds, 0.001f);
        AssertRejected("slamCooldownSeconds");
    }

    [TestMethod]
    public void GetConfig_SlamCooldownAboveCeiling_Reverts()
    {
        WriteConfigWith(@"""slamCooldownSeconds"": 500");

        Assert.AreEqual(20f, _sut.GetConfig().SlamCooldownSeconds, 0.001f);
        AssertRejected("slamCooldownSeconds");
    }

    [TestMethod]
    public void GetConfig_NegativeSlamCooldown_Reverts()
    {
        WriteConfigWith(@"""slamCooldownSeconds"": -1");

        Assert.AreEqual(20f, _sut.GetConfig().SlamCooldownSeconds, 0.001f);
        AssertRejected("slamCooldownSeconds");
    }

    [TestMethod]
    public void GetConfig_ZeroSlamCooldown_RevertsBecauseTheFloorKeepsOnePackagePerSwing()
    {
        // Codex review 114, O3: with no cooldown the second body of one cleaving swing would
        // ring again. The floor (0.5 s) is what makes "one package per swing" hold for every
        // config the provider accepts.
        WriteConfigWith(@"""slamCooldownSeconds"": 0");

        Assert.AreEqual(20f, _sut.GetConfig().SlamCooldownSeconds, 0.001f);
        AssertRejected("slamCooldownSeconds");
    }

    [TestMethod]
    public void GetConfig_NumericDirectionKey_IsDroppedNotNormalised()
    {
        // Codex review 114, F2: "1" is StrikeDirection.Overhead by value; only the member name
        // may address a row.
        WriteConfigWith(@"""strikes"": { ""1"": { ""kind"": ""Slam"" } }");

        Assert.AreEqual(0, _sut.GetConfig().Strikes.Count);
        AssertRejected("'1'");
    }

    [TestMethod]
    public void GetConfig_NumericKind_IsDroppedNotNormalised()
    {
        WriteConfigWith(@"""strikes"": { ""Overhead"": { ""kind"": ""1"" } }");

        Assert.IsFalse(_sut.GetConfig().Strikes.ContainsKey("Overhead"));
        AssertRejected("'1'");
    }

    [TestMethod]
    public void GetConfig_InfiniteSweepCooldown_Reverts()
    {
        WriteConfigWith(@"""sweepCooldownSeconds"": Infinity");

        Assert.AreEqual(12f, _sut.GetConfig().SweepCooldownSeconds, 0.001f);
        AssertRejected("sweepCooldownSeconds");
    }

    [TestMethod]
    public void GetConfig_ShieldBlockedMultiplierAboveOne_Reverts()
    {
        // Above 1.0 would make a shield block HURT MORE than a clean hit: the sign trap in
        // multiplier form.
        WriteConfigWith(@"""shieldBlockedMultiplier"": 1.5");

        Assert.AreEqual(0.25f, _sut.GetConfig().ShieldBlockedMultiplier, 0.001f);
        AssertRejected("shieldBlockedMultiplier");
    }

    // ---- Lists ---------------------------------------------------------------

    [TestMethod]
    public void GetConfig_NullHeroIds_RevertsAndWarns()
    {
        WriteConfigWith(@"""heroIds"": null");

        CollectionAssert.Contains(_sut.GetConfig().HeroIds, "lord_1_17");
        AssertRejected("heroIds");
    }

    [TestMethod]
    public void GetConfig_HeroIdsReplaceTheDefaultsRatherThanAppending()
    {
        WriteConfigWith(@"""heroIds"": [""lord_1_15""]");

        CollectionAssert.AreEquivalent(new[] { "lord_1_15" }, _sut.GetConfig().HeroIds);
    }

    [TestMethod]
    public void GetConfig_EmptyRaces_IsALegitimateSwitchOff()
    {
        WriteConfigWith(@"""races"": []");

        Assert.AreEqual(0, _sut.GetConfig().Races.Count);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    // ---- Strike profiles -------------------------------------------------------

    [TestMethod]
    public void GetConfig_NullStrikes_RevertsToDefaults()
    {
        WriteConfigWith(@"""strikes"": null");

        var config = _sut.GetConfig();

        Assert.IsTrue(config.Strikes.ContainsKey("Overhead"));
        AssertRejected("strikes");
    }

    [TestMethod]
    public void GetConfig_EmptyStrikes_IsInertNotInvalid()
    {
        WriteConfigWith(@"""strikes"": {}");

        Assert.AreEqual(0, _sut.GetConfig().Strikes.Count);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void GetConfig_UnknownDirectionKey_IsSkippedAndWarned()
    {
        // Parsed-but-unresolvable (the M1 trap): the service would never match "Diagonal", so a
        // silently kept row is a row the author believes is live and is not.
        WriteConfigWith(@"""strikes"": { ""Diagonal"": { ""kind"": ""Slam"" }, ""Overhead"": { ""kind"": ""Slam"" } }");

        var config = _sut.GetConfig();

        Assert.IsFalse(config.Strikes.ContainsKey("Diagonal"));
        Assert.IsTrue(config.Strikes.ContainsKey("Overhead"));
        AssertRejected("Diagonal");
    }

    [TestMethod]
    public void GetConfig_DirectionKeyIsNormalisedToTheCanonicalSpelling()
    {
        WriteConfigWith(@"""strikes"": { ""overhead"": { ""kind"": ""Slam"" } }");

        Assert.IsTrue(_sut.GetConfig().Strikes.ContainsKey("Overhead"));
    }

    [TestMethod]
    public void GetConfig_UnknownKind_IsSkippedAndWarned()
    {
        WriteConfigWith(@"""strikes"": { ""Overhead"": { ""kind"": ""Nova"" } }");

        var config = _sut.GetConfig();

        Assert.IsFalse(config.Strikes.ContainsKey("Overhead"));
        AssertRejected("Nova");
    }

    [TestMethod]
    public void GetConfig_NullProfile_IsSkippedAndWarned()
    {
        WriteConfigWith(@"""strikes"": { ""Overhead"": null }");

        Assert.IsFalse(_sut.GetConfig().Strikes.ContainsKey("Overhead"));
        AssertRejected("Overhead");
    }

    [TestMethod]
    public void GetConfig_NaNOuterRadius_Reverts()
    {
        WriteConfigWith(@"""strikes"": { ""Overhead"": { ""kind"": ""Slam"", ""outerRadius"": NaN } }");

        Assert.AreEqual(4f, _sut.GetConfig().Strikes["Overhead"].OuterRadius, 0.001f);
        AssertRejected("outerRadius");
    }

    [TestMethod]
    public void GetConfig_OuterRadiusInCentimetres_Reverts()
    {
        WriteConfigWith(@"""strikes"": { ""Overhead"": { ""kind"": ""Slam"", ""outerRadius"": 400 } }");

        Assert.AreEqual(4f, _sut.GetConfig().Strikes["Overhead"].OuterRadius, 0.001f);
        AssertRejected("outerRadius");
    }

    [TestMethod]
    public void GetConfig_InnerRadiusAboveOuter_RevertsInnerToFitTheValidatedOuter()
    {
        WriteConfigWith(@"""strikes"": { ""Overhead"": { ""kind"": ""Slam"", ""outerRadius"": 2.0, ""innerRadius"": 3.0 } }");

        var profile = _sut.GetConfig().Strikes["Overhead"];

        Assert.AreEqual(2f, profile.OuterRadius, 0.001f);
        Assert.IsTrue(profile.InnerRadius <= profile.OuterRadius);
        AssertRejected("innerRadius");
    }

    [TestMethod]
    public void GetConfig_DamageFractionAboveOne_Reverts()
    {
        WriteConfigWith(@"""strikes"": { ""Overhead"": { ""kind"": ""Slam"", ""damageFraction"": 2.0 } }");

        Assert.AreEqual(0.6f, _sut.GetConfig().Strikes["Overhead"].DamageFraction, 0.001f);
        AssertRejected("damageFraction");
    }

    [TestMethod]
    public void GetConfig_NegativeMagnitude_Reverts()
    {
        WriteConfigWith(@"""strikes"": { ""Overhead"": { ""kind"": ""Slam"", ""magnitude"": -5 } }");

        Assert.AreEqual(80f, _sut.GetConfig().Strikes["Overhead"].Magnitude, 0.001f);
        AssertRejected("magnitude");
    }

    [TestMethod]
    public void GetConfig_FearMoraleAboveCeiling_Reverts()
    {
        WriteConfigWith(@"""strikes"": { ""Overhead"": { ""kind"": ""Slam"", ""fearMorale"": 200 } }");

        Assert.AreEqual(15f, _sut.GetConfig().Strikes["Overhead"].FearMorale, 0.001f);
        AssertRejected("fearMorale");
    }

    [TestMethod]
    public void GetConfig_WorldHitBaseDamageAboveCeiling_Reverts()
    {
        WriteConfigWith(@"""strikes"": { ""Overhead"": { ""kind"": ""Slam"", ""worldHitBaseDamage"": 9999 } }");

        Assert.AreEqual(60, _sut.GetConfig().Strikes["Overhead"].WorldHitBaseDamage);
        AssertRejected("worldHitBaseDamage");
    }

    [TestMethod]
    public void GetConfig_ProfileFieldsRevertToTheDirectionsOwnDefaults()
    {
        // A bad Left value reverts to the LEFT default, not the Overhead one.
        WriteConfigWith(@"""strikes"": { ""Left"": { ""kind"": ""Sweep"", ""outerRadius"": -1 } }");

        Assert.AreEqual(3f, _sut.GetConfig().Strikes["Left"].OuterRadius, 0.001f);
    }

    [TestMethod]
    public void GetConfig_SummaryWarningFiresOnceForSeveralRejections()
    {
        WriteConfigWith(@"""slamCooldownSeconds"": -1, ""sweepCooldownSeconds"": -1");

        _sut.GetConfig();

        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("contained invalid values")));
    }
}
