using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.Enlistment.Content;
using TAOM.Features.Enlistment.Content.Domain;
using TAOM.Features.TroopProgression;

namespace TAOM.Tests.Features.Enlistment;

[TestClass]
public class EnlistmentContentPureLogicTests
{
    // ---- WagePolicy: solvency × arrears matrix ----

    private static WagePolicyConfig Wages(bool fromCommander = true, int floor = 500, int maxDeferred = 60) =>
        new WagePolicyConfig { PayFromCommanderGold = fromCommander, CommanderGoldFloor = floor, MaxDeferredWages = maxDeferred };

    [TestMethod]
    public void Wage_SolventCommander_PaysFullNoArrears()
    {
        var d = WagePolicy.ComputeDaily(14, commanderGold: 2000, currentArrears: 0, Wages());

        Assert.AreEqual(14, d.PaidFromCommander);
        Assert.AreEqual(0, d.Minted);
        Assert.AreEqual(0, d.NewlyDeferred);
        Assert.AreEqual(0, d.ArrearsReleased);
    }

    [TestMethod]
    public void Wage_CommanderAtFloor_DefersAll()
    {
        var d = WagePolicy.ComputeDaily(14, commanderGold: 500, currentArrears: 0, Wages());

        Assert.AreEqual(0, d.PaidFromCommander);
        Assert.AreEqual(14, d.NewlyDeferred);
    }

    [TestMethod]
    public void Wage_PartialSolvency_SplitsPayAndDeferral()
    {
        var d = WagePolicy.ComputeDaily(14, commanderGold: 508, currentArrears: 0, Wages());

        Assert.AreEqual(8, d.PaidFromCommander);
        Assert.AreEqual(6, d.NewlyDeferred);
    }

    [TestMethod]
    public void Wage_ArrearsCapForfeitsOverflow()
    {
        var d = WagePolicy.ComputeDaily(14, commanderGold: 0, currentArrears: 55, Wages());

        Assert.AreEqual(5, d.NewlyDeferred, "only room to 60");
    }

    [TestMethod]
    public void Wage_SolventAgain_ReleasesArrearsAfterWage()
    {
        var d = WagePolicy.ComputeDaily(14, commanderGold: 600, currentArrears: 40, Wages());

        Assert.AreEqual(14, d.PaidFromCommander);
        Assert.AreEqual(40, d.ArrearsReleased, "100 available - 14 wage leaves 86 >= 40 arrears");
    }

    [TestMethod]
    public void Wage_PartialArrearsRelease_CappedByRemainingGold()
    {
        var d = WagePolicy.ComputeDaily(14, commanderGold: 534, currentArrears: 40, Wages());

        Assert.AreEqual(14, d.PaidFromCommander);
        Assert.AreEqual(20, d.ArrearsReleased, "34 available - 14 wage = 20");
    }

    [TestMethod]
    public void Wage_MintedMode_MintsWageAndReleasesArrears()
    {
        var d = WagePolicy.ComputeDaily(14, commanderGold: 0, currentArrears: 25, Wages(fromCommander: false));

        Assert.AreEqual(0, d.PaidFromCommander);
        Assert.AreEqual(14, d.Minted);
        Assert.AreEqual(25, d.ArrearsReleased);
    }

    [TestMethod]
    public void Wage_NegativeInputs_AllZero()
    {
        var d = WagePolicy.ComputeDaily(-5, commanderGold: -100, currentArrears: -3, Wages());

        Assert.AreEqual(0, d.TotalPaidToPlayer);
        Assert.AreEqual(0, d.NewlyDeferred);
    }

    // ---- PromotionEvaluator ----

    private static List<PromotionRequirement> Ladder() => new List<PromotionRequirement>
    {
        new PromotionRequirement { ToRank = ServiceRank.Soldier, MinDaysServed = 7, MinServiceXp = 100 },
        new PromotionRequirement { ToRank = ServiceRank.Veteran, MinDaysServed = 25, MinServiceXp = 350, MinLeadershipSkill = 20, MinDutySuccesses = 2 },
        new PromotionRequirement { ToRank = ServiceRank.Sergeant, MinDaysServed = 60, MinServiceXp = 800, MinLeadershipSkill = 50, MinDutySuccesses = 5, MinTrust = 6 },
    };

