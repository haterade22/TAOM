using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar.Domain;
using TAOM.Features.HeroAbilities.Ranged;

namespace TAOM.Tests.Features.HeroAbilities;

[TestClass]
public class ExtraShotPickerTests
{
    private static readonly Vec3f Origin = new Vec3f(0f, 0f, 1.5f);
    private static readonly Vec3f East = new Vec3f(1f, 0f, 0f);

    private static ExtraShotPick Pick(IReadOnlyList<Vec3f> c, int extra = 2, float spread = 10f, float range = 120f, Vec3f? dir = null) =>
        ExtraShotPicker.Pick(Origin, dir ?? East, c, extra, spread, range);

    [TestMethod]
    public void Pick_PrimaryIsNearestTheShotLine()
    {
        var c = new[] { new Vec3f(50f, 4f, 0f), new Vec3f(50f, 1f, 0f), new Vec3f(50f, -2f, 0f) };

        Assert.AreEqual(1, Pick(c).Primary);
    }

    [TestMethod]
    public void Pick_EqualAngle_NearerWins()
    {
        var c = new[] { new Vec3f(80f, 0f, 0f), new Vec3f(40f, 0f, 0f) };

        Assert.AreEqual(1, Pick(c).Primary);
    }

    [TestMethod]
    public void Pick_NoneInsideCone_NoPrimaryNoExtras()
    {
        // 50 m ahead and 8 m aside is 9.1 degrees; the cone is 6.
        var c = new[] { new Vec3f(50f, 8f, 0f), new Vec3f(50f, 9f, 0f) };

        var pick = Pick(c);

        Assert.AreEqual(-1, pick.Primary);
        Assert.AreEqual(0, pick.Extras.Count);
    }

    [TestMethod]
    public void Pick_ExtrasNearestPrimaryWithinSpread_UpToN()
    {
        var c = new[]
        {
            new Vec3f(50f, 0f, 0f), // primary
            new Vec3f(50f, 9f, 0f), // 9 from the primary
            new Vec3f(50f, 3f, 0f), // 3 from the primary
            new Vec3f(50f, -6f, 0f), // 6 from the primary
        };

        var pick = Pick(c, extra: 2);

        Assert.AreEqual(0, pick.Primary);
        CollectionAssert.AreEqual(new[] { 2, 3 }, new List<int>(pick.Extras));
    }

    [TestMethod]
    public void Pick_OutsideSpread_Excluded()
    {
        var c = new[] { new Vec3f(50f, 0f, 0f), new Vec3f(50f, 10.5f, 0f) };

        Assert.AreEqual(0, Pick(c).Extras.Count);
    }

    [TestMethod]
    public void Pick_BeyondRange_Excluded()
    {
        var c = new[] { new Vec3f(50f, 0f, 0f), new Vec3f(130f, 0f, 0f), new Vec3f(125f, 0f, 0f) };

        var pick = Pick(c, spread: 100f, range: 120f);

        Assert.AreEqual(0, pick.Primary);
        Assert.AreEqual(0, pick.Extras.Count);
        Assert.AreEqual(-1, Pick(new[] { new Vec3f(130f, 0f, 0f) }).Primary, "a primary beyond range is no primary");
    }

    [TestMethod]
    public void Pick_PrimaryNeverAnExtra()
    {
        var c = new[] { new Vec3f(50f, 0f, 0f), new Vec3f(50f, 1f, 0f) };

        var pick = Pick(c, extra: 3);

        CollectionAssert.DoesNotContain(new List<int>(pick.Extras), pick.Primary);
        CollectionAssert.AreEqual(new[] { 1 }, new List<int>(pick.Extras));
    }

    [DataTestMethod]
    [DataRow(0, 10f)]
    [DataRow(-1, 10f)]
    [DataRow(2, 0f)]
    [DataRow(2, float.NaN)]
    public void Pick_ZeroArrowsOrSpread_NoExtras(int extra, float spread)
    {
        var c = new[] { new Vec3f(50f, 0f, 0f), new Vec3f(50f, 1f, 0f) };

        var pick = Pick(c, extra: extra, spread: spread);

        Assert.AreEqual(0, pick.Primary);
        Assert.AreEqual(0, pick.Extras.Count);
    }

    [TestMethod]
    public void Pick_NonFiniteCandidate_Dropped()
    {
        var c = new[]
        {
            new Vec3f(float.NaN, 0f, 0f),
            new Vec3f(50f, float.PositiveInfinity, 0f),
            new Vec3f(50f, 0f, 0f),
            new Vec3f(50f, 2f, float.NaN),
        };

        var pick = Pick(c);

        Assert.AreEqual(2, pick.Primary);
        Assert.AreEqual(0, pick.Extras.Count);
    }

    [TestMethod]
    public void Pick_VerticalOrNaNShot_NoPrimary()
    {
        var c = new[] { new Vec3f(50f, 0f, 0f) };

        Assert.AreEqual(-1, Pick(c, dir: new Vec3f(0f, 0f, 1f)).Primary);
        Assert.AreEqual(-1, Pick(c, dir: new Vec3f(float.NaN, 0f, 0f)).Primary);
        Assert.AreEqual(-1, Pick(c, dir: new Vec3f(0f, float.PositiveInfinity, 0f)).Primary);
    }

    [TestMethod]
    public void Pick_ConeIsSixDegrees()
    {
        var inside = (float)(50.0 * Math.Tan(5.9 * Math.PI / 180.0));
        var outside = (float)(50.0 * Math.Tan(6.1 * Math.PI / 180.0));

        Assert.AreEqual(0, Pick(new[] { new Vec3f(50f, inside, 0f) }).Primary);
        Assert.AreEqual(-1, Pick(new[] { new Vec3f(50f, outside, 0f) }).Primary);
    }
}
