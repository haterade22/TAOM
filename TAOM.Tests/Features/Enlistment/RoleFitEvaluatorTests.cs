using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Enlistment.Content;
using TAOM.Features.Enlistment.Content.Domain;

namespace TAOM.Tests.Features.Enlistment;

[TestClass]
public class RoleFitEvaluatorTests
{
    private static MeritSample Sample(
        float enemyDistance = -1f, float cohesion = 0f, float engagement = 0f, float commander = 0f)
    {
        return new MeritSample
        {
            AverageEnemyDistance = enemyDistance,
            CohesionRatio = cohesion,
            EngagementRatio = engagement,
            CommanderProximityRatio = commander,
        };
    }

    [DataTestMethod]
    [DataRow(18f, true)]   // lower band edge
    [DataRow(35f, true)]
    [DataRow(50f, true)]   // upper band edge
    [DataRow(17.9f, false)] // swallowed by the melee
    [DataRow(60f, false)]   // out of the fight entirely
    public void Archer_RewardsAShootingLine(float distance, bool expected)
    {
        Assert.AreEqual(expected, RoleFitEvaluator.Evaluate(ServiceAssignment.Archer, Sample(distance)));
    }

    [DataTestMethod]
    [DataRow(10f, true)]
    [DataRow(20f, true)]
    [DataRow(28f, true)]
    [DataRow(5f, false)]   // parked in the press
    [DataRow(40f, false)]  // never closed
    public void Cavalry_RewardsWorkingTheFlanks(float distance, bool expected)
    {
        Assert.AreEqual(expected, RoleFitEvaluator.Evaluate(ServiceAssignment.Cavalry, Sample(distance)));
    }

    [TestMethod]
    public void Support_RewardsStayingWithTheCommander()
    {
        Assert.IsTrue(RoleFitEvaluator.Evaluate(ServiceAssignment.Support, Sample(commander: 0.5f)));
        Assert.IsFalse(RoleFitEvaluator.Evaluate(ServiceAssignment.Support, Sample(commander: 0.49f)));
    }

    [TestMethod]
    public void Infantry_RequiresBothFormationAndContact()
    {
        Assert.IsTrue(RoleFitEvaluator.Evaluate(ServiceAssignment.Infantry, Sample(cohesion: 0.6f, engagement: 0.6f)));
        Assert.IsFalse(RoleFitEvaluator.Evaluate(ServiceAssignment.Infantry, Sample(cohesion: 0.9f, engagement: 0.2f)),
            "held the line but never fought");
        Assert.IsFalse(RoleFitEvaluator.Evaluate(ServiceAssignment.Infantry, Sample(cohesion: 0.1f, engagement: 0.9f)),
            "fought but broke formation");
    }

    [DataTestMethod]
    [DataRow(ServiceAssignment.Archer)]
    [DataRow(ServiceAssignment.Cavalry)]
    public void DistanceRoles_NeverMeasured_NoRoleFit(ServiceAssignment assignment)
    {
        // A mission where the player never saw an enemy scores no role bonus rather than
        // a free one (-1 is the never-measured sentinel).
        Assert.IsFalse(RoleFitEvaluator.Evaluate(assignment, Sample(enemyDistance: -1f)));
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    public void NonFiniteDistance_FailsClosed(float distance)
    {
        Assert.IsFalse(RoleFitEvaluator.Evaluate(ServiceAssignment.Archer, Sample(distance)));
    }

    [TestMethod]
    public void NonFiniteRatios_FailClosed()
    {
        var sample = Sample(cohesion: float.NaN, engagement: float.NaN, commander: float.NaN);

        Assert.IsFalse(RoleFitEvaluator.Evaluate(ServiceAssignment.Infantry, sample));
        Assert.IsFalse(RoleFitEvaluator.Evaluate(ServiceAssignment.Support, sample));
    }

    [TestMethod]
    public void NullSample_NoRoleFit()
    {
        Assert.IsFalse(RoleFitEvaluator.Evaluate(ServiceAssignment.Infantry, null));
    }

    [TestMethod]
    public void RoleFitBonus_ActuallyReachesTheScore()
    {
        // The knob was inert before the heuristic existed — pin that it now moves the score.
        var config = new MeritScoringConfig();
        var baseline = new MeritSample { SurvivalRatio = 1f, RoleFit = false };
        var withFit = new MeritSample { SurvivalRatio = 1f, RoleFit = true };

        Assert.AreEqual(
            BattleMeritScorer.Score(baseline, config) + config.RoleFitBonus,
            BattleMeritScorer.Score(withFit, config));
    }
}
