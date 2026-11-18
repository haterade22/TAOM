using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CampaignStartOptions;
using TAOM.Features.Diplomacy;
using TAOM.Features.Diplomacy.Models;

namespace TAOM.Tests.Features.Diplomacy;

/// <summary>
/// Precedence of the campaign's pinned choices in <see cref="WarOfTheRingService"/>: the pinned switch
/// beats MCM and the JSON (and holds where MCM is missing), the pinned days beat the MCM sliders and the
/// JSON pair but never a TestMode run, and the existing clamp still applies to them.
/// </summary>
[TestClass]
public class WarOfTheRingCampaignOptionsTests
{
    private IWarOfTheRingConfigProvider _configProvider = null!;
    private ITaomSettingsProvider _settings = null!;
    private ICampaignStartOptions _campaign = null!;
    private WarOfTheRingConfig _config = null!;

    [TestInitialize]
    public void Setup()
    {
        _config = new WarOfTheRingConfig
        {
            Enabled = true,
            Phase1 = new PhaseConfig { TriggerDay = 30, Wars = new List<WarDeclaration>() },
            Phase2 = new PhaseConfig { TriggerDay = 44, Wars = new List<WarDeclaration>() },
            TestMode = new TestModeConfig { Enabled = false, Phase1Day = 2, Phase2Day = 4 },
        };
        _configProvider = Substitute.For<IWarOfTheRingConfigProvider>();
        _configProvider.LoadConfig().Returns(_config);
        _settings = Substitute.For<ITaomSettingsProvider>();
        _settings.IsAvailable.Returns(true);
        _settings.WarOfTheRingEnabled.Returns(true);
        _settings.Phase1TriggerDay.Returns(30);
        _settings.Phase2TriggerDay.Returns(44);
        _settings.TestMode.Returns(false);
        _campaign = Substitute.For<ICampaignStartOptions>();
    }

    private WarOfTheRingService CreateSut() => new WarOfTheRingService(
        _configProvider, Substitute.For<IDiplomacyService>(), Substitute.For<IAllianceAdapter>(), _settings,
        _campaign, Substitute.For<IModLogger>());

    [TestMethod]
    public void NewCampaignDefaults_TakeMcm_IgnoringTestModeAndThePin()
    {
        _settings.WarOfTheRingEnabled.Returns(false);
        _settings.Phase1TriggerDay.Returns(25);
        _settings.Phase2TriggerDay.Returns(50);
        _settings.TestMode.Returns(true);
        _campaign.WarOfTheRing.Returns(true);
        _campaign.WarPhaseDays.Returns((60, 90));

        var sut = CreateSut();

        Assert.IsFalse(sut.NewCampaignEnabled);
        Assert.AreEqual((25, 50), sut.NewCampaignPhaseDays);
    }

    [TestMethod]
    public void NewCampaignDefaults_TakeTheJson_WhenMcmIsMissing()
    {
        _settings.IsAvailable.Returns(false);
        _config.Enabled = false;

        var sut = CreateSut();

        Assert.IsFalse(sut.NewCampaignEnabled);
        Assert.AreEqual((30, 44), sut.NewCampaignPhaseDays);
    }

    [TestMethod]
    public void NothingPinned_FollowsMcm()
    {
        _settings.WarOfTheRingEnabled.Returns(false);

        Assert.IsFalse(CreateSut().IsEnabled);
    }

    [TestMethod]
    public void PinnedOn_BeatsMcmOff()
    {
        _settings.WarOfTheRingEnabled.Returns(false);
        _campaign.WarOfTheRing.Returns(true);

        Assert.IsTrue(CreateSut().IsEnabled);
    }

    [TestMethod]
    public void PinnedOff_BeatsMcmOn()
    {
        _campaign.WarOfTheRing.Returns(false);

        Assert.IsFalse(CreateSut().IsEnabled);
    }

    [TestMethod]
    public void PinnedSwitch_IsUsedWhenMcmIsMissing()
    {
        _settings.IsAvailable.Returns(false);
        _config.Enabled = true;
        _campaign.WarOfTheRing.Returns(false);

        Assert.IsFalse(CreateSut().IsEnabled);
    }

    [TestMethod]
    public void PinnedOff_StopsThePhaseTransitions()
    {
        _campaign.WarOfTheRing.Returns(false);
        var sut = CreateSut();

        sut.CheckPhaseTransition(500f);

        Assert.AreEqual(WarPhase.Peace, sut.CurrentPhase);
    }

    [TestMethod]
    public void PinnedDays_BeatTheMcmSliders()
    {
        _campaign.WarPhaseDays.Returns((60, 90));
        var sut = CreateSut();

        Assert.AreEqual(60, sut.GetEffectivePhaseDay(WarPhase.IsengardWar));
        Assert.AreEqual(90, sut.GetEffectivePhaseDay(WarPhase.FullWar));
    }

    [TestMethod]
    public void PinnedDays_AreUsedWhenMcmIsMissing()
    {
        _settings.IsAvailable.Returns(false);
        _campaign.WarPhaseDays.Returns((10, 20));
        var sut = CreateSut();

        Assert.AreEqual(10, sut.GetEffectivePhaseDay(WarPhase.IsengardWar));
        Assert.AreEqual(20, sut.GetEffectivePhaseDay(WarPhase.FullWar));
    }

    [TestMethod]
    public void McmTestMode_StillBeatsThePinnedDays()
    {
        _settings.TestMode.Returns(true);
        _campaign.WarPhaseDays.Returns((60, 90));
        var sut = CreateSut();

        Assert.AreEqual(2, sut.GetEffectivePhaseDay(WarPhase.IsengardWar));
        Assert.AreEqual(4, sut.GetEffectivePhaseDay(WarPhase.FullWar));
    }

    [TestMethod]
    public void JsonTestMode_StillBeatsThePinnedDays()
    {
        _config.TestMode.Enabled = true;
        _campaign.WarPhaseDays.Returns((60, 90));
        var sut = CreateSut();

        Assert.AreEqual(2, sut.GetEffectivePhaseDay(WarPhase.IsengardWar));
    }

    [TestMethod]
    public void PinnedDays_StillGoThroughTheClamp()
    {
        _campaign.WarPhaseDays.Returns((50, 50));
        var sut = CreateSut();

        Assert.AreEqual(50, sut.GetEffectivePhaseDay(WarPhase.IsengardWar));
        Assert.AreEqual(51, sut.GetEffectivePhaseDay(WarPhase.FullWar));
    }

    [TestMethod]
    public void NothingPinned_KeepsTheMcmSliders()
    {
        var sut = CreateSut();

        Assert.AreEqual(30, sut.GetEffectivePhaseDay(WarPhase.IsengardWar));
        Assert.AreEqual(44, sut.GetEffectivePhaseDay(WarPhase.FullWar));
    }
}
