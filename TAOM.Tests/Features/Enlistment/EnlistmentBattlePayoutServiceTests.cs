using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Enlistment;
using TAOM.Features.Enlistment.Content;
using TAOM.Features.Enlistment.Content.Domain;
using TAOM.Features.Enlistment.Domain;

namespace TAOM.Tests.Features.Enlistment;

[TestClass]
public class EnlistmentBattlePayoutServiceTests
{
    private IModLogger _logger = null!;
    private EnlistmentStore _store = null!;
    private EnlistmentContentStore _contentStore = null!;
    private IEnlistmentContentConfigProvider _config = null!;
    private BattleMeritAccumulator _accumulator = null!;
    private IServiceRewardService _rewards = null!;
    private IHeroSkillXpAdapter _skillXp = null!;
    private EnlistmentBattlePayoutService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _store = new EnlistmentStore(_logger);
        _contentStore = new EnlistmentContentStore(_logger);
        _config = Substitute.For<IEnlistmentContentConfigProvider>();
        _config.GetConfig().Returns(EnlistmentContentConfigProvider.BuildDefaults());
        _accumulator = new BattleMeritAccumulator();
        _rewards = Substitute.For<IServiceRewardService>();
        _skillXp = Substitute.For<IHeroSkillXpAdapter>();
        var promotion = new PromotionService(_contentStore, _config, _skillXp, _store, _logger);
        _ledger = new SitOutLedger();
        _service = new EnlistmentBattlePayoutService(
            _store, _contentStore, _config, _accumulator, _rewards, promotion, _ledger, _logger);
    }

    private SitOutLedger _ledger = null!;

    private static bool PaysRenown(RewardSpec r) => r.Renown > 0;

    private static MeritSample Fought() => new MeritSample { Kills = 0, SurvivalRatio = 1f };

    private void MakeEnlisted()
    {
        _store.Record.State = EnlistmentState.EnlistedAttached;
        _store.Record.EnlistedHeroId = "main_hero";
        _store.Record.CommanderHeroId = "lord_1_1";
    }

    [TestMethod]
    public void PayOut_NotEnlisted_ReturnsFalseAndDropsStaleSample()
    {
        _accumulator.Submit(new MeritSample { Kills = 5 });

        Assert.IsFalse(_service.PayOutBattle(won: true, playerSimulated: false));
        Assert.IsNull(_accumulator.Consume(), "stale sample must not survive into the next service term");
    }

    [TestMethod]
    public void PayOut_WonNoSample_AwardsBaseXp()
    {
        MakeEnlisted();

        Assert.IsTrue(_service.PayOutBattle(won: true, playerSimulated: false));
        Assert.AreEqual(40, _contentStore.Record.ServiceXp);
        Assert.AreEqual("", _service.LastBandGradeKey);
    }

    [TestMethod]
    public void PayOut_Lost_AwardsLossXp()
    {
        MakeEnlisted();

        _service.PayOutBattle(won: false, playerSimulated: false);

        Assert.AreEqual(15, _contentStore.Record.ServiceXp);
    }

    [TestMethod]
    public void PayOut_KillsExceedCap_XpCappedAtKillXpCap()
    {
        MakeEnlisted();
        _accumulator.Submit(new MeritSample { Kills = 40, SurvivalRatio = 1f });

        _service.PayOutBattle(won: true, playerSimulated: false);

        // 40 win XP + capped 10 kills * 25 = 290. Uncapped would be 1040.
        Assert.AreEqual(290, _contentStore.Record.ServiceXp);
    }

    [TestMethod]
    public void PayOut_HighMeritSample_GrantsDistinguishedBand()
    {
        MakeEnlisted();
        _accumulator.Submit(new MeritSample
        {
            Kills = 6, SurvivalRatio = 1f, CohesionRatio = 1f,
            CommanderProximityRatio = 1f, EngagementRatio = 1f,
        });

        _service.PayOutBattle(won: true, playerSimulated: false);

        Assert.AreEqual("distinguished", _service.LastBandGradeKey);
        _rewards.Received(1).Grant(
            Arg.Is<RewardSpec>(r => r.ServiceXp == 30 && r.Gold == 20 && r.Trust == 2),
            "merit-distinguished");
    }

    /// <summary>
    /// Leaving the field never pays standing, and the guard is here rather than in the band ladder
    /// on purpose. The ladder's own protection is emergent: it holds only while no band a walkout
    /// can reach happens to pay trust, and the walkout ceiling (45 with a full kill count, because
    /// leftFieldPenalty cancels the survival weight and leaves the other four terms standing) sits
    /// close enough to the boundaries that one tuning edit puts it over. `MeritTrustFloorTests` pins
    /// the shipped numbers; this pins the rule, so a future ladder cannot reintroduce it.
    ///
    /// Only trust is withheld. XP and gold still reflect what the player actually did before he
    /// left, which is the same reasoning that made LeftTheField zero the survival term alone.
    /// </summary>
    [TestMethod]
    public void PayOut_LeftTheField_WithholdsBandTrustEvenWhenTheBandPaysIt()
    {
        MakeEnlisted();
        var config = EnlistmentContentConfigProvider.BuildDefaults();
        // A deliberately generous ladder: every score pays trust. The gate must hold anyway.
        config.MeritBands = new System.Collections.Generic.List<MeritBand>
        {
            new MeritBand { MinScore = 0, ServiceXp = 12, Gold = 5, Trust = 5, GradeKey = "rough" },
        };
        _config.GetConfig().Returns(config);

        _accumulator.Submit(new MeritSample
        {
            Kills = 6, SurvivalRatio = 1f, CohesionRatio = 1f,
            CommanderProximityRatio = 1f, EngagementRatio = 1f, RoleFit = true,
            LeftTheField = true,
        });

        _service.PayOutBattle(won: false, playerSimulated: false);

        _rewards.Received(1).Grant(
            Arg.Is<RewardSpec>(r => r.Trust == 0 && r.ServiceXp == 12 && r.Gold == 5),
            "merit-rough");
    }

    [TestMethod]
    public void PayOut_StayedToTheEnd_StillReceivesTheBandTrust()
    {
        MakeEnlisted();
        var config = EnlistmentContentConfigProvider.BuildDefaults();
        config.MeritBands = new System.Collections.Generic.List<MeritBand>
        {
            new MeritBand { MinScore = 0, ServiceXp = 12, Gold = 5, Trust = 5, GradeKey = "rough" },
        };
        _config.GetConfig().Returns(config);

        _accumulator.Submit(new MeritSample { Kills = 6, SurvivalRatio = 1f, LeftTheField = false });

        _service.PayOutBattle(won: true, playerSimulated: false);

        _rewards.Received(1).Grant(Arg.Is<RewardSpec>(r => r.Trust == 5), "merit-rough");
    }

    [TestMethod]
    public void PayOut_SampleConsumedExactlyOnce()
    {
        MakeEnlisted();
        _accumulator.Submit(new MeritSample { Kills = 3, SurvivalRatio = 1f });

        _service.PayOutBattle(won: true, playerSimulated: false);
        var xpAfterFirst = _contentStore.Record.ServiceXp;
        _service.PayOutBattle(won: true, playerSimulated: false);

        Assert.AreEqual(xpAfterFirst + 40, _contentStore.Record.ServiceXp,
            "second battle sees no sample — base XP only");
        Assert.AreEqual("", _service.LastBandGradeKey);
    }

    [TestMethod]
    public void PayOut_BattleXpCrossesThreshold_PromotesImmediately()
    {
        // The second promotion evaluation point: a battle that earns the last XP needed
        // must promote NOW, not on the next daily rollover.
        MakeEnlisted();
        _contentStore.Record.DaysServed = 10;
        _contentStore.Record.ServiceXp = 70; // +40 win = 110 >= 100

        _service.PayOutBattle(won: true, playerSimulated: false);

        Assert.IsTrue(_service.LastBattlePromoted);
        Assert.AreEqual(ServiceRank.Soldier, _service.LastPromotedRank);
        Assert.AreEqual(ServiceRank.Soldier, _contentStore.Record.Rank);
    }

    [TestMethod]
    public void PayOut_CommanderUnavailable_EvaluatesNoPromotion()
    {
        // No chain of command is left to promote anyone (#835); the daily tick refuses it too.
        MakeEnlisted();
        _store.Record.State = EnlistmentState.CommanderUnavailable;
        _contentStore.Record.DaysServed = 10;
        _contentStore.Record.ServiceXp = 70; // +40 win = 110 >= 100: promotes in any other state

        _service.PayOutBattle(won: true, playerSimulated: false);

        Assert.IsFalse(_service.LastBattlePromoted);
        Assert.AreEqual(ServiceRank.Recruit, _contentStore.Record.Rank);
    }

    [TestMethod]
    public void PayOut_BelowThreshold_NoPromotion()
    {
        MakeEnlisted();
        _contentStore.Record.DaysServed = 2; // days gate unmet

        _service.PayOutBattle(won: true, playerSimulated: false);

        Assert.IsFalse(_service.LastBattlePromoted);
        Assert.AreEqual(ServiceRank.Recruit, _contentStore.Record.Rank);
    }

    [TestMethod]
    public void PayOut_SimulatedNoSampleOverwhelming_HalfXpNoRenownOneTrustCost()
    {
        MakeEnlisted();
        _ledger.Record(SitOutVerdict.OverwhelmingCost);

        Assert.IsTrue(_service.PayOutBattle(won: true, playerSimulated: true));

        Assert.AreEqual(20, _contentStore.Record.ServiceXp, "half of the 40 win XP");
        _rewards.DidNotReceive().Grant(Arg.Is<RewardSpec>(r => PaysRenown(r)), Arg.Any<string>());
        _rewards.Received(1).Grant(Arg.Is<RewardSpec>(r => r.Trust == -1 && r.Renown == 0 && r.ServiceXp == 0), Arg.Any<string>());
        Assert.AreEqual(SitOutVerdict.OverwhelmingCost, _service.LastSatOutReason);
    }

    [DataTestMethod]
    [DataRow(SitOutVerdict.WoundedFree)]
    [DataRow(SitOutVerdict.ForcedOpen)]
    public void PayOut_SimulatedNoSampleWoundedOrForcedOpen_FullXpNoRenownNoTrustCost(SitOutVerdict recorded)
    {
        MakeEnlisted();
        _ledger.Record(recorded);

        _service.PayOutBattle(won: true, playerSimulated: true);

        Assert.AreEqual(40, _contentStore.Record.ServiceXp);
        _rewards.DidNotReceive().Grant(Arg.Is<RewardSpec>(r => PaysRenown(r)), Arg.Any<string>());
        _rewards.DidNotReceive().Grant(Arg.Is<RewardSpec>(r => r.Trust != 0), Arg.Any<string>());
        Assert.AreEqual(recorded, _service.LastSatOutReason);
    }

    [DataTestMethod]
    [DataRow(SitOutVerdict.Unchanged)]
    [DataRow(SitOutVerdict.RefusedField)]
    [DataRow(SitOutVerdict.RefusedSiege)]
    public void PayOut_SimulatedNoSampleButNoAllowedVerdict_IsPaidNormallyWithRenown(SitOutVerdict recorded)
    {
        // The simulation did not come from an option the gate let him use (an empty or refused
        // ledger), so it is not a battle he was allowed to sit out.
        MakeEnlisted();
        _ledger.Record(recorded);

        _service.PayOutBattle(won: true, playerSimulated: true);

        Assert.AreEqual(40, _contentStore.Record.ServiceXp);
        Assert.IsNull(_service.LastSatOutReason);
        _rewards.Received(1).Grant(Arg.Is<RewardSpec>(r => r.Renown == 2), "battle-won");
        _rewards.DidNotReceive().Grant(Arg.Is<RewardSpec>(r => r.Trust != 0), Arg.Any<string>());
    }

    [TestMethod]
    public void PayOut_SimulatedLossOverwhelming_HalvesTheLossXpToo()
    {
        MakeEnlisted();
        _ledger.Record(SitOutVerdict.OverwhelmingCost);

        _service.PayOutBattle(won: false, playerSimulated: true);

        Assert.AreEqual(8, _contentStore.Record.ServiceXp, "15 x 0.5 = 7.5 rounds up to 8 (to-even gives 8 too)");
    }

    [TestMethod]
    public void PayOut_SimulatedOverwhelmingHalfPoint_RoundsAwayFromZeroNotToEven()
    {
        // 13 x 0.5 = 6.5: away from zero gives 7, rounding to even would give 6.
        MakeEnlisted();
        var config = EnlistmentContentConfigProvider.BuildDefaults();
        config.Progression.BattleWinXp = 13;
        _config.GetConfig().Returns(config);
        _ledger.Record(SitOutVerdict.OverwhelmingCost);

        _service.PayOutBattle(won: true, playerSimulated: true);

        Assert.AreEqual(7, _contentStore.Record.ServiceXp);
    }

    [TestMethod]
    public void PayOut_SimulatedWithAFoughtSample_IsPaidAsNormal()
    {
        MakeEnlisted();
        _ledger.Record(SitOutVerdict.OverwhelmingCost);
        _accumulator.Submit(Fought());

        _service.PayOutBattle(won: true, playerSimulated: true);

        Assert.AreEqual(40, _contentStore.Record.ServiceXp, "full base XP (band XP goes through the mocked reward service)");
        Assert.IsNull(_service.LastSatOutReason);
        _rewards.Received().Grant(Arg.Is<RewardSpec>(r => r.Renown == 2), "battle-won");
        _rewards.DidNotReceive().Grant(Arg.Is<RewardSpec>(r => r.Trust == -1), Arg.Any<string>());
    }

    [TestMethod]
    public void PayOut_WalkoutNeverDownedWithOverwhelmingVerdict_IsSatOutButKeepsBandPay()
    {
        // He walked out of the mission without being downed: the sit-out rule governs base pay and
        // renown, while the sample still pays what he did before he left.
        MakeEnlisted();
        _ledger.Record(SitOutVerdict.OverwhelmingCost);
        _accumulator.Submit(new MeritSample { LeftTheField = true, SurvivalRatio = 1f });

        _service.PayOutBattle(won: true, playerSimulated: true);

        Assert.AreEqual(SitOutVerdict.OverwhelmingCost, _service.LastSatOutReason);
        Assert.AreEqual(20, _contentStore.Record.ServiceXp);
        _rewards.Received().Grant(Arg.Is<RewardSpec>(r => r.ServiceXp > 0), Arg.Is<string>(s => s.StartsWith("merit-")));
        _rewards.DidNotReceive().Grant(Arg.Is<RewardSpec>(r => PaysRenown(r)), Arg.Any<string>());
        _rewards.Received(1).Grant(Arg.Is<RewardSpec>(r => r.Trust == -1), "sat-out");
    }

    [TestMethod]
    public void PayOut_WalkoutAfterBeingDowned_IsNotSatOut()
    {
        MakeEnlisted();
        _ledger.Record(SitOutVerdict.OverwhelmingCost);
        _accumulator.Submit(new MeritSample { LeftTheField = true, SurvivalRatio = 0f, FellEarly = true });

        _service.PayOutBattle(won: true, playerSimulated: true);

        Assert.IsNull(_service.LastSatOutReason);
        Assert.AreEqual(40, _contentStore.Record.ServiceXp);
        _rewards.Received().Grant(Arg.Is<RewardSpec>(r => r.Renown == 2), "battle-won");
    }

    [TestMethod]
    public void PayOut_NotSimulatedNoSample_IsPaidAsNormal()
    {
        MakeEnlisted();
        _ledger.Record(SitOutVerdict.OverwhelmingCost);

        _service.PayOutBattle(won: true, playerSimulated: false);

        Assert.AreEqual(40, _contentStore.Record.ServiceXp);
        Assert.IsNull(_service.LastSatOutReason);
        _rewards.Received(1).Grant(Arg.Is<RewardSpec>(r => r.Renown == 2), "battle-won");
        _rewards.DidNotReceive().Grant(Arg.Is<RewardSpec>(r => r.Trust != 0), Arg.Any<string>());
    }

    [TestMethod]
    public void PayOut_AnyOutcome_ConsumesTheLedger()
    {
        MakeEnlisted();
        _ledger.Record(SitOutVerdict.OverwhelmingCost);
        _service.PayOutBattle(won: true, playerSimulated: false);
        Assert.AreEqual(SitOutVerdict.Unchanged, _ledger.Consume());

        _ledger.Record(SitOutVerdict.OverwhelmingCost);
        _service.PayOutBattle(won: true, playerSimulated: true);
        Assert.AreEqual(SitOutVerdict.Unchanged, _ledger.Consume());
    }

    [TestMethod]
    public void PayOut_NotEnlisted_StillConsumesTheLedger()
    {
        _ledger.Record(SitOutVerdict.WoundedFree);

        Assert.IsFalse(_service.PayOutBattle(won: true, playerSimulated: true));

        Assert.AreEqual(SitOutVerdict.Unchanged, _ledger.Consume());
    }

    [TestMethod]
    public void PayOut_NextBattle_ResetsTheSatOutReason()
    {
        MakeEnlisted();
        _ledger.Record(SitOutVerdict.ForcedOpen);
        _service.PayOutBattle(won: true, playerSimulated: true);
        Assert.IsNotNull(_service.LastSatOutReason);

        _service.PayOutBattle(won: true, playerSimulated: false);

        Assert.IsNull(_service.LastSatOutReason);
    }

    [TestMethod]
    public void PayOut_ZeroXpFactorAndTrustCost_PaysNoXpAndNoTrust()
    {
        MakeEnlisted();
        var config = EnlistmentContentConfigProvider.BuildDefaults();
        config.SitOut.BaseXpFactor = 0f;
        config.SitOut.TrustCost = 0;
        _config.GetConfig().Returns(config);
        _ledger.Record(SitOutVerdict.OverwhelmingCost);

        _service.PayOutBattle(won: true, playerSimulated: true);

        Assert.AreEqual(0, _contentStore.Record.ServiceXp);
        _rewards.DidNotReceive().Grant(Arg.Is<RewardSpec>(r => r.Trust != 0), Arg.Any<string>());
    }
}
