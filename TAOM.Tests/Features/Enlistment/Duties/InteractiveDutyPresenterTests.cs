using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.Enlistment;
using TAOM.Features.Enlistment.Content;
using TAOM.Features.Enlistment.Content.Domain;
using TAOM.Features.Enlistment.Duties;

namespace TAOM.Tests.Features.Enlistment.Duties;

[TestClass]
public class InteractiveDutyPresenterTests
{
    private IInquiryAdapter _inquiry = null!;
    private ISkillCheckService _skillCheck = null!;
    private IHeroSkillXpAdapter _skillXp = null!;
    private IServiceRewardService _rewards = null!;
    private EnlistmentStore _store = null!;
    private EnlistmentContentStore _contentStore = null!;
    private InteractiveDutyPresenter _presenter = null!;

    private System.Action _capturedOptionA = null!;
    private System.Action _capturedOptionB = null!;

    [TestInitialize]
    public void Setup()
    {
        var logger = Substitute.For<IModLogger>();
        _inquiry = Substitute.For<IInquiryAdapter>();
        _skillCheck = Substitute.For<ISkillCheckService>();
        _skillXp = Substitute.For<IHeroSkillXpAdapter>();
        _rewards = Substitute.For<IServiceRewardService>();
        _store = new EnlistmentStore(logger);
        _contentStore = new EnlistmentContentStore(logger);
        _store.Record.EnlistedHeroId = "main_hero";

        _inquiry
            .When(x => x.ShowTwoOptionInquiry(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<System.Action>(), Arg.Any<System.Action>()))
            .Do(call =>
            {
                _capturedOptionA = call.ArgAt<System.Action>(8);
                _capturedOptionB = call.ArgAt<System.Action>(9);
            });

        _presenter = new InteractiveDutyPresenter(_inquiry, _skillCheck, _skillXp, _rewards, _store, _contentStore, Coop());

        // A duty only resolves while the player is actually serving — the callbacks fire a
        // frame after the popup, so a discharge in that window must not pay out.
        _store.Record.State = TAOM.Features.Enlistment.Domain.EnlistmentState.EnlistedAttached;
        _store.Record.EnlistedHeroId = "main_hero";
        _store.Record.CommanderHeroId = "lord_1";
    }

    private static ServiceProgressSnapshot Progress(ServiceRank rank = ServiceRank.Recruit) => new ServiceProgressSnapshot { Rank = rank };

    private static InteractiveDutyDefinition Duty() => new InteractiveDutyDefinition
    {
        Id = "night_patrol",
        OptionA = new DutyOptionSpec { Key = "walk_rounds", SkillId = "Scouting", Difficulty = 62, SuccessReward = new RewardSpec { ServiceXp = 30 }, FailureReward = new RewardSpec { ServiceXp = 8 } },
        OptionB = new DutyOptionSpec { Key = "double_watch", SkillId = "Athletics", Difficulty = 55, SuccessReward = new RewardSpec { ServiceXp = 24 }, FailureReward = new RewardSpec { ServiceXp = 6 } },
    };

    [TestMethod]
    public void PresentInteractiveDuty_NullDuty_DoesNotShowInquiry()
    {
        _presenter.PresentInteractiveDuty(null, Progress(), 0);

        _inquiry.DidNotReceiveWithAnyArgs().ShowTwoOptionInquiry(
            default, default, default, default, default, default, default, default, default, default);
    }

    [TestMethod]
    public void PresentInteractiveDuty_ShowsInquiryWithHumanizedOptionLabels()
    {
        _presenter.PresentInteractiveDuty(Duty(), Progress(), 0);

        _inquiry.Received(1).ShowTwoOptionInquiry(
            "taom_enlist_duty_night_patrol_title", Arg.Any<string>(),
            "taom_enlist_duty_night_patrol_body", Arg.Any<string>(),
            "taom_enlist_duty_night_patrol_opta", "Walk Rounds",
            "taom_enlist_duty_night_patrol_optb", "Double Watch",
            Arg.Any<System.Action>(), Arg.Any<System.Action>());
    }

    [TestMethod]
    public void OptionA_SkillCheckPasses_GrantsSuccessRewardAndIncrementsSuccesses()
    {
        var duty = Duty();
        _skillCheck.Passes(Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>()).Returns(true);

        _presenter.PresentInteractiveDuty(duty, Progress(), 5);
        _capturedOptionA();

        _rewards.Received(1).Grant(duty.OptionA.SuccessReward, "duty:night_patrol:walk_rounds");
        Assert.AreEqual(1, _contentStore.Record.DutySuccesses);
        Assert.AreEqual(0, _contentStore.Record.DutyFailures);
    }

    [TestMethod]
    public void OptionB_SkillCheckFails_GrantsFailureRewardAndIncrementsFailures()
    {
        var duty = Duty();
        _skillCheck.Passes(Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>()).Returns(false);

        _presenter.PresentInteractiveDuty(duty, Progress(), 5);
        _capturedOptionB();

        _rewards.Received(1).Grant(duty.OptionB.FailureReward, "duty:night_patrol:double_watch");
        Assert.AreEqual(1, _contentStore.Record.DutyFailures);
    }

    [TestMethod]
    public void ResolveOption_RankBonusApplies_PassesRankTimesFourToSkillCheck()
    {
        var duty = Duty();
        duty.OptionA.RankBonusApplies = true;
        _skillCheck.Passes(Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>()).Returns(true);

        _presenter.PresentInteractiveDuty(duty, Progress(ServiceRank.Veteran), 0);
        _capturedOptionA();

        _skillCheck.Received(1).Passes(Arg.Any<int>(), Arg.Any<int?>(), Arg.Is(0), Arg.Is(8), Arg.Is(62)); // Veteran=2 * 4 = 8
    }

