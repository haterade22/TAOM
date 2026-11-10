using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar.Domain;
using TAOM.Features.HeroDefence.Domain;
using static TAOM.Tests.Features.HeroDefence.Domain.DefenceDriver;

namespace TAOM.Tests.Features.HeroDefence.Domain;

[TestClass]
public class PlacementRulesTests
{
    private static PlaceResult Check(HeroClass heroClass, PlacementProbe probe, params Vec3f[] others) =>
        PlacementRules.Check(heroClass, probe, others, Map, new DefenceRules());

    [TestMethod]
    public void Check_MeleeOnTheRoad_IsAllowed()
    {
        Assert.AreEqual(PlaceResult.Placed, Check(HeroClass.Melee, OnRoad(100f)));
    }

    [TestMethod]
    public void Check_RangedOnAPlateau_IsAllowed()
    {
        Assert.AreEqual(PlaceResult.Placed, Check(HeroClass.Ranged, OnPlateau(100f)));
    }

    [TestMethod]
    public void Check_RangedOnTheRoad_IsAllowed()
    {
        Assert.AreEqual(PlaceResult.Placed, Check(HeroClass.Ranged, OnRoad(100f)));
    }

    [TestMethod]
    public void Check_MeleeOnAPlateau_IsWrongGround()
    {
        Assert.AreEqual(PlaceResult.WrongGround, Check(HeroClass.Melee, OnPlateau(100f)));
    }

    [TestMethod]
    public void Check_MeleeOnARoadFaceCutOffFromTheRoad_IsWrongGround()
    {
        var island = OnRoad(100f) with { LinksToRoad = false };

        Assert.AreEqual(PlaceResult.WrongGround, Check(HeroClass.Melee, island));
    }

    [TestMethod]
    public void Check_RangedOnARoadFaceCutOffFromTheRoad_IsAllowed()
    {
        var island = OnRoad(100f) with { LinksToRoad = false };

        Assert.AreEqual(PlaceResult.Placed, Check(HeroClass.Ranged, island));
    }

    [DataTestMethod]
    [DataRow((int)HeroClass.Melee)]
    [DataRow((int)HeroClass.Ranged)]
    public void Check_AFaceGroupThatIsNoZone_IsWrongGround(int heroClass)
    {
        var yard = OnRoad(100f) with { FaceGroup = YardFace };

        Assert.AreEqual(PlaceResult.WrongGround, Check((HeroClass)heroClass, yard));
    }

    [TestMethod]
    public void Check_OffTheNavmesh_IsRefused()
    {
        var off = OnRoad(100f) with { FaceGroup = -1 };

        Assert.AreEqual(PlaceResult.OffNavmesh, Check(HeroClass.Melee, off));
    }

    [TestMethod]
    public void Check_OutsideTheBoundary_IsRefused()
    {
        var outside = OnRoad(100f) with { InsideBoundary = false };

        Assert.AreEqual(PlaceResult.OutOfBounds, Check(HeroClass.Melee, outside));
    }

    [DataTestMethod]
    [DataRow(float.NaN, 0f, 0f)]
    [DataRow(0f, float.NaN, 0f)]
    [DataRow(100f, 0f, float.NaN)]
    [DataRow(float.PositiveInfinity, 0f, 0f)]
    [DataRow(100f, float.NegativeInfinity, 0f)]
    public void Check_NonFinitePoint_IsRefused(float x, float y, float z)
    {
        var bad = OnRoad(100f) with { Point = new Vec3f(x, y, z) };

        Assert.AreEqual(PlaceResult.OffNavmesh, Check(HeroClass.Melee, bad));
    }

    [DataTestMethod]
    [DataRow(29f, (int)PlaceResult.TooCloseToRoadEnd)]
    [DataRow(30f, (int)PlaceResult.Placed)]
    public void Check_NearTheEntry_NeedsTheClearDistance(float x, int expected)
    {
        Assert.AreEqual((PlaceResult)expected, Check(HeroClass.Melee, OnRoad(x)));
    }

    [DataTestMethod]
    [DataRow(471f, (int)PlaceResult.TooCloseToRoadEnd)]
    [DataRow(470f, (int)PlaceResult.Placed)]
    public void Check_NearTheExit_NeedsTheClearDistance(float x, int expected)
    {
        Assert.AreEqual((PlaceResult)expected, Check(HeroClass.Melee, OnRoad(x)));
    }

    [TestMethod]
    public void Check_RangedNearTheExit_IsAlsoRefused()
    {
        var near = OnPlateau(100f) with { Point = new Vec3f(490f, 0f, 8f) };

        Assert.AreEqual(PlaceResult.TooCloseToRoadEnd, Check(HeroClass.Ranged, near));
    }

    [DataTestMethod]
    [DataRow(102f, (int)PlaceResult.TooCloseToPost)]
    [DataRow(103f, (int)PlaceResult.Placed)]
    public void Check_NearAnotherPost_NeedsTheGap(float x, int expected)
    {
        Assert.AreEqual((PlaceResult)expected, Check(HeroClass.Melee, OnRoad(x), new Vec3f(100f, 0f, 0f)));
    }

    [TestMethod]
    public void Check_AnotherPostOnADifferentHeight_StillCountsByGroundDistance()
    {
        Assert.AreEqual(PlaceResult.TooCloseToPost, Check(HeroClass.Melee, OnRoad(100f), new Vec3f(100f, 0f, 8f)));
    }

    [TestMethod]
    public void Check_EmptyZoneMap_IsWrongGround()
    {
        var empty = new DefenceMap(Entry, Exit, new System.Collections.Generic.Dictionary<int, GroundZone>());

        Assert.AreEqual(PlaceResult.WrongGround, PlacementRules.Check(HeroClass.Melee, OnRoad(100f), Array.Empty<Vec3f>(), empty, new DefenceRules()));
    }
}
