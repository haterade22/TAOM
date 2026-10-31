using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Tests.Features.FordsOfWar;

[TestClass]
public class FordsTowerGeometryTests
{
    private static readonly TowerSlot EastTower =
        new("tower_east_0_0", MatchSide.East, 0, 0, TowerKind.Tower, new Vec3f(640f, 0f, 3f), (float)Math.PI);

    [TestMethod]
    public void GunnerPosition_TwoGunners_StandBehindTheTowerOnTheOwnersSide()
    {
        var first = FordsTowerGeometry.GunnerPosition(EastTower, 0, 2);
        var second = FordsTowerGeometry.GunnerPosition(EastTower, 1, 2);

        // The East tower faces West (yaw pi), so behind it is further East.
        Assert.IsTrue(first.X > EastTower.Position.X + 1.5f);
        Assert.IsTrue(second.X > EastTower.Position.X + 1.5f);
        Assert.AreEqual(1.2f, first.Distance2D(second), 0.01f);
    }

    [TestMethod]
    public void GunnerPosition_OneGunner_StandsOnTheTowerAxis()
    {
        var only = FordsTowerGeometry.GunnerPosition(EastTower, 0, 1);

        Assert.AreEqual(0f, only.Y, 0.01f);
    }

    [TestMethod]
    public void VolleyOrigin_IsAboveTheTowerBase()
    {
        var origin = FordsTowerGeometry.VolleyOrigin(EastTower);

        Assert.AreEqual(EastTower.Position.X, origin.X);
        Assert.AreEqual(EastTower.Position.Z + FordsTowerGeometry.VolleyOriginHeight, origin.Z, 0.001f);
    }

    [TestMethod]
    public void AimDirection_FlatTarget_IsAUnitVectorTiltedUpward()
    {
        var aim = Aim(new Vec3f(0f, 0f, 0f), new Vec3f(40f, 0f, 0f), 70f);

        Assert.AreEqual(1f, aim.Distance(default), 0.001f);
        Assert.IsTrue(aim.Z > 0f);
        Assert.IsTrue(aim.X > 0.99f);
    }

    [TestMethod]
    public void AimDirection_FlatTarget_LandsTheArcOnTheTarget()
    {
        const float Speed = 70f;
        var aim = Aim(new Vec3f(0f, 0f, 0f), new Vec3f(40f, 0f, 0f), Speed);

        Assert.AreEqual(0f, HeightAfter(aim, Speed, 40f), 0.05f);
    }

    [TestMethod]
    public void AimDirection_TargetBelowTheOrigin_LandsTheArcOnTheTarget()
    {
        const float Speed = 70f;
        var origin = new Vec3f(0f, 0f, 5f);
        var aim = Aim(origin, new Vec3f(45f, 0f, 0f), Speed);

        Assert.AreEqual(-5f, HeightAfter(aim, Speed, 45f), 0.05f);
    }

    [TestMethod]
    public void AimDirection_TargetOutOfReach_UsesTheFortyFiveDegreeArc()
    {
        var aim = Aim(new Vec3f(0f, 0f, 0f), new Vec3f(5000f, 0f, 0f), 30f);

        Assert.AreEqual(aim.X, aim.Z, 0.001f);
    }

    [TestMethod]
    public void AimDirection_TargetStraightAbove_IsVertical()
    {
        var aim = Aim(new Vec3f(0f, 0f, 0f), new Vec3f(0f, 0f, 10f), 70f);

        Assert.AreEqual(new Vec3f(0f, 0f, 1f), aim);
    }

    [TestMethod]
    public void AimDirection_NonFiniteTarget_GivesNoAim()
    {
        var origin = new Vec3f(0f, 0f, 0f);

        Assert.IsNull(FordsTowerGeometry.AimDirection(origin, new Vec3f(float.NaN, 0f, 0f), 70f));
        Assert.IsNull(FordsTowerGeometry.AimDirection(origin, new Vec3f(40f, float.NaN, 0f), 70f));
        Assert.IsNull(FordsTowerGeometry.AimDirection(origin, new Vec3f(40f, 0f, float.PositiveInfinity), 70f));
    }

    [TestMethod]
    public void AimDirection_NonFiniteOriginOrSpeed_GivesNoAim()
    {
        var target = new Vec3f(40f, 0f, 0f);

        Assert.IsNull(FordsTowerGeometry.AimDirection(new Vec3f(0f, float.NaN, 0f), target, 70f));
        Assert.IsNull(FordsTowerGeometry.AimDirection(new Vec3f(0f, 0f, 0f), target, float.NaN));
        Assert.IsNull(FordsTowerGeometry.AimDirection(new Vec3f(0f, 0f, 0f), target, float.PositiveInfinity));
    }

    [TestMethod]
    public void Reach_IsTheFortyFiveDegreeRange_SpeedSquaredOverGravity()
    {
        Assert.AreEqual(70f * 70f / FordsTowerGeometry.Gravity, FordsTowerGeometry.Reach(70f), 0.01f);
    }

    [TestMethod]
    public void Reach_OfANonPositiveSpeed_IsZero()
    {
        Assert.AreEqual(0f, FordsTowerGeometry.Reach(0f));
        Assert.AreEqual(0f, FordsTowerGeometry.Reach(-20f));
        Assert.AreEqual(0f, FordsTowerGeometry.Reach(float.NaN));
    }

    [TestMethod]
    public void Reach_IsTheDistanceTheFortyFiveDegreeArcLandsAt()
    {
        const float Speed = 30f;
        var reach = FordsTowerGeometry.Reach(Speed);
        var aim = Aim(new Vec3f(0f, 0f, 0f), new Vec3f(reach * 2f, 0f, 0f), Speed);

        Assert.AreEqual(0f, HeightAfter(aim, Speed, reach), 0.05f);
    }

    private static Vec3f Aim(Vec3f origin, Vec3f target, float speed) => FordsTowerGeometry.AimDirection(origin, target, speed)!.Value;

    // Height of the missile, relative to its origin, when it has travelled <flat> metres along the aim's ground heading.
    private static float HeightAfter(Vec3f aim, float speed, float flat)
    {
        var horizontal = (float)Math.Sqrt(aim.X * aim.X + aim.Y * aim.Y);
        var t = flat / (speed * horizontal);
        return speed * aim.Z * t - 0.5f * FordsTowerGeometry.Gravity * t * t;
    }
}
