using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CultureDoctrine.Doctrines;

namespace TAOM.Tests.Features.CultureDoctrine;

[TestClass]
public class InfantrySkirmishMachineTests
{
    // Sixty javelin men: a 10 s can't-throw window and a throwing bar of 0.1 * ratio.
    private const int Men = 60;
    private const float Throwers = 0.8f;

    private static SkirmishReading Reading(float distance, float throwing, float now, bool enemyInfantry = true, float maxRange = 40f, float adjusted = 30f, bool hasEnemy = true)
        => new SkirmishReading(hasEnemy, distance, maxRange, adjusted, throwing, Throwers, Men, enemyInfantry, now);

    private static InfantrySkirmishMachine Fresh(float now = 0f)
    {
        var m = new InfantrySkirmishMachine();
        m.Reset(now);
        return m;
    }

    [TestMethod]
    public void Bars_MatchVanillaSkirmish()
    {
        Assert.AreEqual(5f, InfantrySkirmishMachine.CantShootWindow(5), 1e-4f);
        Assert.AreEqual(10f, InfantrySkirmishMachine.CantShootWindow(60), 1e-4f);
        Assert.AreEqual(10f, InfantrySkirmishMachine.CantShootWindow(500), 1e-4f);
        Assert.AreEqual(0.1f * Throwers, InfantrySkirmishMachine.ThrowingBar(50, Throwers), 1e-4f);
        Assert.AreEqual((0.1f + 0.23f * 0.98f) * Throwers, InfantrySkirmishMachine.ThrowingBar(1, Throwers), 1e-4f, "vanilla's lerp(0.1, 0.33, 1 - n * 0.02) at n = 1");
    }

    [TestMethod]
    public void OutOfRangeAndSilent_Approaches_ThenThrowsWhenCloseEnough()
    {
        var m = Fresh();
        Assert.IsTrue(m.Step(Reading(distance: 80f, throwing: 0f, now: 1f)));
        Assert.AreEqual(SkirmishStage.Approaching, m.Stage);
        Assert.AreEqual(36f, m.CantShootDistance, 1e-4f, "90% of the longest throw");
        Assert.IsFalse(m.Step(Reading(distance: 40f, throwing: 0f, now: 2f)));
        Assert.IsTrue(m.Step(Reading(distance: 28f, throwing: 0f, now: 3f)), "inside 80% of the can't-throw distance");
        Assert.AreEqual(SkirmishStage.Throwing, m.Stage);
    }

    [TestMethod]
    public void Throwing_EnemyFootClosesInsideFortyPercent_PullsBack_ThenReturnsToRange()
    {
        var m = Fresh();
        Assert.IsFalse(m.Step(Reading(distance: 25f, throwing: 0.5f, now: 1f)));
        Assert.IsTrue(m.Step(Reading(distance: 10f, throwing: 0.5f, now: 2f)), "foot within 12 m (40% of 30)");
        Assert.AreEqual(SkirmishStage.PullingBack, m.Stage);
        Assert.IsFalse(m.Step(Reading(distance: 15f, throwing: 0.5f, now: 3f)));
        Assert.IsTrue(m.Step(Reading(distance: 25f, throwing: 0.5f, now: 4f)), "back beyond 80% of the range");
        Assert.AreEqual(SkirmishStage.Throwing, m.Stage);
    }

    [TestMethod]
    public void Approaching_EnoughOfTheLineThrowing_ReturnsToThrowing_BeforeTheDistanceDoes()
    {
        var m = Fresh();
        m.Step(Reading(distance: 80f, throwing: 0f, now: 1f));
        Assert.AreEqual(SkirmishStage.Approaching, m.Stage);
        var bar = InfantrySkirmishMachine.ThrowingBar(Men, Throwers);
        Assert.IsTrue(m.Step(Reading(distance: 60f, throwing: bar * 1.25f, now: 2f)), "still beyond 80% of the can't-throw distance, but the line is throwing");
        Assert.AreEqual(SkirmishStage.Throwing, m.Stage);
    }

