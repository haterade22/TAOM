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
using TAOM.Features.WarOfTheRingMomentum.Models;

namespace TAOM.Tests.Features.CampaignStartOptions;

/// <summary>
/// Phase 2 rules follow "campaign, then MCM, then JSON". MCM is not loaded in a unit test, so the JSON (or the
/// compiled default) is the last step; each test makes it differ from the campaign's pinned value.
/// </summary>
[TestClass]
public class ExtraRulesProvidersPrecedenceTests
{
    private static ICampaignStartOptions Campaign() => Substitute.For<ICampaignStartOptions>();

    // ---- War can be won ----

    private static MomentumSettingsProvider Momentum(ICampaignStartOptions campaign, bool jsonVictory)
    {
        var config = Substitute.For<IMomentumConfigProvider>();
        config.GetConfig().Returns(new MomentumConfig { VictoryEnabled = jsonVictory });
        return new MomentumSettingsProvider(config, campaign);
    }

    [DataTestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void WarCanBeWon_PinnedChoice_BeatsTheJson(bool pinned)
    {
        var campaign = Campaign();
        campaign.MomentumVictory.Returns(pinned);

        var sut = Momentum(campaign, jsonVictory: !pinned);

        Assert.AreEqual(pinned, sut.VictoryEnabled);
        Assert.AreEqual(!pinned, sut.VictoryNewCampaignDefault, "the new-campaign default never reads the campaign");
    }

    [TestMethod]
    public void WarCanBeWon_NothingPinned_FollowsTheJson() =>
        Assert.IsTrue(Momentum(Campaign(), jsonVictory: true).VictoryEnabled);

    // ---- Eye of Sauron hunters ----

    [DataTestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void EyeHunters_PinnedChoice_Wins(bool pinned)
    {
        var campaign = Campaign();
        campaign.EyeOfSauron.Returns(pinned);

        var sut = new EyeOfSauronSettingsProvider(campaign);

        Assert.AreEqual(pinned, sut.Enabled);
        Assert.IsTrue(sut.EnabledNewCampaignDefault);
    }

    [TestMethod]
    public void EyeHunters_NothingPinned_FollowsTheDefault() =>
        Assert.IsTrue(new EyeOfSauronSettingsProvider(Campaign()).Enabled);

    // ---- Large AI armies ----

    [DataTestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void LargeAiArmies_PinnedChoice_Wins(bool pinned)
    {
        var campaign = Campaign();
        campaign.AiPartyScaling.Returns(pinned);

        var sut = new AiPartyScalingSettingsProvider(campaign);

        Assert.AreEqual(pinned, sut.Enabled);
        Assert.IsTrue(sut.NewCampaignDefault);
    }

    [TestMethod]
    public void LargeAiArmies_NothingPinned_FollowsTheDefault() =>
        Assert.IsTrue(new AiPartyScalingSettingsProvider(Campaign()).Enabled);

    // ---- Alignment rules ----

    private static ICampaignStartOptions WithPreset(AlignmentRules? rules)
    {
        var campaign = Campaign();
        campaign.AlignmentRules.Returns(rules);
        return campaign;
    }

    private static RecruitmentAlignmentSettingsProvider Recruitment(ICampaignStartOptions campaign, bool json)
    {
        var config = Substitute.For<IRecruitmentAlignmentConfigProvider>();
        config.GetConfig().Returns(new RecruitmentAlignmentConfig { Enabled = json });
        return new RecruitmentAlignmentSettingsProvider(config, campaign);
    }

    private static MarriageAlignmentSettingsProvider Marriage(ICampaignStartOptions campaign, bool json)
    {
        var config = Substitute.For<IMarriageAlignmentConfigProvider>();
        config.GetConfig().Returns(new MarriageAlignmentConfig { Enabled = json });
        return new MarriageAlignmentSettingsProvider(config, campaign);
    }

    private static AlignmentDesertionSettingsProvider Desertion(ICampaignStartOptions campaign, bool json)
    {
        var config = Substitute.For<IAlignmentDesertionConfigProvider>();
        config.GetConfig().Returns(new AlignmentDesertionConfig { Enabled = json });
        return new AlignmentDesertionSettingsProvider(config, Substitute.For<IRecruitmentAlignmentSettingsProvider>(), campaign);
    }

    private static HostilePeaceSettingsProvider HostilePeace(ICampaignStartOptions campaign, bool jsonAlliances)
    {
        var config = Substitute.For<IHostilePeaceConfigProvider>();
        config.Config.Returns(new HostilePeaceConfig { AllowPlayerAlliancesWithAnyone = jsonAlliances });
        return new HostilePeaceSettingsProvider(config, campaign);
    }

    [TestMethod]
    public void Strict_TurnsEveryBlockOn_AndForbidsAlliancesAcrossTheLine_EvenWhenTheJsonIsLoose()
    {
        var campaign = WithPreset(AlignmentRules.Strict);

        Assert.IsTrue(Recruitment(campaign, json: false).IsEnabled);
        Assert.IsTrue(Marriage(campaign, json: false).IsEnabled);
        Assert.IsTrue(Desertion(campaign, json: false).IsEnabled);
        Assert.IsFalse(HostilePeace(campaign, jsonAlliances: true).AllowPlayerAlliancesWithAnyone);
    }

    [TestMethod]
    public void Relaxed_TurnsEveryBlockOff_AndAllowsAlliances_EvenWhenTheJsonIsStrict()
    {
        var campaign = WithPreset(AlignmentRules.Relaxed);

        Assert.IsFalse(Recruitment(campaign, json: true).IsEnabled);
        Assert.IsFalse(Marriage(campaign, json: true).IsEnabled);
        Assert.IsFalse(Desertion(campaign, json: true).IsEnabled);
        Assert.IsTrue(HostilePeace(campaign, jsonAlliances: false).AllowPlayerAlliancesWithAnyone);
    }

    [TestMethod]
    public void Standard_PinsNothing_AndEveryAlignmentSwitchFollowsMcmThenJson()
    {
        var campaign = WithPreset(null);

        Assert.IsFalse(Recruitment(campaign, json: false).IsEnabled);
        Assert.IsTrue(Marriage(campaign, json: true).IsEnabled);
        Assert.IsFalse(Desertion(campaign, json: false).IsEnabled);
        Assert.IsTrue(HostilePeace(campaign, jsonAlliances: true).AllowPlayerAlliancesWithAnyone);
    }

    [TestMethod]
    public void ThePresetNeverChangesTheNewCampaignDefault()
    {
        var campaign = WithPreset(AlignmentRules.Relaxed);

        Assert.IsTrue(Recruitment(campaign, json: true).NewCampaignDefault);
        Assert.IsTrue(Marriage(campaign, json: true).NewCampaignDefault);
        Assert.IsTrue(Desertion(campaign, json: true).NewCampaignDefault);
        Assert.IsFalse(HostilePeace(campaign, jsonAlliances: false).AllowPlayerAlliancesNewCampaignDefault);
    }

    [TestMethod]
    public void TheTwoPresets_AreOpposites_AndStandardIsNotAPreset()
    {
        Assert.IsFalse(AlignmentRules.Strict.Matches(AlignmentRules.Relaxed));
        Assert.IsTrue(AlignmentRules.Strict.RecruitmentBlock && AlignmentRules.Strict.MarriageBlock && AlignmentRules.Strict.Desertion);
        Assert.IsFalse(AlignmentRules.Strict.AllianceWithAnyone);
        Assert.IsNull(AlignmentRules.ForPreset(CampaignStartOptionKeys.AlignmentStandardItem));
        Assert.IsNull(AlignmentRules.ForPreset("something_else"));
        Assert.AreSame(AlignmentRules.Strict, AlignmentRules.ForPreset(CampaignStartOptionKeys.AlignmentStrictItem));
        Assert.AreSame(AlignmentRules.Relaxed, AlignmentRules.ForPreset(CampaignStartOptionKeys.AlignmentRelaxedItem));
    }
}
