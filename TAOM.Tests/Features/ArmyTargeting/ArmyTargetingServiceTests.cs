using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.ArmyTargeting;

namespace TAOM.Tests.Features.ArmyTargeting;

[TestClass]
public class ArmyTargetingServiceTests
{
    private IArmyTargetingSettingsProvider _settings;
    private IArmyTargetingConfigProvider _configProvider;
    private ArmyTargetingConfig _config;

    [TestInitialize]
    public void Setup()
    {
        _settings = Substitute.For<IArmyTargetingSettingsProvider>();
        _configProvider = Substitute.For<IArmyTargetingConfigProvider>();

        _settings.EnableArmyStrategicIntelligence.Returns(true);
        _settings.CommitmentMultiplier.Returns(4.0f);
        _settings.MaxPriorityBoost.Returns(3.0f);

        _config = new ArmyTargetingConfig
        {
            FactionPriorityTargets = new Dictionary<string, List<string>>()
        };
        _configProvider.GetConfig().Returns(_config);
    }

    private ArmyTargetingService CreateSut() => new ArmyTargetingService(_settings, _configProvider);

    [TestMethod]
    public void GetTargetMultiplier_FeatureDisabled_ReturnsOne()
    {
        // Arrange
        _settings.EnableArmyStrategicIntelligence.Returns(false);
        var sut = CreateSut();

        // Act
        float result = sut.GetTargetMultiplier("settlement_a", "settlement_a", "mordor");

        // Assert
        Assert.AreEqual(1.0f, result, 0.001f);
    }

    [TestMethod]
    public void GetTargetMultiplier_CommittedTarget_ReturnsCommitmentMultiplier()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        float result = sut.GetTargetMultiplier("settlement_a", "settlement_a", null);

        // Assert
        Assert.AreEqual(4.0f, result, 0.001f);
    }

    [TestMethod]
    public void GetTargetMultiplier_NonCommittedTarget_ReturnsOne()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        float result = sut.GetTargetMultiplier("settlement_b", "settlement_a", null);

        // Assert
        Assert.AreEqual(1.0f, result, 0.001f);
    }

    [TestMethod]
    public void GetTargetMultiplier_NullCommittedTarget_ReturnsOne()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        float result = sut.GetTargetMultiplier("settlement_a", null, null);

        // Assert
        Assert.AreEqual(1.0f, result, 0.001f);
    }

    [TestMethod]
    public void GetTargetMultiplier_FirstPriorityTarget_ReturnsMaxBoost()
    {
        // Arrange
        _config.FactionPriorityTargets["mordor"] = new List<string> { "minas_tirith", "osgiliath", "cair_andros" };
        var sut = CreateSut();

        // Act
        float result = sut.GetTargetMultiplier("minas_tirith", null, "mordor");

        // Assert
        Assert.AreEqual(3.0f, result, 0.001f);
    }

    [TestMethod]
    public void GetTargetMultiplier_LastPriorityTarget_ReturnsOne()
    {
        // Arrange
        _config.FactionPriorityTargets["mordor"] = new List<string> { "minas_tirith", "osgiliath", "cair_andros" };
        var sut = CreateSut();

        // Act
        float result = sut.GetTargetMultiplier("cair_andros", null, "mordor");

        // Assert
        Assert.AreEqual(1.0f, result, 0.001f);
    }

    [TestMethod]
    public void GetTargetMultiplier_MiddlePriorityTarget_ReturnsInterpolated()
    {
        // Arrange
        // List of 3: idx=1, t = 1/(3-1) = 0.5, boost = 3.0 - 0.5*(3.0-1.0) = 3.0 - 1.0 = 2.0
        _config.FactionPriorityTargets["mordor"] = new List<string> { "minas_tirith", "osgiliath", "cair_andros" };
        var sut = CreateSut();

        // Act
        float result = sut.GetTargetMultiplier("osgiliath", null, "mordor");

        // Assert
        Assert.AreEqual(2.0f, result, 0.001f);
    }

    [TestMethod]
    public void GetTargetMultiplier_BothCommittedAndPriority_CombinesMultipliers()
    {
        // Arrange
        // Committed: 4.0x, first priority (idx=0, t=0): 3.0x → combined: 12.0x
        _config.FactionPriorityTargets["mordor"] = new List<string> { "minas_tirith", "osgiliath" };
        var sut = CreateSut();

        // Act
        float result = sut.GetTargetMultiplier("minas_tirith", "minas_tirith", "mordor");

        // Assert
        Assert.AreEqual(12.0f, result, 0.001f);
    }

    [TestMethod]
    public void GetTargetMultiplier_NullCulture_ReturnsCommitmentOnly()
    {
        // Arrange
        _config.FactionPriorityTargets["mordor"] = new List<string> { "minas_tirith" };
        var sut = CreateSut();

        // Act
        float result = sut.GetTargetMultiplier("minas_tirith", "minas_tirith", null);

        // Assert
        Assert.AreEqual(4.0f, result, 0.001f);
    }

    [TestMethod]
    public void GetTargetMultiplier_UnknownCulture_ReturnsCommitmentOnly()
    {
        // Arrange
        _config.FactionPriorityTargets["mordor"] = new List<string> { "minas_tirith" };
        var sut = CreateSut();

        // Act
        float result = sut.GetTargetMultiplier("minas_tirith", "minas_tirith", "gondor");

        // Assert
        Assert.AreEqual(4.0f, result, 0.001f);
    }

    [TestMethod]
    public void GetTargetMultiplier_EmptyPriorityList_IgnoresPriority()
    {
        // Arrange
        _config.FactionPriorityTargets["mordor"] = new List<string>();
        var sut = CreateSut();

        // Act
        float result = sut.GetTargetMultiplier("minas_tirith", null, "mordor");

        // Assert
        Assert.AreEqual(1.0f, result, 0.001f);
    }

    [TestMethod]
    public void GetTargetMultiplier_SingleEntryList_ReturnsMaxBoost()
    {
        // Arrange
        // Single entry: idx=0, total=1, t = 0/(1-1) = 0/0 → t=0 (guarded), boost = MaxPriorityBoost
        _config.FactionPriorityTargets["mordor"] = new List<string> { "minas_tirith" };
        var sut = CreateSut();

        // Act
        float result = sut.GetTargetMultiplier("minas_tirith", null, "mordor");

        // Assert
        Assert.AreEqual(3.0f, result, 0.001f);
    }
}
