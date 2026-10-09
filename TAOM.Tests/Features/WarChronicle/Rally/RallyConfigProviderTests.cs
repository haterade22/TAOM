using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.WarChronicle.Rally;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.WarChronicle.Rally;

[TestClass]
public class RallyConfigProviderTests
{
    private string _tempDir = null!;
    private string _featureDir = null!;
    private IPathService _pathService = null!;
    private IModLogger _logger = null!;
    private RallyConfigProvider _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "TAOM_Rally_" + Path.GetRandomFileName());
        _featureDir = Path.Combine(_tempDir, "war_chronicle");
        Directory.CreateDirectory(_featureDir);

        _pathService = Substitute.For<IPathService>();
        _pathService.ModuleDataPath.Returns(_tempDir);
        _logger = Substitute.For<IModLogger>();
        _sut = new RallyConfigProvider(_pathService, _logger);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private void WriteConfig(string json) => File.WriteAllText(Path.Combine(_featureDir, "rally.json"), json);

    // The shipped defaults, with any one value swapped for the text under test.
    private static string Json(
        string enabled = "true", string includeNeutral = "true", string ttl = "48",
        string t1Enter = "0.25", string t1Exit = "0.15", string t1Vol = "0.10", string t1Esc = "0.50",
        string t2Enter = "0.50", string t2Exit = "0.40", string t2Vol = "0.20", string t2Esc = "1.00") =>
        "{ \"enabled\": " + enabled + ", \"includeNeutral\": " + includeNeutral + ", \"effectTtlHours\": " + ttl + ", "
        + "\"tier1\": { \"enterLoss\": " + t1Enter + ", \"exitLoss\": " + t1Exit + ", \"volunteerRate\": " + t1Vol + ", \"prisonerEscape\": " + t1Esc + " }, "
        + "\"tier2\": { \"enterLoss\": " + t2Enter + ", \"exitLoss\": " + t2Exit + ", \"volunteerRate\": " + t2Vol + ", \"prisonerEscape\": " + t2Esc + " } }";

    private static void AssertDefaultThresholds(RallyConfig c)
    {
        Assert.AreEqual(0.25f, c.Tier1.EnterLoss, 0.0001f);
        Assert.AreEqual(0.15f, c.Tier1.ExitLoss, 0.0001f);
        Assert.AreEqual(0.50f, c.Tier2.EnterLoss, 0.0001f);
        Assert.AreEqual(0.40f, c.Tier2.ExitLoss, 0.0001f);
    }

    private static void AssertDefaultMagnitudes(RallyConfig c)
    {
        Assert.AreEqual(0.10f, c.Tier1.VolunteerRate, 0.0001f);
        Assert.AreEqual(0.50f, c.Tier1.PrisonerEscape, 0.0001f);
        Assert.AreEqual(0.20f, c.Tier2.VolunteerRate, 0.0001f);
        Assert.AreEqual(1.00f, c.Tier2.PrisonerEscape, 0.0001f);
    }

    // ---- Load paths ----

    [TestMethod]
    public void GetConfig_ValidJson_ParsesAllFields()
    {
        WriteConfig(Json(
            enabled: "false", includeNeutral: "false", ttl: "72",
            t1Enter: "0.30", t1Exit: "0.20", t1Vol: "0.05", t1Esc: "0.25",
            t2Enter: "0.60", t2Exit: "0.45", t2Vol: "0.15", t2Esc: "0.75"));

        var c = _sut.GetConfig();

        Assert.IsFalse(c.Enabled);
        Assert.IsFalse(c.IncludeNeutral);
        Assert.AreEqual(72f, c.EffectTtlHours, 0.0001f);
        Assert.AreEqual(0.30f, c.Tier1.EnterLoss, 0.0001f);
        Assert.AreEqual(0.20f, c.Tier1.ExitLoss, 0.0001f);
        Assert.AreEqual(0.05f, c.Tier1.VolunteerRate, 0.0001f);
        Assert.AreEqual(0.25f, c.Tier1.PrisonerEscape, 0.0001f);
        Assert.AreEqual(0.60f, c.Tier2.EnterLoss, 0.0001f);
        Assert.AreEqual(0.45f, c.Tier2.ExitLoss, 0.0001f);
        Assert.AreEqual(0.15f, c.Tier2.VolunteerRate, 0.0001f);
        Assert.AreEqual(0.75f, c.Tier2.PrisonerEscape, 0.0001f);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void GetConfig_ValidJson_LogsLoadedWithoutWarning()
    {
        WriteConfig(Json());

        _sut.GetConfig();

        _logger.Received(1).LogInfo(Arg.Is<string>(m => m.Contains("Loaded rally.json")));
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void GetConfig_MissingFile_ReturnsTheCompiledDefaultsAndWarns()
    {
        var c = _sut.GetConfig();

        Assert.IsTrue(c.Enabled);
        Assert.IsTrue(c.IncludeNeutral);
        Assert.AreEqual(48f, c.EffectTtlHours, 0.0001f);
        AssertDefaultThresholds(c);
        AssertDefaultMagnitudes(c);
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("not found")));
    }

    [TestMethod]
    public void GetConfig_MalformedJson_ReturnsTheCompiledDefaultsAndLogsAnError()
    {
        WriteConfig("{ this is not json");

        var c = _sut.GetConfig();

        Assert.IsTrue(c.Enabled);
        AssertDefaultThresholds(c);
        _logger.Received(1).LogError(Arg.Is<string>(m => m.Contains("Failed to parse")));
    }

    [TestMethod]
    public void GetConfig_EmptyObject_KeepsEveryDefaultWithoutWarning()
    {
        WriteConfig("{}");

        var c = _sut.GetConfig();

        Assert.IsTrue(c.Enabled);
        Assert.AreEqual(48f, c.EffectTtlHours, 0.0001f);
        AssertDefaultThresholds(c);
        AssertDefaultMagnitudes(c);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void GetConfig_NullTierObjects_AreReplacedByTheDefaults()
    {
        WriteConfig("{ \"tier1\": null, \"tier2\": null }");

        var c = _sut.GetConfig();

        AssertDefaultThresholds(c);
        AssertDefaultMagnitudes(c);
    }

    [TestMethod]
    public void GetConfig_APartialTierObject_KeepsTheOtherDefaultsOfThatTier()
    {
        WriteConfig("{ \"tier1\": { \"enterLoss\": 0.30 } }");

        var c = _sut.GetConfig();

        Assert.AreEqual(0.30f, c.Tier1.EnterLoss, 0.0001f);
        Assert.AreEqual(0.15f, c.Tier1.ExitLoss, 0.0001f);
        Assert.AreEqual(0.10f, c.Tier1.VolunteerRate, 0.0001f);
        Assert.AreEqual(0.50f, c.Tier1.PrisonerEscape, 0.0001f);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void GetConfig_CalledTwice_ReadsTheFileOnce()
    {
        WriteConfig(Json());
        _sut.GetConfig();
        File.Delete(Path.Combine(_featureDir, "rally.json"));

        var c = _sut.GetConfig();

        Assert.IsTrue(c.Enabled);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void GetConfig_TheShippedRallyJson_LoadsCleanAndEqualsTheDefaults()
    {
        var shippedPaths = Substitute.For<IPathService>();
        shippedPaths.ModuleDataPath.Returns(RepoPaths.RepoPath("Main", "_Module", "ModuleData"));

        var c = new RallyConfigProvider(shippedPaths, _logger).GetConfig();

        Assert.IsTrue(c.Enabled);
        Assert.IsTrue(c.IncludeNeutral);
        Assert.AreEqual(48f, c.EffectTtlHours, 0.0001f);
        AssertDefaultThresholds(c);
        AssertDefaultMagnitudes(c);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
        _logger.DidNotReceive().LogError(Arg.Any<string>());
    }

    // ---- effectTtlHours: finite, 24 to 168 ----

    [DataTestMethod]
    [DataRow("NaN")]
    [DataRow("Infinity")]
    [DataRow("-Infinity")]
    [DataRow("23.9")]
    [DataRow("0")]
    [DataRow("-48")]
    [DataRow("168.1")]
    [DataRow("100000")]
    public void GetConfig_TtlNonFiniteOrOutsideRange_RevertsToTheDefault(string ttl)
    {
        WriteConfig(Json(ttl: ttl, t1Vol: "0.07"));

        var c = _sut.GetConfig();

        Assert.AreEqual(48f, c.EffectTtlHours, 0.0001f);
        Assert.AreEqual(0.07f, c.Tier1.VolunteerRate, 0.0001f, "the other fields keep their values");
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("effectTtlHours")));
    }

    [DataTestMethod]
    [DataRow("24")]
    [DataRow("168")]
    public void GetConfig_TtlAtTheBounds_IsKept(string ttl)
    {
        WriteConfig(Json(ttl: ttl));

        Assert.AreEqual(float.Parse(ttl), _sut.GetConfig().EffectTtlHours, 0.0001f);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    // ---- the four loss thresholds: finite, strictly between 0 and 1 ----

    [DataTestMethod]
    [DataRow("NaN")]
    [DataRow("Infinity")]
    [DataRow("0")]
    [DataRow("-0.1")]
    [DataRow("1")]
    [DataRow("1.5")]
    public void GetConfig_Tier1EnterLossInvalid_RevertsToTheDefault(string bad)
    {
        WriteConfig(Json(t1Enter: bad));

        Assert.AreEqual(0.25f, _sut.GetConfig().Tier1.EnterLoss, 0.0001f);
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("tier1.enterLoss")));
    }

    [DataTestMethod]
    [DataRow("NaN")]
    [DataRow("-Infinity")]
    [DataRow("0")]
    [DataRow("1")]
    [DataRow("2")]
    public void GetConfig_Tier1ExitLossInvalid_RevertsToTheDefault(string bad)
    {
        WriteConfig(Json(t1Exit: bad));

        Assert.AreEqual(0.15f, _sut.GetConfig().Tier1.ExitLoss, 0.0001f);
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("tier1.exitLoss")));
    }

    [DataTestMethod]
    [DataRow("NaN")]
    [DataRow("Infinity")]
    [DataRow("0")]
    [DataRow("1")]
    [DataRow("-0.5")]
    public void GetConfig_Tier2EnterLossInvalid_RevertsToTheDefault(string bad)
    {
        WriteConfig(Json(t2Enter: bad));

        Assert.AreEqual(0.50f, _sut.GetConfig().Tier2.EnterLoss, 0.0001f);
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("tier2.enterLoss")));
    }

    [DataTestMethod]
    [DataRow("NaN")]
    [DataRow("-Infinity")]
    [DataRow("0")]
    [DataRow("1")]
    [DataRow("3")]
    public void GetConfig_Tier2ExitLossInvalid_RevertsToTheDefault(string bad)
    {
        WriteConfig(Json(t2Exit: bad));

        Assert.AreEqual(0.40f, _sut.GetConfig().Tier2.ExitLoss, 0.0001f);
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("tier2.exitLoss")));
    }

    // ---- ordering: exit below enter in each tier, tier 1 not above tier 2 ----

    [DataTestMethod]
    [DataRow("0.25")]
    [DataRow("0.30")]
    public void GetConfig_Tier1ExitNotBelowEnter_RevertsEveryThreshold(string exit)
    {
        WriteConfig(Json(t1Exit: exit, t2Enter: "0.60"));

        var c = _sut.GetConfig();

        AssertDefaultThresholds(c);
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("tier1.exitLoss") && m.Contains("below")));
    }

    [DataTestMethod]
    [DataRow("0.50")]
    [DataRow("0.55")]
    public void GetConfig_Tier2ExitNotBelowEnter_RevertsEveryThreshold(string exit)
    {
        WriteConfig(Json(t2Exit: exit, t1Enter: "0.30"));

        var c = _sut.GetConfig();

        AssertDefaultThresholds(c);
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("tier2.exitLoss") && m.Contains("below")));
    }

    [TestMethod]
    public void GetConfig_Tier1EnterAboveTier2Enter_RevertsEveryThreshold()
    {
        WriteConfig(Json(t1Enter: "0.45", t1Exit: "0.10", t2Enter: "0.40", t2Exit: "0.05"));

        var c = _sut.GetConfig();

        AssertDefaultThresholds(c);
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("tier1.enterLoss") && m.Contains("tier2.enterLoss")));
    }

    [TestMethod]
    public void GetConfig_Tier1ExitAboveTier2Exit_RevertsEveryThreshold()
    {
        WriteConfig(Json(t1Enter: "0.30", t1Exit: "0.28", t2Enter: "0.60", t2Exit: "0.20"));

        var c = _sut.GetConfig();

        AssertDefaultThresholds(c);
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("tier1.exitLoss") && m.Contains("tier2.exitLoss")));
    }

    [TestMethod]
    public void GetConfig_EqualTierEntersAndExits_AreAllowed()
    {
        WriteConfig(Json(t1Enter: "0.40", t1Exit: "0.30", t2Enter: "0.40", t2Exit: "0.30"));

        var c = _sut.GetConfig();

        Assert.AreEqual(0.40f, c.Tier1.EnterLoss, 0.0001f);
        Assert.AreEqual(0.30f, c.Tier2.ExitLoss, 0.0001f);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    // ---- magnitudes: finite, 0 to 2, tier 2 not below tier 1 ----

    [DataTestMethod]
    [DataRow("NaN")]
    [DataRow("Infinity")]
    [DataRow("-0.01")]
    [DataRow("2.01")]
    public void GetConfig_Tier1VolunteerInvalid_RevertsToTheDefault(string bad)
    {
        WriteConfig(Json(t1Vol: bad));

        Assert.AreEqual(0.10f, _sut.GetConfig().Tier1.VolunteerRate, 0.0001f);
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("tier1.volunteerRate")));
    }

    [DataTestMethod]
    [DataRow("NaN")]
    [DataRow("-Infinity")]
    [DataRow("-1")]
    [DataRow("5")]
    public void GetConfig_Tier1PrisonerEscapeInvalid_RevertsToTheDefault(string bad)
    {
        WriteConfig(Json(t1Esc: bad));

        Assert.AreEqual(0.50f, _sut.GetConfig().Tier1.PrisonerEscape, 0.0001f);
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("tier1.prisonerEscape")));
    }

    [DataTestMethod]
    [DataRow("NaN")]
    [DataRow("Infinity")]
    [DataRow("-0.2")]
    [DataRow("2.5")]
    public void GetConfig_Tier2VolunteerInvalid_RevertsToTheDefault(string bad)
    {
        WriteConfig(Json(t2Vol: bad));

        Assert.AreEqual(0.20f, _sut.GetConfig().Tier2.VolunteerRate, 0.0001f);
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("tier2.volunteerRate")));
    }

    [DataTestMethod]
    [DataRow("NaN")]
    [DataRow("-Infinity")]
    [DataRow("-1")]
    [DataRow("9")]
    public void GetConfig_Tier2PrisonerEscapeInvalid_RevertsToTheDefault(string bad)
    {
        WriteConfig(Json(t2Esc: bad));

        Assert.AreEqual(1.00f, _sut.GetConfig().Tier2.PrisonerEscape, 0.0001f);
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("tier2.prisonerEscape")));
    }

    [DataTestMethod]
    [DataRow("0")]
    [DataRow("2")]
    public void GetConfig_MagnitudesAtTheBounds_AreKept(string value)
    {
        WriteConfig(Json(t1Vol: "0", t1Esc: "0", t2Vol: value, t2Esc: value));

        var c = _sut.GetConfig();

        Assert.AreEqual(float.Parse(value), c.Tier2.VolunteerRate, 0.0001f);
        Assert.AreEqual(0f, c.Tier1.PrisonerEscape, 0.0001f);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void GetConfig_Tier2VolunteerBelowTier1_RevertsEveryMagnitude()
    {
        WriteConfig(Json(t1Vol: "0.30", t2Vol: "0.10", t1Esc: "0.40", t2Esc: "0.90"));

        var c = _sut.GetConfig();

        AssertDefaultMagnitudes(c);
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("tier2.volunteerRate") && m.Contains("tier1.volunteerRate")));
    }

    [TestMethod]
    public void GetConfig_Tier2PrisonerEscapeBelowTier1_RevertsEveryMagnitude()
    {
        WriteConfig(Json(t1Esc: "0.80", t2Esc: "0.30", t1Vol: "0.05", t2Vol: "0.15"));

        var c = _sut.GetConfig();

        AssertDefaultMagnitudes(c);
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("tier2.prisonerEscape") && m.Contains("tier1.prisonerEscape")));
    }

    [TestMethod]
    public void GetConfig_EqualTierMagnitudes_AreAllowed()
    {
        WriteConfig(Json(t1Vol: "0.10", t2Vol: "0.10", t1Esc: "0.50", t2Esc: "0.50"));

        var c = _sut.GetConfig();

        Assert.AreEqual(0.10f, c.Tier2.VolunteerRate, 0.0001f);
        Assert.AreEqual(0.50f, c.Tier2.PrisonerEscape, 0.0001f);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    // ---- summary ----

    [TestMethod]
    public void GetConfig_AnyRevert_EndsWithOneSummaryWarning()
    {
        WriteConfig(Json(ttl: "5", t1Enter: "NaN"));

        _sut.GetConfig();

        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("contained invalid values")));
        _logger.DidNotReceive().LogInfo(Arg.Is<string>(m => m.Contains("Loaded rally.json")));
    }
}
