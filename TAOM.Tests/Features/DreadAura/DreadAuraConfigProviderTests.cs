using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.DreadAura;

namespace TAOM.Tests.Features.DreadAura;

/// <summary>
/// One test per validation rule. Syntax-error cases (missing file, malformed JSON) are the easy
/// half; the half that matters is parseable-but-semantically-wrong, which is what an author
/// actually types.
/// </summary>
[TestClass]
public class DreadAuraConfigProviderTests
{
    private string _tempDir = null!;
    private string _featureDir = null!;
    private IPathService _pathService = null!;
    private IModLogger _logger = null!;
    private DreadAuraConfigProvider _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "TAOM_DreadAura_" + Path.GetRandomFileName());
        _featureDir = Path.Combine(_tempDir, "dread_aura");
        Directory.CreateDirectory(_featureDir);

        _pathService = Substitute.For<IPathService>();
        _pathService.ModuleDataPath.Returns(_tempDir);
        _logger = Substitute.For<IModLogger>();

        _sut = new DreadAuraConfigProvider(_pathService, _logger);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private void WriteConfig(string json) =>
        File.WriteAllText(Path.Combine(_featureDir, "dread_aura_config.json"), json);

    // ---- Syntax-level failures --------------------------------------------

    [TestMethod]
    public void GetConfig_MissingFile_ReturnsDefaultsAndWarns()
    {
        var config = _sut.GetConfig();

        Assert.IsTrue(config.Enabled);
        Assert.AreEqual(12f, config.Profile.Radius, 0.001f);
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

        Assert.AreEqual(12f, config.Profile.Radius, 0.001f);
        Assert.AreEqual(5f, config.Profile.MoralePerSecond, 0.001f);
    }

    [TestMethod]
    public void GetConfig_CalledTwice_ParsesOnce()
    {
        WriteConfig(@"{ ""pulseIntervalSeconds"": 0.5 }");

        var first = _sut.GetConfig();
        var second = _sut.GetConfig();

        Assert.AreSame(first, second);
    }

    // ---- Every field round-trips ------------------------------------------

    [TestMethod]
    public void GetConfig_EveryFieldSetToNonDefault_ReadsEachBack()
    {
        // Guards the sanitised-copy trap: a field added to the POCO but forgotten in Validate's
        // object initialiser is silently dropped, and no per-rule test would catch it.
        WriteConfig(@"{
  ""enabled"": false,
  ""pulseIntervalSeconds"": 0.75,
  ""maxSourcesPerFrame"": 5,
  ""fearVoiceChancePerPulse"": 0.33,
  ""heroSets"": [""nazgul_nine"", ""istari_five""],
  ""heroIds"": [""lord_1_17"", ""lord_1_99""],
  ""races"": [""sauron"", ""cave_troll""],
  ""profile"": {
    ""radius"": 20.0,
    ""innerRadius"": 6.0,
    ""moralePerSecond"": 7.5,
    ""moraleFloor"": 12.0
  },
  ""raceResist"": { ""elf"": 0.3, ""dwarf"": 0.6 }
}");

        var config = _sut.GetConfig();

        Assert.IsFalse(config.Enabled);
        Assert.AreEqual(0.75f, config.PulseIntervalSeconds, 0.001f);
        Assert.AreEqual(5, config.MaxSourcesPerFrame);
        Assert.AreEqual(0.33f, config.FearVoiceChancePerPulse, 0.001f);
        CollectionAssert.AreEqual(new[] { "nazgul_nine", "istari_five" }, config.HeroSets);
        CollectionAssert.AreEqual(new[] { "lord_1_17", "lord_1_99" }, config.HeroIds);
        CollectionAssert.AreEqual(new[] { "sauron", "cave_troll" }, config.Races);
        Assert.AreEqual(20f, config.Profile.Radius, 0.001f);
        Assert.AreEqual(6f, config.Profile.InnerRadius, 0.001f);
        Assert.AreEqual(7.5f, config.Profile.MoralePerSecond, 0.001f);
        Assert.AreEqual(12f, config.Profile.MoraleFloor, 0.001f);
        Assert.AreEqual(0.3f, config.RaceResist["elf"], 0.001f);
        Assert.AreEqual(0.6f, config.RaceResist["dwarf"], 0.001f);
    }

    [TestMethod]
    public void GetConfig_PartialRaceResist_ReplacesDefaultsInsteadOfAppending()
    {
        // Without ObjectCreationHandling.Replace, Json.NET merges onto the compiled dictionary and
        // the author's single entry arrives alongside both defaults.
        WriteConfig(@"{ ""raceResist"": { ""elf"": 0.9 } }");

        var config = _sut.GetConfig();

        Assert.AreEqual(1, config.RaceResist.Count);
        Assert.AreEqual(0.9f, config.RaceResist["elf"], 0.001f);
    }

    [TestMethod]
    public void GetConfig_PartialRaces_ReplacesDefaultsInsteadOfAppending()
    {
        WriteConfig(@"{ ""races"": [""cave_troll""] }");

        var config = _sut.GetConfig();

        CollectionAssert.AreEqual(new[] { "cave_troll" }, config.Races);
    }

    // ---- Range and finiteness, one test per rule ---------------------------

    [DataTestMethod]
    [DataRow("NaN")]
    [DataRow("Infinity")]
    [DataRow("-Infinity")]
    [DataRow("0.0")]
    [DataRow("-5.0")]
    [DataRow("31.0")]
    public void GetConfig_RadiusOutOfRangeOrNonFinite_RevertsAndWarns(string raw)
    {
        WriteConfig($@"{{ ""profile"": {{ ""radius"": {raw} }} }}");

        var config = _sut.GetConfig();

        Assert.AreEqual(12f, config.Profile.Radius, 0.001f);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("profile.radius")));
    }

    [TestMethod]
    public void GetConfig_InnerRadiusExceedsRadius_RevertsAndWarns()
    {
        // The ordering invariant. An inner radius above the outer one inverts the falloff band.
        WriteConfig(@"{ ""profile"": { ""radius"": 8.0, ""innerRadius"": 20.0 } }");

        var config = _sut.GetConfig();

        Assert.IsTrue(config.Profile.InnerRadius <= config.Profile.Radius,
            $"innerRadius {config.Profile.InnerRadius} must not exceed radius {config.Profile.Radius}");
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("profile.innerRadius")));
    }

    [TestMethod]
    public void GetConfig_RadiusBelowDefaultInnerRadius_StillYieldsOrderedPair()
    {
        // radius 3 is below the compiled innerRadius default of 4, so the fallback itself must be
        // clamped or the revert would reintroduce the very inversion it is fixing.
        WriteConfig(@"{ ""profile"": { ""radius"": 3.0, ""innerRadius"": 99.0 } }");

        var config = _sut.GetConfig();

        Assert.AreEqual(3f, config.Profile.Radius, 0.001f);
        Assert.IsTrue(config.Profile.InnerRadius <= config.Profile.Radius);
    }

    [DataTestMethod]
    [DataRow("NaN")]
    [DataRow("-1.0")]
    [DataRow("51.0")]
    public void GetConfig_MoralePerSecondOutOfRangeOrNonFinite_RevertsAndWarns(string raw)
    {
        WriteConfig($@"{{ ""profile"": {{ ""moralePerSecond"": {raw} }} }}");

        var config = _sut.GetConfig();

        Assert.AreEqual(5f, config.Profile.MoralePerSecond, 0.001f);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("profile.moralePerSecond")));
    }

    [TestMethod]
    public void GetConfig_MoralePerSecondZero_IsAcceptedAsADisableSwitch()
    {
        WriteConfig(@"{ ""profile"": { ""moralePerSecond"": 0.0 } }");

        Assert.AreEqual(0f, _sut.GetConfig().Profile.MoralePerSecond, 0.001f);
    }

    [DataTestMethod]
    [DataRow("NaN")]
    [DataRow("-1.0")]
    [DataRow("51.0")]
    public void GetConfig_MoraleFloorOutOfRangeOrNonFinite_RevertsAndWarns(string raw)
    {
        WriteConfig($@"{{ ""profile"": {{ ""moraleFloor"": {raw} }} }}");

        var config = _sut.GetConfig();

        Assert.AreEqual(0f, config.Profile.MoraleFloor, 0.001f);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("profile.moraleFloor")));
    }

    [DataTestMethod]
    [DataRow("NaN")]
    [DataRow("0.05")]
    [DataRow("6.0")]
    public void GetConfig_PulseIntervalOutOfRangeOrNonFinite_RevertsAndWarns(string raw)
    {
        WriteConfig($@"{{ ""pulseIntervalSeconds"": {raw} }}");

        var config = _sut.GetConfig();

        Assert.AreEqual(0.25f, config.PulseIntervalSeconds, 0.001f);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("PulseIntervalSeconds")));
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(-3)]
    [DataRow(17)]
    public void GetConfig_MaxSourcesPerFrameOutOfRange_RevertsAndWarns(int raw)
    {
        WriteConfig($@"{{ ""maxSourcesPerFrame"": {raw} }}");

        var config = _sut.GetConfig();

        Assert.AreEqual(2, config.MaxSourcesPerFrame);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("MaxSourcesPerFrame")));
    }

    [DataTestMethod]
    [DataRow("NaN")]
    [DataRow("-0.1")]
    [DataRow("1.5")]
    public void GetConfig_FearVoiceChanceOutOfRangeOrNonFinite_RevertsAndWarns(string raw)
    {
        WriteConfig($@"{{ ""fearVoiceChancePerPulse"": {raw} }}");

        var config = _sut.GetConfig();

        Assert.AreEqual(0.02f, config.FearVoiceChancePerPulse, 0.001f);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("FearVoiceChancePerPulse")));
    }

    [DataTestMethod]
    [DataRow("NaN")]
    [DataRow("-0.5")]
    [DataRow("1.5")]
    public void GetConfig_RaceResistOutOfRangeOrNonFinite_DropsRowAndWarns(string raw)
    {
        // Above 1.0 would AMPLIFY dread on a race the author meant to protect.
        WriteConfig($@"{{ ""raceResist"": {{ ""elf"": 0.4, ""dwarf"": {raw} }} }}");

        var config = _sut.GetConfig();

        Assert.AreEqual(1, config.RaceResist.Count);
        Assert.AreEqual(0.4f, config.RaceResist["elf"], 0.001f);
        Assert.IsFalse(config.RaceResist.ContainsKey("dwarf"));
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("raceResist['dwarf']")));
    }

    [TestMethod]
    public void GetConfig_RaceResistZero_IsAcceptedAsImmunity()
    {
        WriteConfig(@"{ ""raceResist"": { ""elf"": 0.0 } }");

        Assert.AreEqual(0f, _sut.GetConfig().RaceResist["elf"], 0.001f);
    }

    // ---- Null and empty collections ---------------------------------------

    [DataTestMethod]
    [DataRow("heroSets")]
    [DataRow("heroIds")]
    [DataRow("races")]
    public void GetConfig_NullList_RevertsAndWarns(string field)
    {
        WriteConfig($@"{{ ""{field}"": null }}");

        _sut.GetConfig();

        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("is null")));
    }

    [TestMethod]
    public void GetConfig_EmptyLists_ArePreservedAsADeliberateOff()
    {
        WriteConfig(@"{ ""heroSets"": [], ""heroIds"": [], ""races"": [] }");

        var config = _sut.GetConfig();

        Assert.AreEqual(0, config.HeroSets.Count);
        Assert.AreEqual(0, config.HeroIds.Count);
        Assert.AreEqual(0, config.Races.Count);
    }

    [TestMethod]
    public void GetConfig_NullProfile_RevertsAndWarns()
    {
        WriteConfig(@"{ ""profile"": null }");

        var config = _sut.GetConfig();

        Assert.AreEqual(12f, config.Profile.Radius, 0.001f);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("profile is null")));
    }

    [TestMethod]
    public void GetConfig_NullRaceResist_RevertsAndWarns()
    {
        WriteConfig(@"{ ""raceResist"": null }");

        var config = _sut.GetConfig();

        Assert.AreEqual(0.4f, config.RaceResist["elf"], 0.001f);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("raceResist is null")));
    }

    // ---- Summary reporting -------------------------------------------------

    [TestMethod]
    public void GetConfig_AnyRejection_EmitsSummaryWarning()
    {
        WriteConfig(@"{ ""profile"": { ""radius"": 999.0 } }");

        _sut.GetConfig();

        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("contained invalid values")));
    }

    [TestMethod]
    public void GetConfig_AllValid_LogsSuccessWithoutSummaryWarning()
    {
        WriteConfig(@"{ ""profile"": { ""radius"": 15.0 } }");

        _sut.GetConfig();

        _logger.Received().LogInfo(Arg.Is<string>(m => m.Contains("Loaded")));
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(m => m.Contains("contained invalid values")));
    }
}
