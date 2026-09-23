using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.SignatureStrikes;
using TAOM.Features.SignatureStrikes.Domain;

namespace TAOM.Tests.Features.SignatureStrikes;

/// <summary>
/// One test per validation rule (csharp-architecture.md "Config Providers MUST Validate"). The
/// syntax cases are the easy half; the half that matters is a value an author actually types:
/// a NaN, a radius in centimetres, an inner radius outside the outer one, a direction name the
/// service would never match, a signature with no one in it, a strike whose kind has no cooldown.
/// </summary>
[TestClass]
public class SignatureStrikesConfigProviderTests
{
    // Sauron's signature as shipped, minus the Right row. Callers append fields after it; Json.NET
    // keeps the LAST duplicate key, so an appended field replaces the one here.
    private const string SauronBody = @"""id"": ""sauron"",
        ""heroIds"": [""lord_1_17""],
        ""races"": [""sauron""],
        ""cooldowns"": { ""Slam"": 20, ""Sweep"": 12 },
        ""strikes"": {
            ""Overhead"": { ""kind"": ""Slam"", ""origin"": ""Impact"", ""outerRadius"": 4.0, ""innerRadius"": 1.5, ""damageFraction"": 0.6, ""worldHitBaseDamage"": 60, ""magnitude"": 80, ""knockDown"": true, ""knockBack"": false, ""fearMorale"": 15 },
            ""Left"": { ""kind"": ""Sweep"", ""outerRadius"": 3.0, ""innerRadius"": 1.5, ""damageFraction"": 0.25, ""worldHitBaseDamage"": 0, ""magnitude"": 60, ""knockDown"": false, ""knockBack"": true, ""fearMorale"": 0 }
        }";

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

    // A full, valid file holding Sauron's signature, with signature-level fields appended.
    private void WriteSauronWith(string signatureOverride = "") => WriteConfig(
        @"{ ""enabled"": true, ""shieldBlockedMultiplier"": 0.25, ""signatures"": [ { " + SauronBody
        + (signatureOverride.Length > 0 ? ", " + signatureOverride : "") + " } ] }");

    private void WriteStrikes(string strikesJson) => WriteSauronWith(@"""strikes"": " + strikesJson);

    private SignatureConfig Only() => _sut.GetConfig().Signatures.Single();