    [TestMethod]
    public void PullingBack_TimerRunsOutWithNobodyThrowing_ReturnsToThrowing_AndRetriesLater()
    {
        var m = Fresh();
        m.Step(Reading(distance: 25f, throwing: 0.5f, now: 1f));
        m.Step(Reading(distance: 10f, throwing: 0.5f, now: 2f));
        Assert.AreEqual(SkirmishStage.PullingBack, m.Stage);
        Assert.IsFalse(m.Step(Reading(distance: 15f, throwing: 0f, now: 11f)), "the 10 s pull-back has not run out");
        Assert.IsTrue(m.Step(Reading(distance: 15f, throwing: 0f, now: 12.5f)), "ran out with nobody throwing: stand and throw");
        Assert.AreEqual(SkirmishStage.Throwing, m.Stage);
        Assert.IsFalse(m.Step(Reading(distance: 10f, throwing: 0.5f, now: 13f)), "the 5 s retry window holds the line in place");
        Assert.IsTrue(m.Step(Reading(distance: 10f, throwing: 0.5f, now: 18f)));
        Assert.AreEqual(SkirmishStage.PullingBack, m.Stage);
    }

    [TestMethod]
    public void CantShootWindow_LerpsBetweenTheClamps()
        => Assert.AreEqual(7.5f, InfantrySkirmishMachine.CantShootWindow(35), 1e-4f);

    [TestMethod]
    public void Throwing_CavalryClosing_DoesNotPullBack()
    {
        var m = Fresh();
        Assert.IsFalse(m.Step(Reading(distance: 10f, throwing: 0.5f, now: 2f, enemyInfantry: false)));
        Assert.AreEqual(SkirmishStage.Throwing, m.Stage);
    }

    [TestMethod]
    public void InRangeSilentForTheWindow_WellInside_Commits_AndStaysCommitted()
    {
        var m = Fresh();
        Assert.IsFalse(m.Step(Reading(distance: 15f, throwing: 0f, now: 1f)), "the window opens");
        Assert.IsFalse(m.Step(Reading(distance: 15f, throwing: 0f, now: 10f)));
        Assert.IsTrue(m.Step(Reading(distance: 15f, throwing: 0f, now: 11.5f)));
        Assert.AreEqual(SkirmishStage.Committed, m.Stage);
        Assert.IsFalse(m.Step(Reading(distance: 60f, throwing: 0.9f, now: 20f)), "committed is terminal until reset");
        Assert.AreEqual(SkirmishStage.Committed, m.Stage);
    }

    [TestMethod]
    public void InRangeSilentForTheWindow_FarOut_ApproachesInstead()
    {
        var m = Fresh();
        m.Step(Reading(distance: 35f, throwing: 0f, now: 1f));
        Assert.IsTrue(m.Step(Reading(distance: 35f, throwing: 0f, now: 12f)));
        Assert.AreEqual(SkirmishStage.Approaching, m.Stage, "35 m is outside 60% of the 30 m average throw: the long arm set the range");
        Assert.AreEqual(35f, m.CantShootDistance, 1e-4f);
    }

    [TestMethod]
    public void ThrowingResumes_ClosesTheWindow()
    {
        var m = Fresh();
        m.Step(Reading(distance: 15f, throwing: 0f, now: 1f));
        m.Step(Reading(distance: 15f, throwing: 0.5f, now: 5f));
        Assert.IsFalse(m.Step(Reading(distance: 15f, throwing: 0f, now: 12f)), "a fresh window opens instead of committing");
        Assert.AreEqual(SkirmishStage.Throwing, m.Stage);
    }

    [TestMethod]
    public void NoEnemyOrNaN_HoldsTheStage()
    {
        var m = Fresh();
        Assert.IsFalse(m.Step(Reading(distance: 80f, throwing: 0f, now: 1f, hasEnemy: false)));
        Assert.IsFalse(m.Step(Reading(distance: float.NaN, throwing: 0f, now: 2f)));
        Assert.AreEqual(SkirmishStage.Throwing, m.Stage);
    }

    [TestMethod]
    public void Reset_ClearsCommitted()
    {
        var m = Fresh();
        m.Step(Reading(distance: 15f, throwing: 0f, now: 1f));
        m.Step(Reading(distance: 15f, throwing: 0f, now: 12f));
        Assert.AreEqual(SkirmishStage.Committed, m.Stage);
        m.Reset(20f);
        Assert.AreEqual(SkirmishStage.Throwing, m.Stage);
    }
}
