using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.AiPartySize;
using TAOM.Features.AlignmentDesertion;
using TAOM.Features.AlignmentRecruitment;
using TAOM.Features.CampaignStartOptions;
using TAOM.Features.EyeOfSauron;
using TAOM.Features.HostilePeace;
using TAOM.Features.MarriageAlignment;
using TAOM.Features.WarOfTheRingMomentum;
using TAOM.Features.Diplomacy;
using TAOM.Features.Diplomacy.Models;
using TAOM.Features.TroopWeight;

namespace TAOM.Tests.Features.CampaignStartOptions;

[TestClass]
public class CampaignStartDefaultsProviderTests
{
    private IWarOfTheRingService _wotr = null!;
    private ITroopWeightSettingsProvider _weight = null!;
    private IMomentumSettingsProvider _momentum = null!;
    private IEyeOfSauronSettings _eye = null!;
    private IAiPartyScalingSettingsProvider _scaling = null!;
    private IRecruitmentAlignmentSettingsProvider _recruitment = null!;
    private IMarriageAlignmentSettingsProvider _marriage = null!;
    private IAlignmentDesertionSettingsProvider _desertion = null!;
    private IHostilePeaceSettingsProvider _hostilePeace = null!;

    [TestInitialize]
    public void Setup()
    {
        _wotr = Substitute.For<IWarOfTheRingService>();
        _wotr.NewCampaignPhaseDays.Returns((31, 45));
        _weight = Substitute.For<ITroopWeightSettingsProvider>();
        _momentum = Substitute.For<IMomentumSettingsProvider>();
        _eye = Substitute.For<IEyeOfSauronSettings>();
        _scaling = Substitute.For<IAiPartyScalingSettingsProvider>();
        _recruitment = Substitute.For<IRecruitmentAlignmentSettingsProvider>();
        _marriage = Substitute.For<IMarriageAlignmentSettingsProvider>();
        _desertion = Substitute.For<IAlignmentDesertionSettingsProvider>();
        _hostilePeace = Substitute.For<IHostilePeaceSettingsProvider>();
    }

    private CampaignStartDefaultsProvider Create() => new CampaignStartDefaultsProvider(
        _wotr, _weight, _momentum, _eye, _scaling, _recruitment, _marriage, _desertion, _hostilePeace);

    [TestMethod]
    public void TheExtraRules_TakeEachProvidersNewCampaignDefault_NotTheCampaignValue()
    {
        _momentum.VictoryNewCampaignDefault.Returns(true);
        _momentum.VictoryEnabled.Returns(false); // the pinned choice of a loaded campaign must not leak in
        _eye.EnabledNewCampaignDefault.Returns(false);
        _scaling.NewCampaignDefault.Returns(false);
        _recruitment.NewCampaignDefault.Returns(false);
        _marriage.NewCampaignDefault.Returns(true);
        _desertion.NewCampaignDefault.Returns(false);
        _hostilePeace.AllowPlayerAlliancesNewCampaignDefault.Returns(true);

        var d = Create().Current();

        Assert.IsTrue(d.MomentumVictory);
        Assert.IsFalse(d.EyeOfSauron);
        Assert.IsFalse(d.AiPartyScaling);
        Assert.IsTrue(d.Alignment.Matches(new AlignmentRules(false, true, false, true)));
    }

    [TestMethod]
    public void TakesTheWarOfTheRingDefaultsFromTheService_AndTheOtherSwitchesFromTheirProviders()
    {
        _wotr.NewCampaignEnabled.Returns(true);
        _wotr.NewCampaignPhaseDays.Returns((25, 50));
        _weight.NewCampaignDefault.Returns(false);
        _weight.IsEnabled.Returns(true); // a loaded campaign's pinned choice must not leak into the defaults

        var d = Create().Current();

        Assert.IsTrue(d.WarOfTheRing);
        Assert.AreEqual(25, d.Phase1Day);
        Assert.AreEqual(50, d.Phase2Day);
        Assert.IsFalse(d.TroopWeight);
    }

    [TestMethod]
    public void AnInvertedPair_IsClampedOnce_SoTheMismatchCheckSeesTheSamePairThePinWrites()
    {
        _wotr.NewCampaignPhaseDays.Returns((50, 20));

        var d = Create().Current();

        Assert.AreEqual((50, 51), (d.Phase1Day, d.Phase2Day));

        // A Standard campaign pinned from these defaults is not flagged as different from MCM.
        var pinned = Substitute.For<ICampaignStartOptions>();
        pinned.IsPinned.Returns(true);
        pinned.WarPhaseDays.Returns((d.Phase1Day, d.Phase2Day));
        Assert.IsFalse(CampaignStartMismatch.Differs(pinned, d));
    }
}