    private void AssertRejected(string fieldFragment)
    {
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains(fieldFragment)));
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("contained invalid values")));
    }

    // ---- Syntax-level failures --------------------------------------------

    [TestMethod]
    public void GetConfig_MissingFile_ReturnsBothDefaultSignaturesAndWarns()
    {
        var config = _sut.GetConfig();

        Assert.IsTrue(config.Enabled);
        CollectionAssert.AreEqual(new[] { "sauron", "nazgul" }, config.Signatures.Select(s => s.Id).ToArray());
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("not found")));
    }

    [TestMethod]
    public void GetConfig_MalformedJson_ReturnsDefaultsAndLogsError()
    {
        WriteConfig("{ this is not json ");

        var config = _sut.GetConfig();

        Assert.AreEqual(2, config.Signatures.Count);
        _logger.Received().LogError(Arg.Is<string>(m => m.Contains("parse")));
    }

    [TestMethod]
    public void GetConfig_EmptyObject_ReturnsTheCompiledDefaults()
    {
        WriteConfig("{}");

        var config = _sut.GetConfig();
        var sauron = config.Signatures[0];
        var nazgul = config.Signatures[1];

        Assert.AreEqual(20f, sauron.Cooldowns["Slam"], 0.001f);
        Assert.AreEqual(12f, sauron.Cooldowns["Sweep"], 0.001f);
        CollectionAssert.AreEquivalent(new[] { "Overhead", "Left", "Right" }, sauron.Strikes.Keys.ToArray());
        Assert.AreEqual(15f, nazgul.Cooldowns["Scream"], 0.001f);
        CollectionAssert.AreEquivalent(new[] { "Overhead", "Left", "Right" }, nazgul.Strikes.Keys.ToArray());
        Assert.IsTrue(nazgul.Strikes.Values.All(p => p.Kind == "Scream" && p.Origin == "Self"));
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void GetConfig_ValidFile_LoadsWithoutRejection()
    {
        WriteSauronWith();

        var only = Only();

        Assert.AreEqual(2, only.Strikes.Count);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
        _logger.Received().LogInfo(Arg.Is<string>(m => m.Contains("Loaded")));
    }

    [TestMethod]
    public void GetConfig_EnabledFalse_PassesThrough()
    {
        WriteConfig(@"{ ""enabled"": false }");

        Assert.IsFalse(_sut.GetConfig().Enabled);
    }

    [TestMethod]
    public void GetConfig_ShieldBlockedMultiplierAboveOne_Reverts()
    {
        // Above 1.0 would make a shield block HURT MORE than a clean hit: the sign trap in
        // multiplier form.
        WriteConfig(@"{ ""shieldBlockedMultiplier"": 1.5 }");

        Assert.AreEqual(0.25f, _sut.GetConfig().ShieldBlockedMultiplier, 0.001f);
        AssertRejected("shieldBlockedMultiplier");
    }

    // ---- The signatures list -----------------------------------------------

    [TestMethod]
    public void GetConfig_NullSignatures_RevertsToDefaults()
    {
        WriteConfig(@"{ ""signatures"": null }");

        Assert.AreEqual(2, _sut.GetConfig().Signatures.Count);
        AssertRejected("signatures");
    }

    [TestMethod]
    public void GetConfig_EmptySignatures_IsALegitimateSwitchOff()
    {
        WriteConfig(@"{ ""signatures"": [] }");

        Assert.AreEqual(0, _sut.GetConfig().Signatures.Count);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void GetConfig_NullSignatureEntry_IsDroppedAndWarned()
    {
        WriteConfig(@"{ ""signatures"": [ null, { " + SauronBody + " } ] }");

        Assert.AreEqual("sauron", Only().Id);
        AssertRejected("signatures[0]");
    }

    [TestMethod]
    public void GetConfig_SignatureWithoutAnId_IsDroppedAndWarned()
    {
        WriteSauronWith(@"""id"": ""  """);

        Assert.AreEqual(0, _sut.GetConfig().Signatures.Count);
        AssertRejected("no id");
    }

    [TestMethod]
    public void GetConfig_DuplicateSignatureId_KeepsTheFirstAndWarns()
    {
        WriteConfig(@"{ ""signatures"": [ { " + SauronBody + @" }, { " + SauronBody + @", ""id"": ""SAURON"", ""heroIds"": [""lord_x""] } ] }");

        CollectionAssert.AreEqual(new[] { "lord_1_17" }, Only().HeroIds);
        AssertRejected("repeats");
    }

    [TestMethod]
    public void GetConfig_SignatureMatchingNobody_IsDroppedAndWarned()
    {
        WriteSauronWith(@"""heroIds"": [], ""heroSets"": [], ""races"": []");

        Assert.AreEqual(0, _sut.GetConfig().Signatures.Count);
        AssertRejected("matches nobody");
    }

    [TestMethod]
    public void GetConfig_IdIsTrimmed()
    {
        WriteSauronWith(@"""id"": "" sauron """);

        Assert.AreEqual("sauron", Only().Id);
    }

    // ---- Identity lists ------------------------------------------------------

    [TestMethod]
    public void GetConfig_NullHeroIds_RevertsToThisSignaturesDefault()
    {
        WriteSauronWith(@"""heroIds"": null");

        CollectionAssert.Contains(Only().HeroIds, "lord_1_17");
        AssertRejected("heroIds");
    }

    [TestMethod]
    public void GetConfig_NullHeroSets_Reverts()
    {
        WriteSauronWith(@"""heroSets"": null");

        Assert.AreEqual(0, Only().HeroSets.Count, "Sauron's compiled default has no hero set");
        AssertRejected("heroSets");
    }

    [TestMethod]
    public void GetConfig_HeroIdsReplaceTheDefaultsRatherThanAppending()
    {
        WriteSauronWith(@"""heroIds"": [""lord_1_15""]");

        CollectionAssert.AreEquivalent(new[] { "lord_1_15" }, Only().HeroIds);
    }

    [TestMethod]
    public void GetConfig_EmptyRaces_IsALegitimateSwitchOff()
    {
        WriteSauronWith(@"""races"": []");

        Assert.AreEqual(0, Only().Races.Count);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    // ---- Cooldowns -------------------------------------------------------------

    [TestMethod]
    public void GetConfig_NaNCooldown_RevertsToThisSignaturesDefault()
    {
        WriteSauronWith(@"""cooldowns"": { ""Slam"": NaN, ""Sweep"": 12 }");

        Assert.AreEqual(20f, Only().Cooldowns["Slam"], 0.001f);
        AssertRejected("cooldowns['Slam']");
    }

    [TestMethod]
    public void GetConfig_InfiniteCooldown_Reverts()
    {
        WriteSauronWith(@"""cooldowns"": { ""Slam"": 20, ""Sweep"": Infinity }");

        Assert.AreEqual(12f, Only().Cooldowns["Sweep"], 0.001f);
        AssertRejected("cooldowns['Sweep']");
    }

    [TestMethod]
    public void GetConfig_CooldownAboveCeiling_Reverts()
    {
        WriteSauronWith(@"""cooldowns"": { ""Slam"": 500, ""Sweep"": 12 }");

        Assert.AreEqual(20f, Only().Cooldowns["Slam"], 0.001f);
        AssertRejected("cooldowns['Slam']");
    }

    [TestMethod]
    public void GetConfig_NegativeCooldown_Reverts()
    {
        WriteSauronWith(@"""cooldowns"": { ""Slam"": -1, ""Sweep"": 12 }");

        Assert.AreEqual(20f, Only().Cooldowns["Slam"], 0.001f);
        AssertRejected("cooldowns['Slam']");
    }

    [TestMethod]
    public void GetConfig_ZeroCooldown_RevertsBecauseTheFloorKeepsOnePackagePerSwing()
    {
        // Codex review 114, O3: with no cooldown the second body of one cleaving swing would
        // ring again. The floor (0.5 s) is what makes "one package per swing" hold for every
        // config the provider accepts.
        WriteSauronWith(@"""cooldowns"": { ""Slam"": 0, ""Sweep"": 12 }");

        Assert.AreEqual(20f, Only().Cooldowns["Slam"], 0.001f);
        AssertRejected("cooldowns['Slam']");
    }

    [TestMethod]
    public void GetConfig_UnknownCooldownKind_IsDroppedAndWarned()
    {
        WriteSauronWith(@"""cooldowns"": { ""Slam"": 20, ""Sweep"": 12, ""Nova"": 5 }");

        CollectionAssert.AreEquivalent(new[] { "Slam", "Sweep" }, Only().Cooldowns.Keys.ToArray());
        AssertRejected("Nova");
    }

    [TestMethod]
    public void GetConfig_NumericCooldownKind_IsDroppedNotNormalised()
    {
        WriteSauronWith(@"""cooldowns"": { ""Slam"": 20, ""Sweep"": 12, ""1"": 5 }");

        Assert.AreEqual(2, Only().Cooldowns.Count);
        AssertRejected("'1'");
    }

    [TestMethod]
    public void GetConfig_CooldownKindIsNormalisedToTheCanonicalSpelling()
    {
        WriteSauronWith(@"""cooldowns"": { ""slam"": 20, ""SWEEP"": 12 }");

        CollectionAssert.AreEquivalent(new[] { "Slam", "Sweep" }, Only().Cooldowns.Keys.ToArray());
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void GetConfig_NullCooldowns_RevertsToThisSignaturesDefaults()
    {
        WriteSauronWith(@"""cooldowns"": null");

        Assert.AreEqual(20f, Only().Cooldowns["Slam"], 0.001f);
        AssertRejected("cooldowns");
    }

    [TestMethod]
    public void GetConfig_StrikeWhoseKindHasNoCooldown_IsDroppedAndWarned()
    {
        // A strike with no timer would fire on every swing; the row goes, not the safety.
        WriteSauronWith(@"""cooldowns"": { ""Slam"": 20 }");

        var only = Only();

        Assert.IsTrue(only.Strikes.ContainsKey("Overhead"));
        Assert.IsFalse(only.Strikes.ContainsKey("Left"));
        AssertRejected("no cooldowns entry");
    }

    [TestMethod]
    public void GetConfig_InvalidCooldownWithNoCompiledDefault_DropsItAndItsStrikes()
    {
        WriteConfig(@"{ ""signatures"": [ { ""id"": ""witch"", ""heroIds"": [""lord_1_15""],
            ""cooldowns"": { ""Scream"": NaN },
            ""strikes"": { ""Overhead"": { ""kind"": ""Scream"" } } } ] }");

        var only = Only();

        Assert.AreEqual(0, only.Cooldowns.Count);
        Assert.AreEqual(0, only.Strikes.Count);
        AssertRejected("cooldowns['Scream']");
    }

    // ---- Strike profiles -------------------------------------------------------

    [TestMethod]
    public void GetConfig_NullStrikes_RevertsToThisSignaturesDefaults()
    {
        WriteStrikes("null");

        CollectionAssert.AreEquivalent(new[] { "Overhead", "Left", "Right" }, Only().Strikes.Keys.ToArray());
        AssertRejected("strikes");
    }

    [TestMethod]
    public void GetConfig_EmptyStrikes_IsInertNotInvalid()
    {
        WriteStrikes("{}");

        Assert.AreEqual(0, Only().Strikes.Count);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void GetConfig_UnknownDirectionKey_IsSkippedAndWarned()
    {
        // Parsed-but-unresolvable (the M1 trap): the service would never match "Diagonal", so a
        // silently kept row is a row the author believes is live and is not.
        WriteStrikes(@"{ ""Diagonal"": { ""kind"": ""Slam"" }, ""Overhead"": { ""kind"": ""Slam"" } }");

        var only = Only();

        Assert.IsFalse(only.Strikes.ContainsKey("Diagonal"));
        Assert.IsTrue(only.Strikes.ContainsKey("Overhead"));
        AssertRejected("Diagonal");
    }

    [TestMethod]
    public void GetConfig_NumericDirectionKey_IsDroppedNotNormalised()
    {
        // Codex review 114, F2: "1" is StrikeDirection.Overhead by value; only the member name
        // may address a row.
        WriteStrikes(@"{ ""1"": { ""kind"": ""Slam"" } }");

        Assert.AreEqual(0, Only().Strikes.Count);
        AssertRejected("'1'");
    }

    [TestMethod]
    public void GetConfig_DirectionKeyAndKindAreNormalisedToTheCanonicalSpelling()
    {
        WriteStrikes(@"{ ""overhead"": { ""kind"": ""slam"" } }");

        Assert.AreEqual("Slam", Only().Strikes["Overhead"].Kind);
    }

    [TestMethod]
    public void GetConfig_UnknownKind_IsSkippedAndWarned()
    {
        WriteStrikes(@"{ ""Overhead"": { ""kind"": ""Nova"" } }");

        Assert.IsFalse(Only().Strikes.ContainsKey("Overhead"));
        AssertRejected("Nova");
    }

    [TestMethod]
    public void GetConfig_NumericKind_IsDroppedNotNormalised()
    {
        WriteStrikes(@"{ ""Overhead"": { ""kind"": ""1"" } }");

        Assert.IsFalse(Only().Strikes.ContainsKey("Overhead"));
        AssertRejected("'1'");
    }

    [TestMethod]
    public void GetConfig_NullProfile_IsSkippedAndWarned()
    {
        WriteStrikes(@"{ ""Overhead"": null }");

        Assert.IsFalse(Only().Strikes.ContainsKey("Overhead"));
        AssertRejected("Overhead");
    }

    [TestMethod]
    public void GetConfig_NaNOuterRadius_Reverts()
    {
        WriteStrikes(@"{ ""Overhead"": { ""kind"": ""Slam"", ""outerRadius"": NaN } }");

        Assert.AreEqual(4f, Only().Strikes["Overhead"].OuterRadius, 0.001f);
        AssertRejected("outerRadius");
    }

    [TestMethod]
    public void GetConfig_OuterRadiusInCentimetres_Reverts()
    {
        WriteStrikes(@"{ ""Overhead"": { ""kind"": ""Slam"", ""outerRadius"": 400 } }");

        Assert.AreEqual(4f, Only().Strikes["Overhead"].OuterRadius, 0.001f);
        AssertRejected("outerRadius");
    }

    [TestMethod]
    public void GetConfig_InnerRadiusAboveOuter_RevertsInnerToFitTheValidatedOuter()
    {
        WriteStrikes(@"{ ""Overhead"": { ""kind"": ""Slam"", ""outerRadius"": 2.0, ""innerRadius"": 3.0 } }");

        var profile = Only().Strikes["Overhead"];

        Assert.AreEqual(2f, profile.OuterRadius, 0.001f);
        Assert.IsTrue(profile.InnerRadius <= profile.OuterRadius);
        AssertRejected("innerRadius");
    }

    [TestMethod]
    public void GetConfig_DamageFractionAboveOne_Reverts()
    {
        WriteStrikes(@"{ ""Overhead"": { ""kind"": ""Slam"", ""damageFraction"": 2.0 } }");

        Assert.AreEqual(0.6f, Only().Strikes["Overhead"].DamageFraction, 0.001f);
        AssertRejected("damageFraction");
    }

    [TestMethod]
    public void GetConfig_NegativeMagnitude_Reverts()
    {
        WriteStrikes(@"{ ""Overhead"": { ""kind"": ""Slam"", ""magnitude"": -5 } }");

        Assert.AreEqual(80f, Only().Strikes["Overhead"].Magnitude, 0.001f);
        AssertRejected("magnitude");
    }

    [TestMethod]
    public void GetConfig_FearMoraleAboveCeiling_Reverts()
    {
        WriteStrikes(@"{ ""Overhead"": { ""kind"": ""Slam"", ""fearMorale"": 200 } }");

        Assert.AreEqual(15f, Only().Strikes["Overhead"].FearMorale, 0.001f);
        AssertRejected("fearMorale");
    }

    [TestMethod]
    public void GetConfig_WorldHitBaseDamageAboveCeiling_Reverts()
    {
        WriteStrikes(@"{ ""Overhead"": { ""kind"": ""Slam"", ""worldHitBaseDamage"": 9999 } }");

        Assert.AreEqual(60, Only().Strikes["Overhead"].WorldHitBaseDamage);
        AssertRejected("worldHitBaseDamage");
    }

    [TestMethod]
    public void GetConfig_ProfileFieldsRevertToTheDirectionsOwnDefaults()
    {
        // A bad Left value reverts to the LEFT default, not the Overhead one.
        WriteStrikes(@"{ ""Left"": { ""kind"": ""Sweep"", ""outerRadius"": -1 } }");

        Assert.AreEqual(3f, Only().Strikes["Left"].OuterRadius, 0.001f);
    }

    [TestMethod]
    public void GetConfig_ProfileFieldsRevertToTheirOwnSignaturesDefaults()
    {
        // A bad Nazgul value reverts to the NAZGUL default (25), never to Sauron's (15).
        WriteConfig(@"{ ""signatures"": [ { ""id"": ""nazgul"", ""heroSets"": [""nazgul_nine""],
            ""cooldowns"": { ""Scream"": 15 },
            ""strikes"": { ""Overhead"": { ""kind"": ""Scream"", ""origin"": ""Self"", ""fearMorale"": 200 } } } ] }");

        Assert.AreEqual(25f, Only().Strikes["Overhead"].FearMorale, 0.001f);
        AssertRejected("fearMorale");
    }

    [TestMethod]
    public void GetConfig_UnknownSignatureId_RevertsToTheNeutralProfile()
    {
        WriteConfig(@"{ ""signatures"": [ { ""id"": ""witch"", ""heroIds"": [""lord_1_15""],
            ""cooldowns"": { ""Scream"": 15 },
            ""strikes"": { ""Overhead"": { ""kind"": ""Scream"", ""fearMorale"": 500 } } } ] }");

        Assert.AreEqual(0f, Only().Strikes["Overhead"].FearMorale, 0.001f);
        AssertRejected("fearMorale");
    }

    // ---- Origin and sound (#645) -------------------------------------------------

    [TestMethod]
    public void GetConfig_UnknownOrigin_RevertsToTheRowsDefault()
    {
        WriteStrikes(@"{ ""Overhead"": { ""kind"": ""Slam"", ""origin"": ""Sky"" } }");

        Assert.AreEqual("Impact", Only().Strikes["Overhead"].Origin);
        AssertRejected("origin");
    }

    [TestMethod]
    public void GetConfig_OriginIsNormalisedToTheCanonicalSpelling()
    {
        WriteStrikes(@"{ ""Overhead"": { ""kind"": ""Slam"", ""origin"": ""self"" } }");

        Assert.AreEqual("Self", Only().Strikes["Overhead"].Origin);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void GetConfig_ModuleSoundName_PassesThroughTrimmed()
    {
        WriteStrikes(@"{ ""Overhead"": { ""kind"": ""Slam"", ""sound"": "" LOTR/Mordor/Nazgul/nazgul_scream "" } }");

        Assert.AreEqual("LOTR/Mordor/Nazgul/nazgul_scream", Only().Strikes["Overhead"].Sound);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void GetConfig_SoundNameWithIllegalCharacters_RevertsAndWarns()
    {
        WriteStrikes(@"{ ""Overhead"": { ""kind"": ""Slam"", ""sound"": ""scream loud!"" } }");

        Assert.IsNull(Only().Strikes["Overhead"].Sound, "Sauron's overhead has no compiled sound");
        AssertRejected("sound");
    }

    [TestMethod]
    public void GetConfig_BlankSound_MeansNoSound()
    {
        WriteStrikes(@"{ ""Overhead"": { ""kind"": ""Slam"", ""sound"": ""   "" } }");

        Assert.IsNull(Only().Strikes["Overhead"].Sound);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void GetConfig_SummaryWarningFiresOnceForSeveralRejections()
    {
        WriteSauronWith(@"""cooldowns"": { ""Slam"": -1, ""Sweep"": -1 }");

        _sut.GetConfig();

        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("contained invalid values")));
    }
}
