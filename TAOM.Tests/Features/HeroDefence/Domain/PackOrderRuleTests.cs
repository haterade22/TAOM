using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar.Domain;
using TAOM.Features.HeroDefence.Domain;

namespace TAOM.Tests.Features.HeroDefence.Domain;

[TestClass]
public class PackOrderRuleTests
{
    private static readonly PackOrderBand Band = new();
    private static readonly Vec3f Hero = new(0f, 0f, 0f);

    private static Vec3f At(float y, float x = 0f, float z = 0f) => new(x, y, z);

    private static PackOrder Next(PackOrder current, Vec3f[] creeps, params Vec3f[] heroes) =>
        PackOrderRule.Next(current, creeps, heroes, Band);

    [TestMethod]
    public void Band_Defaults_AreTheRunFiveValues()
    {
        Assert.AreEqual(12f, Band.EngageMetres);
        Assert.AreEqual(22f, Band.ReleaseMetres);
        Assert.AreEqual(6f, Band.HeightMetres);
    }

    [TestMethod]
    public void Move_ACreepAtTheEngageDistance_Charges()
    {
        Assert.AreEqual(PackOrder.Charge, Next(PackOrder.Move, new[] { At(12f) }, Hero));
        Assert.AreEqual(PackOrder.Move, Next(PackOrder.Move, new[] { At(12.01f) }, Hero));
    }

    [TestMethod]
    public void Move_KeysOnTheNearestCreep_NotTheLead()
    {
        var wideLine = new[] { At(30f), At(5f, x: 10f) };

        Assert.AreEqual(PackOrder.Charge, Next(PackOrder.Move, wideLine, Hero), "BLOCK-O-M1: a lead key missed a wide line");
    }

    [TestMethod]
    public void Charge_UntilTheReleaseDistance_StaysCharge()
    {
        Assert.AreEqual(PackOrder.Charge, Next(PackOrder.Charge, new[] { At(21.9f) }, Hero));
        Assert.AreEqual(PackOrder.Move, Next(PackOrder.Charge, new[] { At(22f) }, Hero));
    }

    [TestMethod]
    public void BetweenTheDistances_KeepsTheCurrentOrder()
    {
        var creeps = new[] { At(15f) };

        Assert.AreEqual(PackOrder.Move, Next(PackOrder.Move, creeps, Hero));
        Assert.AreEqual(PackOrder.Charge, Next(PackOrder.Charge, creeps, Hero));
    }

    [TestMethod]
    public void Charge_ACreepBehindTheHeroWithinRelease_StaysCharge()
    {
        var passedAndAhead = new[] { At(-30f), At(-15f) };

        Assert.AreEqual(PackOrder.Charge, Next(PackOrder.Charge, passedAndAhead, Hero), "the release needs every creep, front and rear, past 22 m");
    }

    [TestMethod]
    public void Charge_NoHeroLeft_MovesAtOnce()
    {
        var wideBand = new PackOrderBand { ReleaseMetres = 1000f };

        Assert.AreEqual(PackOrder.Move, PackOrderRule.Next(PackOrder.Charge, new[] { At(1f) }, Array.Empty<Vec3f>(), wideBand));
    }

    [TestMethod]
    public void Charge_ChargedHeroFell_AnotherHeroWithinRelease_StaysCharge()
    {
        var other = At(0f, x: 15f);

        Assert.AreEqual(PackOrder.Charge, Next(PackOrder.Charge, new[] { At(2f) }, other));
    }

    [TestMethod]
    public void Charge_ChargedHeroFell_TheOtherHeroBeyondRelease_Moves()
    {
        var other = At(0f, x: 40f);

        Assert.AreEqual(PackOrder.Move, Next(PackOrder.Charge, new[] { At(2f) }, other));
    }

    [TestMethod]
    public void Move_HeroAboveTheHeightBand_DoesNotCharge()
    {
        Assert.AreEqual(PackOrder.Move, Next(PackOrder.Move, new[] { At(3f) }, At(0f, z: 6.1f)));
        Assert.AreEqual(PackOrder.Charge, Next(PackOrder.Move, new[] { At(3f) }, At(0f, z: 6f)));
    }

    [TestMethod]
    public void Move_TheHeightBand_IsCheckedPerPairBeforeTheMinimum()
    {
        var low = At(0f, z: 0f);
        var high = At(0f, x: 100f, z: 10f);
        var creeps = new[] { At(2f, x: 100f), At(10f) };

        Assert.AreEqual(PackOrder.Charge, Next(PackOrder.Move, creeps, low, high), "the creep under the high hero does not count, the one 10 m from the low hero does");
        Assert.AreEqual(10f, PackOrderRule.NearestFlat(creeps, new[] { low, high }, Band.HeightMetres), 1e-4f);
    }

    [TestMethod]
    public void NoCreep_Moves()
    {
        Assert.AreEqual(PackOrder.Move, Next(PackOrder.Charge, Array.Empty<Vec3f>(), Hero));
        Assert.AreEqual(PackOrder.Move, Next(PackOrder.Move, Array.Empty<Vec3f>(), Hero));
    }

    [TestMethod]
    [DataRow(float.NaN, 0f, 0f)]
    [DataRow(0f, float.NaN, 0f)]
    [DataRow(0f, 0f, float.NaN)]
    [DataRow(float.PositiveInfinity, 0f, 0f)]
    public void NonFinitePosition_NeverPairs(float x, float y, float z)
    {
        var bad = new Vec3f(x, y, z);

        Assert.AreEqual(PackOrder.Move, Next(PackOrder.Move, new[] { bad }, Hero), "a bad creep never starts a charge");
        Assert.AreEqual(PackOrder.Move, Next(PackOrder.Charge, new[] { At(5f) }, bad), "a bad hero position counts as no hero");
        Assert.AreEqual(PackOrder.Charge, Next(PackOrder.Move, new[] { bad, At(5f) }, Hero), "the good creep still counts");
    }
}
