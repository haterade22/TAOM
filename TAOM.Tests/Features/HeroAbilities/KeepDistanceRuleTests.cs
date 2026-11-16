using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.HeroAbilities.Ranged;

namespace TAOM.Tests.Features.HeroAbilities;

// The ranged hero steps away from a melee threat (#1415).
[TestClass]
public class KeepDistanceRuleTests
{
    [TestMethod]
    public void ShouldStep_ThreatInsideTheRadius_Steps() =>
        Assert.IsTrue(KeepDistanceRule.ShouldStep(nearestThreat: 3f, keepDistance: 5f, stepBack: 8f, now: 10f, nextAllowed: 0f));

    [TestMethod]
    public void ShouldStep_ThreatOnTheRadius_Steps() =>
        Assert.IsTrue(KeepDistanceRule.ShouldStep(5f, 5f, 8f, 10f, 0f));

    [TestMethod]
    public void ShouldStep_ThreatOutsideTheRadius_DoesNotStep() =>
        Assert.IsFalse(KeepDistanceRule.ShouldStep(5.1f, 5f, 8f, 10f, 0f));

    [TestMethod]
    public void ShouldStep_BeforeTheCooldownEnds_DoesNotStep() =>
        Assert.IsFalse(KeepDistanceRule.ShouldStep(3f, 5f, 8f, 10f, 10.5f));

    [TestMethod]
    public void ShouldStep_AtTheCooldownEnd_Steps() =>
        Assert.IsTrue(KeepDistanceRule.ShouldStep(3f, 5f, 8f, 10f, 10f));

    [TestMethod]
    public void ShouldStep_KeepDistanceOff_DoesNotStep() =>
        Assert.IsFalse(KeepDistanceRule.ShouldStep(0.5f, 0f, 8f, 10f, 0f));

    [TestMethod]
    public void ShouldStep_StepBackOff_DoesNotStep() =>
        Assert.IsFalse(KeepDistanceRule.ShouldStep(0.5f, 5f, 0f, 10f, 0f));

    [TestMethod]
    public void ShouldStep_NaNThreatDistance_DoesNotStep() =>
        Assert.IsFalse(KeepDistanceRule.ShouldStep(float.NaN, 5f, 8f, 10f, 0f));

    [TestMethod]
    public void ShouldStep_NaNRadius_DoesNotStep() =>
        Assert.IsFalse(KeepDistanceRule.ShouldStep(3f, float.NaN, 8f, 10f, 0f));

    [TestMethod]
    public void ShouldStep_NaNStepBack_DoesNotStep() =>
        Assert.IsFalse(KeepDistanceRule.ShouldStep(3f, 5f, float.NaN, 10f, 0f));

    [TestMethod]
    public void ShouldStep_NaNClock_DoesNotStep() =>
        Assert.IsFalse(KeepDistanceRule.ShouldStep(3f, 5f, 8f, float.NaN, 0f));

    [TestMethod]
    public void AwayFrom_OneThreatEast_PointsWest()
    {
        var away = KeepDistanceRule.AwayFrom(0f, 0f, new[] { (3f, 0f) });

        Assert.IsNotNull(away);
        Assert.AreEqual(-1f, away!.Value.X, 1e-5f);
        Assert.AreEqual(0f, away.Value.Y, 1e-5f);
    }

    [TestMethod]
    public void AwayFrom_TwoThreats_PointsAwayFromBoth()
    {
        var away = KeepDistanceRule.AwayFrom(0f, 0f, new[] { (3f, 0f), (0f, 3f) });

        Assert.IsNotNull(away);
        var half = (float)Math.Sqrt(0.5);
        Assert.AreEqual(-half, away!.Value.X, 1e-5f);
        Assert.AreEqual(-half, away.Value.Y, 1e-5f);
    }

    [TestMethod]
    public void AwayFrom_NearerThreat_WeighsMore()
    {
        var away = KeepDistanceRule.AwayFrom(0f, 0f, new[] { (1f, 0f), (0f, 4f) });

        Assert.IsNotNull(away);
        Assert.IsTrue(-away!.Value.X > -away.Value.Y, "the threat at 1 m pushes harder than the one at 4 m");
    }

    [TestMethod]
    public void AwayFrom_ThreatsThatCancel_IsNull() =>
        Assert.IsNull(KeepDistanceRule.AwayFrom(0f, 0f, new[] { (2f, 0f), (-2f, 0f) }));

    [TestMethod]
    public void AwayFrom_ThreatOnTopOfHim_IsNull() =>
        Assert.IsNull(KeepDistanceRule.AwayFrom(1f, 1f, new[] { (1f, 1f) }));

    [TestMethod]
    public void AwayFrom_NoThreat_IsNull() =>
        Assert.IsNull(KeepDistanceRule.AwayFrom(0f, 0f, Array.Empty<(float, float)>()));

    [TestMethod]
    public void AwayFrom_NaNThreat_IsSkipped()
    {
        var away = KeepDistanceRule.AwayFrom(0f, 0f, new[] { (float.NaN, 0f), (3f, 0f) });

        Assert.IsNotNull(away);
        Assert.AreEqual(-1f, away!.Value.X, 1e-5f);
    }

    [TestMethod]
    public void Candidates_StraightAwayFirst_ThenFortyFiveThenNinety()
    {
        var points = KeepDistanceRule.Candidates(10f, 20f, -1f, 0f, 8f).ToArray();

        Assert.AreEqual(5, points.Length);
        Assert.AreEqual(2f, points[0].X, 1e-4f);
        Assert.AreEqual(20f, points[0].Y, 1e-4f);
        foreach (var p in points)
        {
            var d = Math.Sqrt((p.X - 10f) * (p.X - 10f) + (p.Y - 20f) * (p.Y - 20f));
            Assert.AreEqual(8.0, d, 1e-3, "every point is the step away");
        }

        var angles = points.Select(p => Math.Round(Math.Atan2(p.Y - 20f, p.X - 10f) * 180.0 / Math.PI)).ToArray();
        CollectionAssert.AreEqual(new[] { 180.0, -135.0, 135.0, -90.0, 90.0 }, angles);
    }

    // The engine's rotation of a direction is Atan2(-x, y) (Vec2.RotationInRadians), and he faces the threats, against the step.
    [TestMethod]
    public void Facing_StepWest_LooksEast() =>
        Assert.AreEqual((float)(-Math.PI / 2), KeepDistanceRule.FacingRadians(-1f, 0f), 1e-5f);

    [TestMethod]
    public void Facing_StepSouth_LooksNorth() =>
        Assert.AreEqual(0f, KeepDistanceRule.FacingRadians(0f, -1f), 1e-5f);

    // The step from (0,0) to (8,0): he walked it when he covered half its length along it by the end of the hold.
    [DataTestMethod]
    [DataRow(4f, 0f, true)]
    [DataRow(8f, 3f, true)]
    [DataRow(3.9f, 0f, false)]
    [DataRow(0f, 6f, false)]
    [DataRow(-6f, 0f, false)]
    [DataRow(float.NaN, 0f, false)]
    public void Walked_HalfTheStepAlongIt(float atX, float atY, bool walked) =>
        Assert.AreEqual(walked, KeepDistanceRule.Walked(0f, 0f, 8f, 0f, atX, atY));

    [TestMethod]
    public void Walked_AStepOfNoLength_IsNever() =>
        Assert.IsFalse(KeepDistanceRule.Walked(1f, 1f, 1f, 1f, 5f, 5f));
}
