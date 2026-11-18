using TAOM.Features.CampaignStartOptions;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Diplomacy;
using TAOM.Features.Diplomacy.Models;

namespace TAOM.Tests.Features.Diplomacy;

/// <summary>
/// <c>IWarOfTheRingService.GetEffectivePhaseDay</c> (#765, the War Chronicle's phase anchors): the same
/// clamped pair <c>CheckPhaseTransition</c> compares against, read live. It is the configured trigger day,
/// not the day the phase was entered (a war switched on late enters its phase after this day).
/// </summary>
[TestClass]
public class WarOfTheRingPhaseDayTests
{
    private IWarOfTheRingConfigProvider _configProvider = null!;
    private ITaomSettingsProvider _settings = null!;
    private WarOfTheRingConfig _config = null!;

    [TestInitialize]
    public void Setup()
    {
        _config = new WarOfTheRingConfig
        {
            Enabled = true,
            Phase1 = new PhaseConfig { TriggerDay = 30, Wars = new List<WarDeclaration>() },
            Phase2 = new PhaseConfig { TriggerDay = 44 },
            TestMode = new TestModeConfig { Enabled = false, Phase1Day = 1, Phase2Day = 3 },
        };
        _configProvider = Substitute.For<IWarOfTheRingConfigProvider>();
        _configProvider.LoadConfig().Returns(_config);
        _settings = Substitute.For<ITaomSettingsProvider>();
        _settings.IsAvailable.Returns(false);
    }

    private WarOfTheRingService CreateSut() => new WarOfTheRingService(
        _configProvider, Substitute.For<IDiplomacyService>(), Substitute.For<IAllianceAdapter>(), _settings,
        Substitute.For<ICampaignStartOptions>(), Substitute.For<IModLogger>());

    [TestMethod]
    public void GetEffectivePhaseDay_IsengardWar_ReturnsThePhase1Day()
    {
        Assert.AreEqual(30, CreateSut().GetEffectivePhaseDay(WarPhase.IsengardWar));
    }

    [TestMethod]
    public void GetEffectivePhaseDay_FullWar_ReturnsThePhase2Day()
    {
        Assert.AreEqual(44, CreateSut().GetEffectivePhaseDay(WarPhase.FullWar));
    }

    [TestMethod]
    public void GetEffectivePhaseDay_McmTestMode_ReturnsTheTestModeDays()
    {
        _settings.IsAvailable.Returns(true);
        _settings.TestMode.Returns(true);

        var sut = CreateSut();

        Assert.AreEqual(1, sut.GetEffectivePhaseDay(WarPhase.IsengardWar));
        Assert.AreEqual(3, sut.GetEffectivePhaseDay(WarPhase.FullWar));
    }

    [TestMethod]
    public void GetEffectivePhaseDay_InvertedMcmPair_ReturnsTheClampedPhase2Day()
    {
        _settings.IsAvailable.Returns(true);
        _settings.TestMode.Returns(false);
        _settings.Phase1TriggerDay.Returns(50);
        _settings.Phase2TriggerDay.Returns(20);

        var sut = CreateSut();

        Assert.AreEqual(50, sut.GetEffectivePhaseDay(WarPhase.IsengardWar));
        Assert.AreEqual(51, sut.GetEffectivePhaseDay(WarPhase.FullWar), "phase 2 lands strictly after phase 1, as the machine clamps it");
    }

    [DataTestMethod]
    [DataRow(WarPhase.Peace)]
    [DataRow(WarPhase.WarEnded)]
    [DataRow((WarPhase)99)]
    public void GetEffectivePhaseDay_PhaseWithNoTriggerDay_ReturnsNull(WarPhase phase)
    {
        Assert.IsNull(CreateSut().GetEffectivePhaseDay(phase));
    }
}