    [TestMethod]
    public void Promotion_AllThresholdsMet_Promotes()
    {
        var progress = new ServiceProgressSnapshot { Rank = ServiceRank.Recruit, DaysServed = 8, ServiceXp = 120 };

        var eval = PromotionEvaluator.Evaluate(progress, Ladder());

        Assert.IsTrue(eval.Promote);
        Assert.AreEqual(ServiceRank.Soldier, eval.ToRank);
    }

    [TestMethod]
    public void Promotion_UnmetThresholds_ListsEveryGap()
    {
        var progress = new ServiceProgressSnapshot
        {
            Rank = ServiceRank.Veteran, DaysServed = 30, ServiceXp = 400,
            LeadershipSkill = 10, DutySuccesses = 1, Trust = 0,
        };

        var eval = PromotionEvaluator.Evaluate(progress, Ladder());

        Assert.IsFalse(eval.Promote);
        CollectionAssert.AreEquivalent(
            new[] { "days", "xp", "leadership", "dutySuccesses", "trust" },
            eval.UnmetRequirementKeys);
    }

    [TestMethod]
    public void Promotion_AtTopRank_NoPromotionNoGaps()
    {
        var eval = PromotionEvaluator.Evaluate(
            new ServiceProgressSnapshot { Rank = ServiceRank.Sergeant }, Ladder());

        Assert.IsFalse(eval.Promote);
        Assert.IsTrue(eval.AtTopRank);
        Assert.AreEqual(0, eval.UnmetRequirementKeys.Count);
    }

    // ---- BattleMeritScorer ----

    private static MeritScoringConfig Scoring() => new MeritScoringConfig();

    [TestMethod]
    public void Merit_PerfectBattle_Clamps100()
    {
        var score = BattleMeritScorer.Score(new MeritSample
        {
            Kills = 20, SurvivalRatio = 1f, CohesionRatio = 1f,
            CommanderProximityRatio = 1f, EngagementRatio = 1f, RoleFit = true,
        }, Scoring());

        Assert.AreEqual(100, score, "kills capped at 6*5=30, +25+15+10+10+10 = 100");
    }

    [TestMethod]
    public void Merit_FellEarlyNothingElse_ClampsToZero()
    {
        var score = BattleMeritScorer.Score(new MeritSample { FellEarly = true }, Scoring());

        Assert.AreEqual(0, score);
    }

    [TestMethod]
    public void Merit_NaNRatios_ContributeZeroNotInflate()
    {
        var score = BattleMeritScorer.Score(new MeritSample
        {
            Kills = 2,
            SurvivalRatio = float.NaN,
            CohesionRatio = float.PositiveInfinity,
            CommanderProximityRatio = float.NegativeInfinity,
            EngagementRatio = float.NaN,
        }, Scoring());

        Assert.AreEqual(10, score, "only 2 kills * 5 count");
    }

    [TestMethod]
    public void Merit_BandResolution_FirstBandAtOrBelowScore()
    {
        var bands = new List<MeritBand>
        {
            new MeritBand { MinScore = 80, GradeKey = "distinguished" },
            new MeritBand { MinScore = 60, GradeKey = "strong" },
            new MeritBand { MinScore = 40, GradeKey = "solid" },
            new MeritBand { MinScore = 0, GradeKey = "rough" },
        };

        Assert.AreEqual("distinguished", BattleMeritScorer.ResolveBand(95, bands).GradeKey);
        Assert.AreEqual("strong", BattleMeritScorer.ResolveBand(60, bands).GradeKey);
        Assert.AreEqual("solid", BattleMeritScorer.ResolveBand(59, bands).GradeKey);
        Assert.AreEqual("rough", BattleMeritScorer.ResolveBand(0, bands).GradeKey);
    }

    // ---- SkillCheckService ----

    [TestMethod]
    public void SkillCheck_DeterministicRoll_ExactBoundaryPasses()
    {
        var random = Substitute.For<IRandomProvider>();
        random.Next(SkillCheckService.RollRange).Returns(10);
        var check = new SkillCheckService(random);

        Assert.IsTrue(check.Passes(40, null, trust: 5, rankBonus: 0, difficulty: 60), "40+10+10=60 >= 60");
        Assert.IsFalse(check.Passes(40, null, trust: 5, rankBonus: 0, difficulty: 61));
    }

