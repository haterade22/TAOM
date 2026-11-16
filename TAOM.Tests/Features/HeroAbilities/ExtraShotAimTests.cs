using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar.Domain;
using TAOM.Features.HeroAbilities.Ranged;

namespace TAOM.Tests.Features.HeroAbilities;

[TestClass]
public class ExtraShotAimTests
{
    private static readonly Vec3f Origin = new Vec3f(0f, 0f, 1.5f);

    [TestMethod]
    public void Direction_ZeroAngle_HorizontalUnit()
    {
        var d = ExtraShotAim.Direction(Origin, new Vec3f(30f, 40f, 9f), 0f)!.Value;

        Assert.AreEqual(0.6f, d.X, 1e-5f);
        Assert.AreEqual(0.8f, d.Y, 1e-5f);
        Assert.AreEqual(0f, d.Z, 1e-6f);
    }

    [TestMethod]
    public void Direction_Angle_RisesBySin()
    {
        var angle = 0.2f;

        var d = ExtraShotAim.Direction(Origin, new Vec3f(50f, 0f, 0f), angle)!.Value;

        Assert.AreEqual((float)Math.Cos(angle), d.X, 1e-5f);
        Assert.AreEqual(0f, d.Y, 1e-6f);
        Assert.AreEqual((float)Math.Sin(angle), d.Z, 1e-5f);
        Assert.AreEqual(1f, d.X * d.X + d.Y * d.Y + d.Z * d.Z, 1e-5f);
    }

    [TestMethod]
    public void Direction_AimStraightAbove_Null()
    {
        Assert.IsNull(ExtraShotAim.Direction(Origin, new Vec3f(0f, 0f, 20f), 0.1f));
        Assert.IsNull(ExtraShotAim.Direction(Origin, new Vec3f(float.NaN, 0f, 0f), 0.1f));
        Assert.IsNull(ExtraShotAim.Direction(Origin, new Vec3f(50f, 0f, 0f), float.NaN));
    }

    // The native solver answers pi for a target out of reach (IMBMission_GetMissileVerticalAimCorrection, 0x6F5A90).
    [DataTestMethod]
    [DataRow(float.NaN, false)]
    [DataRow(float.PositiveInfinity, false)]
    [DataRow(1.6f, false)]
    [DataRow(3.1415927f, false)]
    [DataRow(1.5707f, true)]
    [DataRow(0f, true)]
    [DataRow(-0.3f, true)]
    [DataRow(-1.6f, false)]
    public void IsUsableAngle_NaNOrAbovePiHalf_False(float angle, bool expected) =>
        Assert.AreEqual(expected, ExtraShotAim.IsUsableAngle(angle));

    [TestMethod]
    public void LeadPoint_Standing_IsChest()
    {
        var chest = new Vec3f(50f, 3f, 1.2f);

        Assert.AreEqual(chest, ExtraShotAim.LeadPoint(Origin, chest, new Vec3f(0f, 0f, 0f), 50f, 0f));
    }

    [TestMethod]
    public void LeadPoint_Moving_LeadsByFlightTime()
    {
        var chest = new Vec3f(50f, 0f, 1.2f);

        var aim = ExtraShotAim.LeadPoint(Origin, chest, new Vec3f(2f, 0f, 0f), 50f, 0f)!.Value;

        // Pass 1 flies 50 m at 50 m/s (1 s), so the target is at 52; pass 2 flies 52 m (1.04 s), so 52.08.
        Assert.AreEqual(52.08f, aim.X, 1e-3f);
        Assert.AreEqual(0f, aim.Y, 1e-6f);
        Assert.AreEqual(1.2f, aim.Z, 1e-6f);
    }

    [TestMethod]
    public void LeadPoint_VerticalVelocityIgnored()
    {
        var chest = new Vec3f(50f, 0f, 1.2f);

        var aim = ExtraShotAim.LeadPoint(Origin, chest, new Vec3f(0f, 0f, 9f), 50f, 0f)!.Value;

        Assert.AreEqual(chest, aim);
    }

    [TestMethod]
    public void LeadPoint_NonFinite_Null()
    {
        var chest = new Vec3f(50f, 0f, 1.2f);
        var still = new Vec3f(0f, 0f, 0f);

        Assert.IsNull(ExtraShotAim.LeadPoint(Origin, new Vec3f(float.NaN, 0f, 0f), still, 50f, 0f));
        Assert.IsNull(ExtraShotAim.LeadPoint(Origin, chest, new Vec3f(float.PositiveInfinity, 0f, 0f), 50f, 0f));
        Assert.IsNull(ExtraShotAim.LeadPoint(Origin, chest, still, float.NaN, 0f));
        Assert.IsNull(ExtraShotAim.LeadPoint(Origin, chest, still, 0f, 0f));
        Assert.IsNull(ExtraShotAim.LeadPoint(Origin, chest, still, 50f, float.NaN));
        Assert.IsNull(ExtraShotAim.LeadPoint(Origin, chest, still, 50f, 1.5707963f), "cos is zero: the arrow never advances");
    }
}
