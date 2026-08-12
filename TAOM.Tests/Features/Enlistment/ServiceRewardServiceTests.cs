using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Enlistment;
using TAOM.Features.Enlistment.Content;
using TAOM.Features.Enlistment.Content.Domain;
using TAOM.Features.Enlistment.Domain;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>
/// Orchestration tests for the wage/reward chokepoint. These exist because the pure
/// WagePolicy tests passed while PayDailyWage double-paid arrears (mint mode ran BOTH the
/// commander transfer and the mint) and double-counted the transfer shortfall — the pure
/// function was right and the orchestration was wrong.
/// </summary>
[TestClass]
public class ServiceRewardServiceTests
{
    private IHeroRenownAdapter _renown = null!;
    private IModLogger _logger = null!;
    private EnlistmentStore _store = null!;
    private EnlistmentContentStore _contentStore = null!;
    private IEnlistmentContentConfigProvider _config = null!;
    private EnlistmentContentConfig _configValue = null!;
    private IHeroSkillXpAdapter _skillXp = null!;
    private IGoldGiftAdapter _goldGift = null!;
    private IGoldTransferAdapter _goldTransfer = null!;
    private ICommanderLordAdapter _commander = null!;
    private ServiceRewardService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _store = new EnlistmentStore(_logger);
        _contentStore = new EnlistmentContentStore(_logger);
        _configValue = EnlistmentContentConfigProvider.BuildDefaults();
        _config = Substitute.For<IEnlistmentContentConfigProvider>();
        _config.GetConfig().Returns(_ => _configValue);
        _skillXp = Substitute.For<IHeroSkillXpAdapter>();
        _goldGift = Substitute.For<IGoldGiftAdapter>();
        _goldTransfer = Substitute.For<IGoldTransferAdapter>();
        _commander = Substitute.For<ICommanderLordAdapter>();
        var playerParty = Substitute.For<IPlayerPartyAdapter>();
        playerParty.GetMainHeroId().Returns("main_hero");
        _renown = Substitute.For<IHeroRenownAdapter>();
        _service = new ServiceRewardService(
            _store, _contentStore, _config, _skillXp, _goldGift, _goldTransfer, _commander, playerParty,
            _renown, _logger);

        _store.Record.State = EnlistmentState.EnlistedAttached;
        _store.Record.EnlistedHeroId = "main_hero";
        _store.Record.CommanderHeroId = "lord_1_1";