    [TestMethod]
    public void SkillCheck_NegativeTrust_NoPenaltyBelowZero()
    {
        var random = Substitute.For<IRandomProvider>();
        random.Next(SkillCheckService.RollRange).Returns(0);
        var check = new SkillCheckService(random);

        Assert.IsTrue(check.Passes(60, null, trust: -8, rankBonus: 0, difficulty: 60), "negative trust adds 0, never subtracts");
    }

    [TestMethod]
    public void SkillCheck_BestOfTwo_UsesHigherSkill()
    {
        var random = Substitute.For<IRandomProvider>();
        random.Next(SkillCheckService.RollRange).Returns(0);
        var check = new SkillCheckService(random);

        Assert.IsTrue(check.Passes(10, 70, trust: 0, rankBonus: 0, difficulty: 70));
    }

    // ---- TrustLedger ----

    [TestMethod]
    public void Trust_ClampsBothEnds()
    {
        var config = new SchedulerConfig();

        Assert.AreEqual(20, TrustLedger.AdjustTrust(19, 5, config));
        Assert.AreEqual(-10, TrustLedger.AdjustTrust(-9, -5, config));
    }

    [TestMethod]
    public void Reputation_ClampsZeroToMax()
    {
        var config = new SchedulerConfig();
        var record = new ServiceContentRecord { FieldRep = 49 };

        TrustLedger.ApplyReputation(record, ReputationDomain.Field, 5, config);
        Assert.AreEqual(50, record.FieldRep);

        TrustLedger.ApplyReputation(record, ReputationDomain.Field, -60, config);
        Assert.AreEqual(0, record.FieldRep);
    }

    [TestMethod]
    public void Reputation_DominantDomain_TiesGoToFirstHighest()
    {
        var record = new ServiceContentRecord { FieldRep = 3, CommandRep = 7, SiegeRep = 7 };

        Assert.AreEqual(ReputationDomain.Command, TrustLedger.DominantDomain(record));
    }

    // ---- ServiceContentRecord round-trip ----

    [TestMethod]
    public void ContentRecord_RoundTrip_PreservesEverything()
    {
        var record = new ServiceContentRecord
        {
            Rank = ServiceRank.Veteran,
            Assignment = ServiceAssignment.Archer,
            ServiceXp = 512,
            DaysServed = 40,
            DutySuccesses = 4,
            DutyFailures = 1,
            Trust = 12,
            FieldRep = 9,
            LogisticsRep = 2,
            CommandRep = 5,
            SiegeRep = 1,
            DeferredWages = 22,
            BattleVictories = 11,
            BattleDefeats = 3,
            TournamentWins = 1,
            LastAssignmentSwapDay = 210.5,
            ActiveDutyId = "bandit_hunt",
            ActiveDutyTargetPartyId = "taom_enlist_duty_ab12",
            ActiveDutyDeadlineDay = 216.0,
            LastOfferDay = 211.0,
            RecentDutyIds = new List<string> { "road_patrol", "forage" },
        };

        Assert.IsTrue(ServiceContentRecord.TryParse(record.Serialize(), out var parsed));
        Assert.AreEqual(ServiceRank.Veteran, parsed.Rank);
        Assert.AreEqual(ServiceAssignment.Archer, parsed.Assignment);
        Assert.AreEqual(512, parsed.ServiceXp);
        Assert.AreEqual(22, parsed.DeferredWages);
        Assert.AreEqual("bandit_hunt", parsed.ActiveDutyId);
        Assert.AreEqual(216.0, parsed.ActiveDutyDeadlineDay);
        CollectionAssert.AreEqual(new List<string> { "road_patrol", "forage" }, parsed.RecentDutyIds);
    }

    [TestMethod]
    public void ContentRecord_NaNDeadline_DroppedFieldLevel()
    {
        Assert.IsTrue(ServiceContentRecord.TryParse("rank=1;dutyId=bandit_hunt;dutyDeadline=NaN", out var parsed));
        Assert.IsNull(parsed.ActiveDutyDeadlineDay);
    }

    [TestMethod]
    public void ContentRecord_UnknownKeys_Ignored()
    {
        Assert.IsTrue(ServiceContentRecord.TryParse("rank=0;futureField=zzz", out _));
    }

    [TestMethod]
    public void ContentRecord_MissingRank_ReturnsFalse()
    {
        Assert.IsFalse(ServiceContentRecord.TryParse("xp=100", out _));
    }
}
