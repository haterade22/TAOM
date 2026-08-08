using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Enlistment.Content;
using TAOM.Features.Enlistment.Content.Domain;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>
/// Walking out of an enlisted battle used to bank the FULL survival weight: survival was computed
/// as "never went down", and a player who left at t=5s never went down. These pin the corrected
/// reading — leaving is not surviving.
/// </summary>
[TestClass]
public class BattleMeritLeftFieldTests
{
    private static MeritScoringConfig Scoring() => new MeritScoringConfig();

    private static List<MeritBand> DefaultBands() => new List<MeritBand>
    {
        new MeritBand { MinScore = 80, GradeKey = "distinguished" },
        new MeritBand { MinScore = 60, GradeKey = "strong" },
        new MeritBand { MinScore = 40, GradeKey = "solid" },
        new MeritBand { MinScore = 0, GradeKey = "rough" },
    };

    [TestMethod]
    public void Score_LeftImmediatelyWithoutFighting_ScoresZero()
    {
        var score = BattleMeritScorer.Score(
            new MeritSample { SurvivalRatio = 1f, LeftTheField = true }, Scoring());

        Assert.AreEqual(0, score, "A t=5s walkout must not bank the 25-point survival weight.");
    }

    [TestMethod]
    public void Score_StayedToTheEnd_BanksTheFullSurvivalWeight()
    {
        var score = BattleMeritScorer.Score(
            new MeritSample { SurvivalRatio = 1f, LeftTheField = false }, Scoring());

        Assert.AreEqual(25, score, "Fighting through without going down still earns survival.");
    }

    [TestMethod]
    public void Score_LeftAfterFullEngagement_SinksIntoTheBottomBand()
    {
        var sample = new MeritSample
        {
            SurvivalRatio = 1f,
            CohesionRatio = 1f,
            CommanderProximityRatio = 1f,
            EngagementRatio = 1f,
            RoleFit = true,
            LeftTheField = true,
        };

        var score = BattleMeritScorer.Score(sample, Scoring());

        Assert.AreEqual(15, score, "15 + 10 + 10 + 10 role fit, survival zeroed, minus the 30 penalty.");
        Assert.AreEqual("rough", BattleMeritScorer.ResolveBand(score, DefaultBands()).GradeKey,
            "The best possible walkout must still land in the bottom band.");
    }

    [TestMethod]
    public void Score_LeavingVersusStaying_DiffersBySurvivalWeightPlusPenalty()
    {
        var config = Scoring();
        var stayed = new MeritSample { Kills = 6, SurvivalRatio = 1f, LeftTheField = false };
        var left = new MeritSample { Kills = 6, SurvivalRatio = 1f, LeftTheField = true };

        Assert.AreEqual(55, BattleMeritScorer.Score(stayed, config), "6 kills * 5 + 25 survival");
        Assert.AreEqual(0, BattleMeritScorer.Score(left, config), "30 kill points minus the 30 penalty");
    }

    [TestMethod]
    public void Score_LeftTheFieldAndFellEarly_TakesBothPenalties()
    {
        var config = Scoring();

        Assert.AreEqual(65, BattleMeritScorer.Score(Fought(false, false), config));
        Assert.AreEqual(55, BattleMeritScorer.Score(Fought(true, false), config));
        Assert.AreEqual(35, BattleMeritScorer.Score(Fought(false, true), config));
        Assert.AreEqual(25, BattleMeritScorer.Score(Fought(true, true), config),
            "The two penalties describe different failures and both apply.");
    }

    [TestMethod]
    public void Score_LeftTheFieldWithNaNSurvival_StillContributesZeroSurvival()
    {
        var sample = new MeritSample { Kills = 2, SurvivalRatio = float.NaN, LeftTheField = true };

        Assert.AreEqual(0, BattleMeritScorer.Score(sample, Scoring()),
            "10 kill points minus the 30 penalty");
    }

    private static MeritSample Fought(bool fellEarly, bool leftTheField) => new MeritSample
    {
        Kills = 6,
        CohesionRatio = 1f,
        CommanderProximityRatio = 1f,
        EngagementRatio = 1f,
        FellEarly = fellEarly,
        LeftTheField = leftTheField,
    };
}
