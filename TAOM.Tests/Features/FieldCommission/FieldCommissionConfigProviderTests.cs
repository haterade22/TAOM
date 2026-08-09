using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.FieldCommission;

namespace TAOM.Tests.Features.FieldCommission;

[TestClass]
public class FieldCommissionConfigProviderTests
{
    private string _tempDir = null!;
    private string _featureDir = null!;
    private IPathService _pathService = null!;
    private IModLogger _logger = null!;
    private FieldCommissionConfigProvider _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "TAOM_FieldCommission_" + Path.GetRandomFileName());
        _featureDir = Path.Combine(_tempDir, "field_commission");
        Directory.CreateDirectory(_featureDir);

        _pathService = Substitute.For<IPathService>();
        _pathService.ModuleDataPath.Returns(_tempDir);
        _logger = Substitute.For<IModLogger>();

        _sut = new FieldCommissionConfigProvider(_pathService, _logger);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private void WriteConfig(string json) =>
        File.WriteAllText(Path.Combine(_featureDir, "field_commission_config.json"), json);

    [TestMethod]
    public void GetConfig_ValidJson_ParsesAllFields()
    {
        WriteConfig(@"{
  ""enabled"": false,
  ""ratioThreshold"": 2.0,
  ""meritPerKill"": 2,
  ""meritThreshold"": 10,
  ""retainerAllowance"": 3,
  ""maxOffersPerBattle"": 4,
  ""diagnostics"": true,
  ""skillPointsPerLevel"": 8,
  ""allowedRaceNames"": [""human""]
}");

        var config = _sut.GetConfig();

        Assert.IsFalse(config.Enabled);
        Assert.AreEqual(2.0f, config.RatioThreshold, 0.001f);
        Assert.AreEqual(2, config.MeritPerKill);
        Assert.AreEqual(10, config.MeritThreshold);
        Assert.AreEqual(3, config.RetainerAllowance);
        Assert.AreEqual(4, config.MaxOffersPerBattle);
        // Validate() rebuilds the config field-by-field, so a field added to the POCO but forgotten
        // in that copy is silently dropped no matter what the file says. This assertion is the guard
        // for that whole class of omission — it caught exactly that for `diagnostics`.
        Assert.IsTrue(config.Diagnostics);
        Assert.AreEqual(8, config.SkillPointsPerLevel);
        CollectionAssert.AreEqual(new[] { "human" }, config.AllowedRaceNames);
    }

    [TestMethod]
    public void GetConfig_MaxOffersPerBattleBelowOne_RevertsToDefaultAndWarns()
    {
        // 0 would read as "promotions enabled" while queueing nothing — the one state the master
        // toggle exists to make explicit.
        WriteConfig(@"{ ""maxOffersPerBattle"": 0 }");

        var config = _sut.GetConfig();

        Assert.AreEqual(2, config.MaxOffersPerBattle);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("maxOffersPerBattle=0")));
    }

    [TestMethod]
    public void GetConfig_MissingFile_ReturnsDefaultsAndLogsWarning()
    {
        var config = _sut.GetConfig();

        Assert.IsTrue(config.Enabled);
        Assert.AreEqual(1.3f, config.RatioThreshold, 0.001f);
        Assert.AreEqual(1, config.MeritPerKill);
        Assert.AreEqual(32, config.MeritThreshold);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("not found")));
    }

    [TestMethod]
    public void GetConfig_MalformedJson_ReturnsDefaultsAndLogsError()
    {
        WriteConfig("not valid json {{{");

        var config = _sut.GetConfig();

        Assert.AreEqual(32, config.MeritThreshold);
        _logger.Received().LogError(Arg.Is<string>(s => s.Contains("Failed to parse")));
    }

    [TestMethod]
    public void GetConfig_NaNRatioThreshold_RevertsToDefaultAndWarns()
    {
        WriteConfig(@"{ ""ratioThreshold"": ""NaN"" }");

        var config = _sut.GetConfig();

        Assert.AreEqual(1.3f, config.RatioThreshold, 0.001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("ratioThreshold")));
    }

    [TestMethod]
    public void GetConfig_NegativeRatioThreshold_RevertsToDefaultAndWarns()
    {
        WriteConfig(@"{ ""ratioThreshold"": -1.0 }");

        var config = _sut.GetConfig();

        Assert.AreEqual(1.3f, config.RatioThreshold, 0.001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("ratioThreshold=-1")));
    }

    [TestMethod]
    public void GetConfig_ZeroRatioThreshold_AcceptedAsEffectivelyDisabled()
    {
        // 0 is a valid (if extreme) choice — it means the ratio gate can never pass. Not a bug,
        // so it must NOT revert.
        WriteConfig(@"{ ""ratioThreshold"": 0 }");

        var config = _sut.GetConfig();

        Assert.AreEqual(0f, config.RatioThreshold, 0.001f);
    }

    [TestMethod]
    public void GetConfig_MeritPerKillZero_RevertsToDefaultAndWarns()
    {
        WriteConfig(@"{ ""meritPerKill"": 0 }");

        var config = _sut.GetConfig();

        Assert.AreEqual(1, config.MeritPerKill);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("meritPerKill=0")));
    }

    [TestMethod]
    public void GetConfig_MeritThresholdNegative_RevertsToDefaultAndWarns()
    {
        WriteConfig(@"{ ""meritThreshold"": -5 }");

        var config = _sut.GetConfig();

        Assert.AreEqual(32, config.MeritThreshold);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("meritThreshold=-5")));
    }

    [TestMethod]
    public void GetConfig_SkillPointsPerLevelZero_RevertsToDefaultAndWarns()
    {
        WriteConfig(@"{ ""skillPointsPerLevel"": 0 }");

        var config = _sut.GetConfig();

        Assert.AreEqual(5, config.SkillPointsPerLevel);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("skillPointsPerLevel=0")));
    }

    [TestMethod]
    public void GetConfig_RetainerAllowanceNegative_RevertsToDefaultAndWarns()
    {
        WriteConfig(@"{ ""retainerAllowance"": -1 }");

        var config = _sut.GetConfig();

        Assert.AreEqual(0, config.RetainerAllowance);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("retainerAllowance=-1")));
    }

    [TestMethod]
    public void GetConfig_AllowedRaceNamesMissing_DefaultsToHumanDwarfElf()
    {
        WriteConfig(@"{ ""meritThreshold"": 10 }");

        var config = _sut.GetConfig();

        CollectionAssert.AreEqual(new[] { "human", "dwarf", "elf" }, config.AllowedRaceNames);
    }

    [TestMethod]
    public void GetConfig_AllowedRaceNamesNull_DefaultsToHumanDwarfElf()
    {
        WriteConfig(@"{ ""allowedRaceNames"": null }");

        var config = _sut.GetConfig();

        CollectionAssert.AreEqual(new[] { "human", "dwarf", "elf" }, config.AllowedRaceNames);
    }

    [TestMethod]
    public void GetConfig_AllowedRaceNamesContainsBlankEntry_SanitizedOut()
    {
        WriteConfig(@"{ ""allowedRaceNames"": [""human"", """", ""  "", ""dwarf""] }");

        var config = _sut.GetConfig();

        CollectionAssert.AreEqual(new[] { "human", "dwarf" }, config.AllowedRaceNames);
    }

    [TestMethod]
    public void GetConfig_ValidValues_LogsInfoNotWarning()
    {
        WriteConfig(@"{ ""meritThreshold"": 10 }");

        _sut.GetConfig();

        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("Loaded")));
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void GetConfig_CalledTwice_ReturnsSameCachedInstance()
    {
        WriteConfig(@"{ ""meritThreshold"": 10 }");

        Assert.AreSame(_sut.GetConfig(), _sut.GetConfig());
    }
}
