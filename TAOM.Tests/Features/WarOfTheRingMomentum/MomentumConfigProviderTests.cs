using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.WarOfTheRingMomentum;
using TAOM.Features.WarOfTheRingMomentum.Domain;

namespace TAOM.Tests.Features.WarOfTheRingMomentum;

[TestClass]
public class MomentumConfigProviderTests
{
    private string _tempDir = null!;
    private string _featureDir = null!;
    private IPathService _pathService = null!;
    private IModLogger _logger = null!;
    private MomentumConfigProvider _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "TAOM_Momentum_" + Path.GetRandomFileName());
        _featureDir = Path.Combine(_tempDir, "momentum");
        Directory.CreateDirectory(_featureDir);

        _pathService = Substitute.For<IPathService>();
        _pathService.ModuleDataPath.Returns(_tempDir);
        _logger = Substitute.For<IModLogger>();

        _sut = new MomentumConfigProvider(_pathService, _logger);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private void WriteConfig(string json) =>
        File.WriteAllText(Path.Combine(_featureDir, "momentum.json"), json);

    // ---- Load paths ----

    [TestMethod]
    public void GetConfig_ValidJson_ParsesAllFields()
    {
        WriteConfig(@"{
  ""enabled"": false,
  ""victoryThreshold"": 600,
  ""softCapDivisor"": 400.0,
  ""events"": { ""maxBattleMomentum"": 111, ""siegeMomentum"": 222, ""raidMomentum"": 333, ""armyMomentum"": 444, ""maxStrengthMomentum"": 555 },
  ""durationsHours"": { ""battleWon"": 100, ""siege"": 200, ""raid"": 300, ""armyGathered"": 400, ""relativeStrength"": 6 },
  ""player"": { ""requireParticipationForVictory"": false, ""participationMultiplier"": 2.0, ""minimumPlayerEventsForVictory"": 8 },
  ""strengthRatioForMaxMomentum"": 3.0
}");

        var config = _sut.GetConfig();

        Assert.IsFalse(config.Enabled);
        Assert.AreEqual(600, config.VictoryThreshold);
        Assert.AreEqual(400f, config.SoftCapDivisor, 0.001f);
        Assert.AreEqual(111, config.Events.MaxBattleMomentum);
        Assert.AreEqual(222, config.Events.SiegeMomentum);
        Assert.AreEqual(333, config.Events.RaidMomentum);
        Assert.AreEqual(444, config.Events.ArmyMomentum);
        Assert.AreEqual(555, config.Events.MaxStrengthMomentum);
        Assert.AreEqual(100, config.DurationsHours.BattleWon);
        Assert.AreEqual(200, config.DurationsHours.Siege);
        Assert.AreEqual(300, config.DurationsHours.Raid);
        Assert.AreEqual(400, config.DurationsHours.ArmyGathered);
        Assert.AreEqual(6, config.DurationsHours.RelativeStrength);
        Assert.IsFalse(config.Player.RequireParticipationForVictory);
        Assert.AreEqual(2.0f, config.Player.ParticipationMultiplier, 0.001f);
        Assert.AreEqual(8, config.Player.MinimumPlayerEventsForVictory);
        Assert.AreEqual(3.0f, config.StrengthRatioForMaxMomentum, 0.001f);
    }

    [TestMethod]
    public void GetConfig_MissingFile_ReturnsDefaultsAndWarns()
    {
        var config = _sut.GetConfig();

        Assert.AreEqual(500, config.VictoryThreshold);
        Assert.AreEqual(300, config.Events.MaxBattleMomentum);
        Assert.AreEqual(250, config.Events.SiegeMomentum);
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("not found")));
    }

    [TestMethod]
    public void GetConfig_MalformedJson_ReturnsDefaultsAndLogsError()
    {
        WriteConfig("{ not valid json !!");

        var config = _sut.GetConfig();

        Assert.AreEqual(500, config.VictoryThreshold);
        _logger.Received(1).LogError(Arg.Is<string>(s => s.Contains("Failed to parse")));
    }

    [TestMethod]
    public void GetConfig_NullLiteral_ReturnsDefaults()
    {
        WriteConfig("null");

        var config = _sut.GetConfig();

        Assert.AreEqual(500, config.VictoryThreshold);
    }

    [TestMethod]
    public void GetConfig_MissingNestedObjects_UsesNestedDefaults()
    {
        WriteConfig(@"{ ""victoryThreshold"": 700 }");

        var config = _sut.GetConfig();

        Assert.AreEqual(700, config.VictoryThreshold);
        Assert.AreEqual(300, config.Events.MaxBattleMomentum);
        Assert.AreEqual(504, config.DurationsHours.BattleWon);
        Assert.AreEqual(1.5f, config.Player.ParticipationMultiplier, 0.001f);
    }

    // ---- Semantic validation: one test per rule ----

    [TestMethod]
    public void GetConfig_VictoryThresholdZero_RevertsToDefault()
    {
        WriteConfig(@"{ ""victoryThreshold"": 0 }");
        Assert.AreEqual(500, _sut.GetConfig().VictoryThreshold);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("victoryThreshold")));
    }

    [TestMethod]
    public void GetConfig_SoftCapDivisorNaN_RevertsToDefault()
    {
        WriteConfig(@"{ ""softCapDivisor"": ""NaN"" }");
        Assert.AreEqual(300f, _sut.GetConfig().SoftCapDivisor, 0.001f);
    }

    [TestMethod]
    public void GetConfig_SoftCapDivisorZero_RevertsToDefault()
    {
        WriteConfig(@"{ ""softCapDivisor"": 0 }");
        Assert.AreEqual(300f, _sut.GetConfig().SoftCapDivisor, 0.001f);
    }

    [TestMethod]
    public void GetConfig_NegativeEventValue_RevertsToDefault()
    {
        WriteConfig(@"{ ""events"": { ""siegeMomentum"": -50 } }");
        Assert.AreEqual(250, _sut.GetConfig().Events.SiegeMomentum);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("siegeMomentum")));
    }

    [TestMethod]
    public void GetConfig_ZeroDuration_RevertsToDefault()
    {
        WriteConfig(@"{ ""durationsHours"": { ""battleWon"": 0 } }");
        Assert.AreEqual(504, _sut.GetConfig().DurationsHours.BattleWon);
    }

    [TestMethod]
    public void GetConfig_ParticipationMultiplierBelowOne_RevertsToDefault()
    {
        // A multiplier < 1 would silently flip "player involvement helps" into a penalty.
        WriteConfig(@"{ ""player"": { ""participationMultiplier"": 0.5 } }");
        Assert.AreEqual(1.5f, _sut.GetConfig().Player.ParticipationMultiplier, 0.001f);
    }

    [TestMethod]
    public void GetConfig_ParticipationMultiplierInfinity_RevertsToDefault()
    {
        WriteConfig(@"{ ""player"": { ""participationMultiplier"": ""Infinity"" } }");
        Assert.AreEqual(1.5f, _sut.GetConfig().Player.ParticipationMultiplier, 0.001f);
    }

    [TestMethod]
    public void GetConfig_NegativeMinPlayerEvents_RevertsToDefault()
    {
        WriteConfig(@"{ ""player"": { ""minimumPlayerEventsForVictory"": -1 } }");
        Assert.AreEqual(5, _sut.GetConfig().Player.MinimumPlayerEventsForVictory);
    }

    [TestMethod]
    public void GetConfig_StrengthRatioAtOne_RevertsToDefault()
    {
        // The strength award divides by (ratio − 1) — ratio must be strictly > 1.
        WriteConfig(@"{ ""strengthRatioForMaxMomentum"": 1.0 }");
        Assert.AreEqual(4.0f, _sut.GetConfig().StrengthRatioForMaxMomentum, 0.001f);
    }

    [TestMethod]
    public void GetConfig_AnyReversion_EmitsSummaryWarning()
    {
        WriteConfig(@"{ ""victoryThreshold"": -5 }");
        _sut.GetConfig();
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("invalid values")));
    }

    // ---- Duration lookup helper ----

    [TestMethod]
    public void GetHours_MapsEveryActionType()
    {
        var config = _sut.GetConfig();
        Assert.AreEqual(504, config.DurationsHours.GetHours(MomentumActionType.BattleWon));
        Assert.AreEqual(504, config.DurationsHours.GetHours(MomentumActionType.Sieges));
        Assert.AreEqual(504, config.DurationsHours.GetHours(MomentumActionType.VillageRaided));
        Assert.AreEqual(168, config.DurationsHours.GetHours(MomentumActionType.ArmyGathered));
        Assert.AreEqual(12, config.DurationsHours.GetHours(MomentumActionType.RelativeStrength));
    }
}
