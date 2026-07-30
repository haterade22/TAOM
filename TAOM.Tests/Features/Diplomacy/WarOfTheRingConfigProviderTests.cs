using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.Diplomacy;

namespace TAOM.Tests.Features.Diplomacy;

[TestClass]
public class WarOfTheRingConfigProviderTests
{
    private IPathService _pathService;
    private IModLogger _logger;
    private string _moduleDataPath;
    private WarOfTheRingConfigProvider _sut;

    [TestInitialize]
    public void Setup()
    {
        _moduleDataPath = Path.Combine(Path.GetTempPath(), "taom_wotr_cfg_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_moduleDataPath, "diplomacy"));

        _pathService = Substitute.For<IPathService>();
        _pathService.ModuleDataPath.Returns(_moduleDataPath);
        _logger = Substitute.For<IModLogger>();

        _sut = new WarOfTheRingConfigProvider(_pathService, _logger);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_moduleDataPath))
            Directory.Delete(_moduleDataPath, recursive: true);
    }

    private void WriteConfig(string json)
    {
        File.WriteAllText(Path.Combine(_moduleDataPath, "diplomacy", "war_of_the_ring.json"), json);
    }

    // ---- Phase1 / Phase2 ordering invariant ----

    [TestMethod]
    public void LoadConfig_Phase2EqualsPhase1_RevertsPhase2ToPhase1PlusOne()
    {
        WriteConfig(@"{ ""phase1"": { ""triggerDay"": 30 }, ""phase2"": { ""triggerDay"": 30 } }");

        var config = _sut.LoadConfig();

        Assert.AreEqual(30, config.Phase1.TriggerDay);
        Assert.AreEqual(31, config.Phase2.TriggerDay);
        _logger.ReceivedWithAnyArgs().LogWarning(default);
    }

    [TestMethod]
    public void LoadConfig_Phase2BelowPhase1_RevertsPhase2ToPhase1PlusOne()
    {
        WriteConfig(@"{ ""phase1"": { ""triggerDay"": 30 }, ""phase2"": { ""triggerDay"": 10 } }");

        var config = _sut.LoadConfig();

        Assert.AreEqual(30, config.Phase1.TriggerDay);
        Assert.AreEqual(31, config.Phase2.TriggerDay);
    }

    [TestMethod]
    public void LoadConfig_Phase1BelowOne_RevertsToOne()
    {
        WriteConfig(@"{ ""phase1"": { ""triggerDay"": 0 }, ""phase2"": { ""triggerDay"": 44 } }");

        var config = _sut.LoadConfig();

        Assert.AreEqual(1, config.Phase1.TriggerDay);
        Assert.AreEqual(44, config.Phase2.TriggerDay);
    }

    [TestMethod]
    public void LoadConfig_ValidOrderedPhaseDays_LeavesThemUnchanged()
    {
        WriteConfig(@"{ ""phase1"": { ""triggerDay"": 30 }, ""phase2"": { ""triggerDay"": 44 } }");

        var config = _sut.LoadConfig();

        Assert.AreEqual(30, config.Phase1.TriggerDay);
        Assert.AreEqual(44, config.Phase2.TriggerDay);
    }

    // ---- TestMode ordering invariant (same collapse risk as the live phase days) ----

    [TestMethod]
    public void LoadConfig_TestModePhase2EqualsPhase1_RevertsPhase2ToPhase1PlusOne()
    {
        WriteConfig(@"{ ""testMode"": { ""phase1Day"": 3, ""phase2Day"": 3 } }");

        var config = _sut.LoadConfig();

        Assert.AreEqual(3, config.TestMode.Phase1Day);
        Assert.AreEqual(4, config.TestMode.Phase2Day);
    }

    [TestMethod]
    public void LoadConfig_TestModePhase2BelowPhase1_RevertsPhase2ToPhase1PlusOne()
    {
        WriteConfig(@"{ ""testMode"": { ""phase1Day"": 5, ""phase2Day"": 2 } }");

        var config = _sut.LoadConfig();

        Assert.AreEqual(5, config.TestMode.Phase1Day);
        Assert.AreEqual(6, config.TestMode.Phase2Day);
    }

    [TestMethod]
    public void LoadConfig_TestModePhase1DayBelowOne_RevertsToOne()
    {
        WriteConfig(@"{ ""testMode"": { ""phase1Day"": 0, ""phase2Day"": 3 } }");

        var config = _sut.LoadConfig();

        Assert.AreEqual(1, config.TestMode.Phase1Day);
        Assert.AreEqual(3, config.TestMode.Phase2Day);
    }

    [TestMethod]
    public void LoadConfig_ValidTestModeDays_LeavesThemUnchanged()
    {
        WriteConfig(@"{ ""testMode"": { ""phase1Day"": 1, ""phase2Day"": 3 } }");

        var config = _sut.LoadConfig();

        Assert.AreEqual(1, config.TestMode.Phase1Day);
        Assert.AreEqual(3, config.TestMode.Phase2Day);
    }

    // ---- Structural fallbacks ----

    [TestMethod]
    public void LoadConfig_NullSubConfigs_DefaultInitializesThem()
    {
        WriteConfig(@"{ ""phase1"": null, ""phase2"": null, ""testMode"": null }");

        var config = _sut.LoadConfig();

        Assert.IsNotNull(config.Phase1);
        Assert.IsNotNull(config.Phase2);
        Assert.IsNotNull(config.TestMode);
        Assert.IsTrue(config.Phase2.TriggerDay > config.Phase1.TriggerDay);
    }

    [TestMethod]
    public void LoadConfig_NullLiteralJson_ReturnsOrderedDefaults()
    {
        WriteConfig("null");

        var config = _sut.LoadConfig();

        Assert.IsNotNull(config);
        Assert.IsTrue(config.Phase2.TriggerDay > config.Phase1.TriggerDay);
    }

    [TestMethod]
    public void LoadConfig_MissingFile_ReturnsOrderedDefaultsAndLogsWarning()
    {
        var config = _sut.LoadConfig();

        Assert.IsNotNull(config);
        Assert.IsTrue(config.Phase2.TriggerDay > config.Phase1.TriggerDay);
        _logger.ReceivedWithAnyArgs().LogWarning(default);
    }

    [TestMethod]
    public void LoadConfig_MalformedJson_ReturnsOrderedDefaultsAndLogsError()
    {
        WriteConfig(@"{ ""phase1"": { ""triggerDay"": ");

        var config = _sut.LoadConfig();

        Assert.IsNotNull(config);
        Assert.IsTrue(config.Phase2.TriggerDay > config.Phase1.TriggerDay);
        _logger.ReceivedWithAnyArgs().LogError(default);
    }
}