    [TestMethod]
    public void ResolveOption_RankBonusDoesNotApply_PassesZeroRankBonus()
    {
        var duty = Duty();
        duty.OptionA.RankBonusApplies = false;
        _skillCheck.Passes(Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>()).Returns(true);

        _presenter.PresentInteractiveDuty(duty, Progress(ServiceRank.Veteran), 0);
        _capturedOptionA();

        _skillCheck.Received(1).Passes(Arg.Any<int>(), Arg.Any<int?>(), Arg.Is(0), Arg.Is(0), Arg.Is(62));
    }

    [TestMethod]
    public void ResolveOption_SecondarySkillPresent_ReadsSecondarySkillValue()
    {
        var duty = Duty();
        duty.OptionA.SecondarySkillId = "OneHanded";
        _skillXp.GetSkillValue("main_hero", "OneHanded").Returns(40);
        _skillCheck.Passes(Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>()).Returns(true);

        _presenter.PresentInteractiveDuty(duty, Progress(), 0);
        _capturedOptionA();

        _skillCheck.Received(1).Passes(Arg.Any<int>(), 40, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>());
    }

    [TestMethod]
    public void ResolveOption_NoSecondarySkill_PassesNullSecondary()
    {
        var duty = Duty();
        _skillCheck.Passes(Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>()).Returns(true);

        _presenter.PresentInteractiveDuty(duty, Progress(), 0);
        _capturedOptionA();

        _skillCheck.Received(1).Passes(Arg.Any<int>(), null, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>());
    }

    // ---- Incidents ----

    private static IncidentDefinition Incident(string effect = "") => new IncidentDefinition
    {
        Id = "pay_delay",
        Effect = effect,
        OptionA = new DutyOptionSpec { Key = "press_claim", SkillId = "Charm", Difficulty = 65, SuccessReward = new RewardSpec { ServiceXp = 20 }, FailureReward = new RewardSpec() },
        OptionB = new DutyOptionSpec { Key = "wait_it_out", SkillId = "Steward", Difficulty = 50, SuccessReward = new RewardSpec { ServiceXp = 16 }, FailureReward = new RewardSpec() },
    };

    [TestMethod]
    public void PresentIncident_NullIncident_DoesNotShowInquiry()
    {
        _presenter.PresentIncident(null, Progress(), 0);

        _inquiry.DidNotReceiveWithAnyArgs().ShowTwoOptionInquiry(
            default, default, default, default, default, default, default, default, default, default);
    }

    [TestMethod]
    public void PresentIncident_ShowsInquiryWithIncidentCopy()
    {
        _presenter.PresentIncident(Incident(), Progress(), 0);

        _inquiry.Received(1).ShowTwoOptionInquiry(
            "taom_enlist_duty_pay_delay_title", Arg.Any<string>(),
            "taom_enlist_duty_pay_delay_body", Arg.Any<string>(),
            "taom_enlist_duty_pay_delay_opta", "Press Claim",
            "taom_enlist_duty_pay_delay_optb", "Wait It Out",
            Arg.Any<System.Action>(), Arg.Any<System.Action>());
    }

    [TestMethod]
    public void IncidentOption_ReleaseDeferredPayEffectOnSuccess_ReleasesHalfArrearsAndGrantsGold()
    {
        var incident = Incident("ReleaseDeferredPay");
        _contentStore.Record.DeferredWages = 40;
        _skillCheck.Passes(Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>()).Returns(true);

        _presenter.PresentIncident(incident, Progress(), 0);
        _capturedOptionA();

        Assert.AreEqual(20, _contentStore.Record.DeferredWages); // 40 - max(8, 20) = 20
        _rewards.Received(1).Grant(
            Arg.Is<RewardSpec>(r => r.Gold == 20),
            "incident:pay_delay:deferred-release");
    }

    [TestMethod]
    public void IncidentOption_ReleaseDeferredPayEffectOnFailure_DoesNotReleaseArrears()
    {
        var incident = Incident("ReleaseDeferredPay");
        _contentStore.Record.DeferredWages = 40;
        _skillCheck.Passes(Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>()).Returns(false);

        _presenter.PresentIncident(incident, Progress(), 0);
        _capturedOptionA();

        Assert.AreEqual(40, _contentStore.Record.DeferredWages);
    }

    [TestMethod]
    public void IncidentOption_NoEffect_DoesNotTouchDeferredWages()
    {
        var incident = Incident(""); // short_rations / camp_discipline have no effect
        _contentStore.Record.DeferredWages = 40;
        _skillCheck.Passes(Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>()).Returns(true);

        _presenter.PresentIncident(incident, Progress(), 0);
        _capturedOptionA();

        Assert.AreEqual(40, _contentStore.Record.DeferredWages);
    }

    [TestMethod]
    public void IncidentOption_DeferredWagesBelowFloor_ReleasesFloorCappedByArrears()
    {
        var incident = Incident("ReleaseDeferredPay");
        _contentStore.Record.DeferredWages = 5; // below the 8-gold floor
        _skillCheck.Passes(Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>()).Returns(true);

        _presenter.PresentIncident(incident, Progress(), 0);
        _capturedOptionA();

        Assert.AreEqual(0, _contentStore.Record.DeferredWages, "min(arrears, max(floor, arrears/2)) caps at the arrears themselves");
    }

    private static ICoopSessionProvider Coop()
    {
        var coop = Substitute.For<ICoopSessionProvider>();
        coop.IsAuthority.Returns(true);
        return coop;
    }
}