        // Commander pays in full unless a test says otherwise.
        _goldTransfer.GetHeroGold("lord_1_1").Returns(10000);
        _goldTransfer.TransferToPlayer("lord_1_1", Arg.Any<int>()).Returns(call => call.ArgAt<int>(1));
    }

    /// <summary>
    /// The wallet projection and the payment must name the SAME condition.
    ///
    /// <c>GetDailyWage</c> gated only on <c>IsEnlisted</c>, which spans five states, while
    /// <c>EnlistmentDailyService.RunDailyTick</c> early-returns before <c>PayDailyWage</c> whenever
    /// the state is <c>CommanderUnavailable</c> — there is no chain of command left to pay anyone.
    /// The clan gold-change tooltip therefore promised income on exactly the days none arrived, for
    /// a grace window up to a week long, and that tooltip is the one surface a player checks when
    /// they suspect they are not being paid. Found by the deep-review data-flow pass, 2026-08-11.
    /// </summary>
    [TestMethod]
    public void GetDailyWage_CommanderUnavailable_PromisesNothing()
    {
        _store.Record.State = EnlistmentState.CommanderUnavailable;

        Assert.AreEqual(0, _service.GetDailyWage());
    }

    [TestMethod]
    public void GetDailyWage_Attached_PromisesTheRankWage()
    {
        Assert.AreEqual(5, _service.GetDailyWage(), "recruit wage");
    }

    [TestMethod]
    public void GetDailyWage_NotEnlisted_PromisesNothing()
    {
        _store.Record.State = EnlistmentState.NotEnlisted;
        _store.Record.EnlistedHeroId = null;
        _store.Record.CommanderHeroId = null;

        Assert.AreEqual(0, _service.GetDailyWage());
    }

    /// <summary>
    /// Every state the DAILY TICK pays a wage in must project one, or the tooltip under-promises —
    /// the same class of lie in the opposite direction. The tick's only exclusion is
    /// <c>CommanderUnavailable</c>, so every other enlisted state has to show the wage.
    /// </summary>
    [DataTestMethod]
    [DataRow(EnlistmentState.EnlistedAttached)]
    [DataRow(EnlistmentState.EnlistedBattle)]
    [DataRow(EnlistmentState.EnlistedPlayerCaptive)]
    [DataRow(EnlistmentState.EnlistedDetachedOnDuty)]
    public void GetDailyWage_EveryOtherEnlistedState_StillPromisesTheWage(EnlistmentState state)
    {
        _store.Record.State = state;

        Assert.AreEqual(5, _service.GetDailyWage());
    }

    [TestMethod]
    public void PayDailyWage_SolventCommander_TransfersWageOnlyNoMint()
    {
        var decision = _service.PayDailyWage();

        Assert.AreEqual(5, decision.PaidFromCommander, "recruit wage");
        Assert.AreEqual(0, decision.Minted);
        _goldTransfer.Received(1).TransferToPlayer("lord_1_1", 5);
        _goldGift.DidNotReceive().GiveGoldToHero(Arg.Any<string>(), Arg.Any<int>());
        Assert.AreEqual(0, _contentStore.Record.DeferredWages);
    }

    [TestMethod]
    public void PayDailyWage_BrokeCommander_DefersWholeWage()
    {
        _goldTransfer.GetHeroGold("lord_1_1").Returns(400); // below the 500 floor

        var decision = _service.PayDailyWage();

        Assert.AreEqual(0, decision.PaidFromCommander);
        Assert.AreEqual(5, decision.NewlyDeferred);
        Assert.AreEqual(5, _contentStore.Record.DeferredWages);
        _goldTransfer.DidNotReceive().TransferToPlayer(Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void PayDailyWage_SolventAgain_ClearsArrearsExactlyOnce()
    {
        _contentStore.Record.DeferredWages = 20;

        var decision = _service.PayDailyWage();

        // 5 wage + 20 arrears = 25 delivered, through the commander channel only.
        _goldTransfer.Received(1).TransferToPlayer("lord_1_1", 25);
        _goldGift.DidNotReceive().GiveGoldToHero(Arg.Any<string>(), Arg.Any<int>());
        Assert.AreEqual(5, decision.PaidFromCommander);
        Assert.AreEqual(20, decision.ArrearsReleased);
        Assert.AreEqual(0, _contentStore.Record.DeferredWages);
    }

    [TestMethod]
    public void PayDailyWage_MintMode_MintsOnlyAndNeverTouchesCommanderGold()
    {
        // The HIGH bug: mint mode used to ALSO transfer arrears from the commander and
        // then mint them again — the player got paid ~1.67x and the lord was drained.
        _configValue.WagePolicy.PayFromCommanderGold = false;
        _contentStore.Record.DeferredWages = 20;

        var decision = _service.PayDailyWage();

        _goldTransfer.DidNotReceive().TransferToPlayer(Arg.Any<string>(), Arg.Any<int>());
        _goldGift.Received(1).GiveGoldToHero("main_hero", 25);
        Assert.AreEqual(5, decision.Minted);
        Assert.AreEqual(20, decision.ArrearsReleased);
        Assert.AreEqual(0, decision.PaidFromCommander);
        Assert.AreEqual(0, _contentStore.Record.DeferredWages);
    }

    [TestMethod]
    public void PayDailyWage_TransferShortfall_LedgerMatchesConservation()
    {
        // Prior debt 5, wage 5 (recruit) → 10 requested; only 8 lands.
        // Owed 10, delivered 8 → remaining debt is exactly 2 (the old code said 4).
        _contentStore.Record.DeferredWages = 5;
        _goldTransfer.TransferToPlayer("lord_1_1", 10).Returns(8);

        _service.PayDailyWage();

        Assert.AreEqual(2, _contentStore.Record.DeferredWages);
    }

    [TestMethod]
    public void PayDailyWage_ArrearsNeverExceedCap()
    {
        // 2 days x the recruit's 5/day = a 10 gold ceiling, already full.
        _configValue.WagePolicy.MaxDeferredWageDays = 2;
        _contentStore.Record.DeferredWages = 10;
        _goldTransfer.GetHeroGold("lord_1_1").Returns(0);

        var decision = _service.PayDailyWage();

        Assert.AreEqual(10, _contentStore.Record.DeferredWages, "overflow forfeited, never exceeds the cap");
        Assert.AreEqual(5, decision.Forfeited, "today's whole wage was destroyed by the cap");
    }

    [TestMethod]
    public void PayDailyWage_CapReached_LogsTheForfeitedGold()
    {
        // The real defect: the clamp destroyed the player's back pay in total silence. Roughly
        // 600 gold evaporated over a 30-day insolvent stretch with nothing in the log.
        _configValue.WagePolicy.MaxDeferredWageDays = 2;
        _contentStore.Record.DeferredWages = 10;
        _goldTransfer.GetHeroGold("lord_1_1").Returns(0);

        _service.PayDailyWage();

        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("forfeited") && s.Contains("5")));
    }

    [TestMethod]
    public void PayDailyWage_CapNotReached_LogsNoForfeit()
    {
        _contentStore.Record.DeferredWages = 20;
        _goldTransfer.GetHeroGold("lord_1_1").Returns(0);

        _service.PayDailyWage();

        Assert.AreEqual(25, _contentStore.Record.DeferredWages, "5 wage + 20 prior, all inside the 70-gold recruit cap");
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(s => s.Contains("forfeited")));
    }

    [TestMethod]
    public void PayDailyWage_SergeantInsolvent_CapScalesWithRankWageInsteadOfDestroyingBackPay()
    {
        // Under the old flat 60-GOLD cap this Sergeant's arrears were clamped to 60 and 162
        // gold vanished on this single tick. The cap is now 14 days x 22/day = 308.
        _contentStore.Record.Rank = ServiceRank.Sergeant;
        _contentStore.Record.DeferredWages = 200;
        _goldTransfer.GetHeroGold("lord_1_1").Returns(0);

        var decision = _service.PayDailyWage();

        Assert.AreEqual(222, _contentStore.Record.DeferredWages, "22 wage + 200 prior, all still owed");
        Assert.AreEqual(0, decision.Forfeited);
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(s => s.Contains("forfeited")));
    }

    [TestMethod]
    public void PayDailyWage_CapShrinksBelowBankedArrears_ForfeitsOnlyTodaysAccrual()
    {
        // A wage-relative ceiling can SHRINK (demotion, retuned wage table). A bare Min()
        // against it would confiscate arrears the player legitimately banked at the old wage.
        _contentStore.Record.Rank = ServiceRank.Sergeant;   // 22/day, cap 2 x 22 = 44
        _configValue.WagePolicy.MaxDeferredWageDays = 2;
        _contentStore.Record.DeferredWages = 300;
        _goldTransfer.GetHeroGold("lord_1_1").Returns(0);

        var decision = _service.PayDailyWage();

        Assert.AreEqual(300, _contentStore.Record.DeferredWages, "banked debt survives a shrinking cap");
        Assert.AreEqual(22, decision.Forfeited, "only today's refused accrual is lost");
    }

    [TestMethod]
    public void PayDailyWage_ZeroWageTable_DoesNotWipeStandingArrears()
    {
        // wage 0 makes the day-denominated cap 0. That must not mean "destroy everything owed".
        _configValue.Progression.DailyWageByRank = new List<int> { 0, 0, 0, 0 };
        _contentStore.Record.DeferredWages = 200;
        _goldTransfer.GetHeroGold("lord_1_1").Returns(0);

        var decision = _service.PayDailyWage();

        Assert.AreEqual(200, _contentStore.Record.DeferredWages);
        Assert.AreEqual(0, decision.Forfeited);
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(s => s.Contains("forfeited")));
    }

    [TestMethod]
    public void PayDailyWage_ArrearsNeverGoNegative()
    {
        _contentStore.Record.DeferredWages = 0;

        _service.PayDailyWage();

        Assert.IsTrue(_contentStore.Record.DeferredWages >= 0);
    }

    [TestMethod]
    public void PayDailyWage_HigherRank_UsesRankWage()
    {
        _contentStore.Record.Rank = ServiceRank.Sergeant;

        var decision = _service.PayDailyWage();

        Assert.AreEqual(22, decision.PaidFromCommander);
    }

    // ---- Grant ----

    [TestMethod]
    public void Grant_FullReward_RoutesEveryChannel()
    {
        _service.Grant(new RewardSpec
        {
            ServiceXp = 30,
            Gold = 20,
            SkillId = "Scouting",
            SkillXp = 15,
            Trust = 2,
            Relation = 1,
            RepDomain = ReputationDomain.Field,
            RepAmount = 3,
        }, "test");

        Assert.AreEqual(30, _contentStore.Record.ServiceXp);
        _goldGift.Received(1).GiveGoldToHero("main_hero", 20);
        _skillXp.Received(1).AddSkillXp("main_hero", "Scouting", 15);
        _commander.Received(1).ApplyPlayerRelation("lord_1_1", 1);
        Assert.AreEqual(2, _contentStore.Record.Trust);
        Assert.AreEqual(3, _contentStore.Record.FieldRep);
    }

    [TestMethod]
    public void Grant_ZeroFields_NoSideEffects()
    {
        _service.Grant(new RewardSpec(), "empty");

        _goldGift.DidNotReceive().GiveGoldToHero(Arg.Any<string>(), Arg.Any<int>());
        _skillXp.DidNotReceive().AddSkillXp(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<float>());
        _commander.DidNotReceive().ApplyPlayerRelation(Arg.Any<string>(), Arg.Any<int>());
        Assert.AreEqual(0, _contentStore.Record.Trust);
    }

    [TestMethod]
    public void Grant_Null_DoesNotThrow()
    {
        _service.Grant(null, "null");
    }

    [TestMethod]
    public void AdjustTrust_ClampsAtConfiguredCeiling()
    {
        _contentStore.Record.Trust = 19;

        _service.AdjustTrust(10);

        Assert.AreEqual(20, _contentStore.Record.Trust);
    }

    [TestMethod]
    public void Grant_RenownGoesToThePlayerNotTheCommander()
    {
        // The relation sink above pays the COMMANDER's id; renown must not follow it there. Serving
        // in another lord's army builds your own clan's name — paying the commander would make the
        // reward invisible to the player, which is the field report this fixes, not a fix for it.
        _store.Record.EnlistedHeroId = "main_hero";
        _store.Record.CommanderHeroId = "lord_1_1";

        _service.Grant(new RewardSpec { Renown = 3 }, "battle-won");

        _renown.Received(1).AddClanRenown("main_hero", 3);
        _renown.DidNotReceive().AddClanRenown("lord_1_1", Arg.Any<int>());
    }

    [TestMethod]
    public void Grant_ZeroRenown_DoesNotTouchTheAdapter()
    {
        _service.Grant(new RewardSpec { ServiceXp = 5 }, "duty");

        _renown.DidNotReceive().AddClanRenown(Arg.Any<string>(), Arg.Any<int>());
    }
}
