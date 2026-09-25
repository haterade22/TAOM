using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CultureDoctrine.Doctrines;
using TaleWorlds.Library;

namespace TAOM.Tests.Features.CultureDoctrine;

[TestClass]
[TestCategory("RequiresGame")]
public class EnvelopGeometryTests
{
    private static readonly Vec2 Centre = new Vec2(0f, 0f);
    private static readonly Vec2 Enemy = new Vec2(0f, 100f);

    [TestMethod]
    public void RightWing_GoesToTheRightOfTheAxis_LevelWithTheEnemy()
    {
        var p = EnvelopGeometry.WingPoint(Centre, Enemy, enemyWidth: 40f, wingWidth: 20f, WingSide.Right, margin: 6f);
        Assert.AreEqual(36f, p.x, 1e-3f, "half of each width plus the margin, on the right (+x when advancing along +y)");
        Assert.AreEqual(100f, p.y, 1e-3f);
    }

    [TestMethod]
    public void LeftWing_MirrorsTheRight()
    {
        var p = EnvelopGeometry.WingPoint(Centre, Enemy, 40f, 20f, WingSide.Left, 6f);
        Assert.AreEqual(-36f, p.x, 1e-3f);
        Assert.AreEqual(100f, p.y, 1e-3f);
    }

    [TestMethod]
    public void RotatedAxis_KeepsTheWingOnItsSide()
    {
        var enemy = new Vec2(100f, 0f);
        var right = EnvelopGeometry.WingPoint(Centre, enemy, 40f, 20f, WingSide.Right, 6f);
        Assert.AreEqual(100f, right.x, 1e-3f);
        Assert.AreEqual(-36f, right.y, 1e-3f, "advancing along +x, the right hand is -y");
    }

    [TestMethod]
    public void NoAxis_OrBadInputs_FallBackToTheEnemy()
    {
        Assert.AreEqual(Enemy, EnvelopGeometry.WingPoint(Enemy, Enemy, 40f, 20f, WingSide.Right, 6f));
        Assert.AreEqual(Enemy, EnvelopGeometry.WingPoint(new Vec2(float.NaN, 0f), Enemy, 40f, 20f, WingSide.Right, 6f));
        var p = EnvelopGeometry.WingPoint(Centre, Enemy, float.NaN, -5f, WingSide.Right, float.NaN);
        Assert.AreEqual(EnvelopGeometry.DefaultMargin, p.x, 1e-3f, "NaN and negative widths count as zero, a NaN margin as the default");
    }

    [TestMethod]
    public void ShouldCharge_NearThePoint_OrWithTheEnemyAtArmsReach()
    {
        Assert.IsFalse(EnvelopGeometry.ShouldCharge(distanceToWingPoint: 60f, distanceToEnemy: 50f));
        Assert.IsTrue(EnvelopGeometry.ShouldCharge(25f, 50f));
        Assert.IsTrue(EnvelopGeometry.ShouldCharge(60f, 15f));
        Assert.IsFalse(EnvelopGeometry.ShouldCharge(float.NaN, float.NaN));
    }
}

[TestClass]
[TestCategory("RequiresGame")]
public class CavalryThreatTests
{
    private static readonly Vec2 Wall = new Vec2(0f, 0f);

    [TestMethod]
    public void Riders_ClosingFromTheFlank_AreInbound_WhateverTheWallFaces()
    {
        // 100 m out on the flank, riding straight at the wall at 10 m/s: 10 s away.
        Assert.IsTrue(CavalryThreat.IsInbound(Wall, new Vec2(100f, 0f), new Vec2(-10f, 0f)));
    }

    [TestMethod]
    public void Riders_TooFar_OrTooSlow_OrRidingPast_AreNot()
    {
        Assert.IsFalse(CavalryThreat.IsInbound(Wall, new Vec2(200f, 0f), new Vec2(-10f, 0f)), "20 s out");
        Assert.IsFalse(CavalryThreat.IsInbound(Wall, new Vec2(30f, 0f), new Vec2(-1f, 0f)), "walking");
        Assert.IsFalse(CavalryThreat.IsInbound(Wall, new Vec2(100f, 0f), new Vec2(0f, 10f)), "riding across");
        Assert.IsFalse(CavalryThreat.IsInbound(Wall, new Vec2(100f, 0f), new Vec2(10f, 0f)), "riding away");
    }

    [TestMethod]
    public void Riders_OnTopOfUs_AreInbound_AndNaNIsNot()
    {
        Assert.IsTrue(CavalryThreat.IsInbound(Wall, Wall, new Vec2(5f, 0f)));
        Assert.IsFalse(CavalryThreat.IsInbound(Wall, new Vec2(float.NaN, 0f), new Vec2(-10f, 0f)));
        Assert.IsFalse(CavalryThreat.IsInbound(Wall, new Vec2(100f, 0f), new Vec2(float.NaN, 0f)));
    }

    [TestMethod]
    public void Bars_MatchTheEngineQuery()
    {
        Assert.AreEqual(0.75f, CavalryThreat.ClosingDot);
        Assert.AreEqual(15f, CavalryThreat.HorizonSeconds);
    }
}

[TestClass]
public class VolleyDecisionTests
{
    private static readonly VolleyTunables T = new VolleyTunables(releaseFraction: 0.8f, holdFraction: 0.95f);

    [TestMethod]
    public void FireAtWill_AlwaysFires()
        => Assert.IsTrue(VolleyDecision.ShouldFire(firingNow: false, hasEnemy: false, distance: 999f, rangeAdjusted: 100f, VolleyTunables.FireAtWill));

    [TestMethod]
    public void NoEnemy_Holds()
        => Assert.IsFalse(VolleyDecision.ShouldFire(true, hasEnemy: false, 10f, 100f, T));

    [TestMethod]
    public void Holding_ReleasesOnlyInsideTheReleaseRange()
    {
        Assert.IsFalse(VolleyDecision.ShouldFire(false, true, 90f, 100f, T));
        Assert.IsTrue(VolleyDecision.ShouldFire(false, true, 80f, 100f, T));
    }

    [TestMethod]
    public void Firing_KeepsFiringUntilTheHoldRange()
    {
        Assert.IsTrue(VolleyDecision.ShouldFire(true, true, 90f, 100f, T), "inside the hold range: hysteresis");
        Assert.IsFalse(VolleyDecision.ShouldFire(true, true, 96f, 100f, T));
    }

    [TestMethod]
    public void NaNOrZeroRange_KeepsTheCurrentOrder()
    {
        Assert.IsTrue(VolleyDecision.ShouldFire(true, true, float.NaN, 100f, T));
        Assert.IsFalse(VolleyDecision.ShouldFire(false, true, 10f, 0f, T));
        Assert.IsFalse(VolleyDecision.ShouldFire(false, true, 10f, float.NaN, T));
    }
}
