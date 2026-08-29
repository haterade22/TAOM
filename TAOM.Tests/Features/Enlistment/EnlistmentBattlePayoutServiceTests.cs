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
        _service = new EnlistmentBattlePayoutService(
            _store, _contentStore, _config, _accumulator, _rewards, promotion, _logger);
    }

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

        Assert.IsFalse(_service.PayOutBattle(won: true));
        Assert.IsNull(_accumulator.Consume(), "stale sample must not survive into the next service term");
    }

    [TestMethod]
    public void PayOut_WonNoSample_AwardsBaseXpAndCountsVictory()
    {
        MakeEnlisted();

        Assert.IsTrue(_service.PayOutBattle(won: true));
        Assert.AreEqual(40, _contentStore.Record.ServiceXp);
        Assert.AreEqual(1, _contentStore.Record.BattleVictories);
        Assert.AreEqual("", _service.LastBandGradeKey);
    }

    [TestMethod]
    public void PayOut_Lost_AwardsLossXpAndCountsDefeat()
    {
        MakeEnlisted();

        _service.PayOutBattle(won: false);

        Assert.AreEqual(15, _contentStore.Record.ServiceXp);
        Assert.AreEqual(1, _contentStore.Record.BattleDefeats);
    }

    [TestMethod]
    public void PayOut_KillsExceedCap_XpCappedAtKillXpCap()
    {
        MakeEnlisted();
        _accumulator.Submit(new MeritSample { Kills = 40, SurvivalRatio = 1f });

        _service.PayOutBattle(won: true);

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

        _service.PayOutBattle(won: true);

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

        _service.PayOutBattle(won: false);

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

        _service.PayOutBattle(won: true);

        _rewards.Received(1).Grant(Arg.Is<RewardSpec>(r => r.Trust == 5), "merit-rough");
    }

    [TestMethod]
    public void PayOut_SampleConsumedExactlyOnce()
    {
        MakeEnlisted();
        _accumulator.Submit(new MeritSample { Kills = 3, SurvivalRatio = 1f });

        _service.PayOutBattle(won: true);
        var xpAfterFirst = _contentStore.Record.ServiceXp;
        _service.PayOutBattle(won: true);

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

        _service.PayOutBattle(won: true);

        Assert.IsTrue(_service.LastBattlePromoted);
        Assert.AreEqual(ServiceRank.Soldier, _service.LastPromotedRank);
        Assert.AreEqual(ServiceRank.Soldier, _contentStore.Record.Rank);
    }

    [TestMethod]
    public void PayOut_BelowThreshold_NoPromotion()
    {
        MakeEnlisted();
        _contentStore.Record.DaysServed = 2; // days gate unmet

        _service.PayOutBattle(won: true);

        Assert.IsFalse(_service.LastBattlePromoted);
        Assert.AreEqual(ServiceRank.Recruit, _contentStore.Record.Rank);
    }
}
