using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CultureDoctrine.Doctrines;
using TaleWorlds.Library;

namespace TAOM.Tests.Features.CultureDoctrine;

/// <summary>
/// The players' answer to cavalry and to archers caught in the open (Mike, 2026-09-17): the
/// wall in front, the archers beside it, set a little back, so the enemy AI goes for the
/// closer wall and the bows have a clear line; not skirmishing ahead of the wall and backing
/// through it. The fourth battle lost 116 of 122 archers standing behind the wall.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class ArcherFlankGeometryTests
{
    // The wall stands at the origin facing +Y (the enemy is north); right is +X.
    private static readonly Vec2 Wall = Vec2.Zero;
    private static readonly Vec2 Facing = new Vec2(0f, 1f);

    [TestMethod]
    public void FlankPoint_Right_IsBesideTheWallAndSetBack()
    {
        var p = ArcherFlankGeometry.FlankPoint(Wall, Facing, wallWidth: 40f, archersWidth: 20f, ArcherFlankGeometry.FlankSide.Right, gap: 10f, setBack: 8f);
        Assert.AreEqual(40f, p.x, 1e-4f, "half the wall (20) + gap (10) + half the archers (10)");
        Assert.AreEqual(-8f, p.y, 1e-4f, "eight metres behind the wall's centre line");
    }

    [TestMethod]
    public void FlankPoint_Left_MirrorsRight()
    {
        var p = ArcherFlankGeometry.FlankPoint(Wall, Facing, 40f, 20f, ArcherFlankGeometry.FlankSide.Left, 10f, 8f);
        Assert.AreEqual(-40f, p.x, 1e-4f);
        Assert.AreEqual(-8f, p.y, 1e-4f);
    }

    [TestMethod]
    public void FlankPoint_RotatesWithTheFacing()
    {
        // Facing east: right is south.
        var p = ArcherFlankGeometry.FlankPoint(Wall, new Vec2(1f, 0f), 40f, 20f, ArcherFlankGeometry.FlankSide.Right, 10f, 8f);
        Assert.AreEqual(-8f, p.x, 1e-4f, "set back is west of an east-facing wall");
        Assert.AreEqual(-40f, p.y, 1e-4f, "right of an east-facing wall is south");
    }

    [TestMethod]
    public void BehindPoint_IsStraightBehindTheWall()
    {
        var p = ArcherFlankGeometry.BehindPoint(Wall, Facing, 15f);
        Assert.AreEqual(0f, p.x, 1e-4f);
        Assert.AreEqual(-15f, p.y, 1e-4f);
    }

    [TestMethod]
    public void ChooseSide_TakesTheSideWithFewerEnemies()
    {
        var enemies = new List<Vec2> { new Vec2(30f, 100f), new Vec2(50f, 80f), new Vec2(-10f, 120f) };
        Assert.AreEqual(ArcherFlankGeometry.FlankSide.Left, ArcherFlankGeometry.ChooseSide(Wall, Facing, enemies), "two enemy formations on the right, one on the left");
    }

    [TestMethod]
    public void ChooseSide_Tie_IsRight()
    {
        Assert.AreEqual(ArcherFlankGeometry.FlankSide.Right, ArcherFlankGeometry.ChooseSide(Wall, Facing, new List<Vec2>()));
        Assert.AreEqual(ArcherFlankGeometry.FlankSide.Right, ArcherFlankGeometry.ChooseSide(Wall, Facing, new List<Vec2> { new Vec2(0f, 100f) }), "dead ahead counts for neither side");
    }

    [TestMethod]
    public void ShouldFallBack_WhenAnEnemyIsCloserToTheArchersThanToTheWall_InsideTheTrigger()
    {
        Assert.IsTrue(ArcherFlankGeometry.ShouldFallBack(nearestToArchers: 25f, thatEnemysDistanceToWall: 40f, trigger: 30f, fallingBack: false));
        Assert.IsFalse(ArcherFlankGeometry.ShouldFallBack(25f, 20f, 30f, false), "closer to the wall than to us: the wall's fight");
        Assert.IsFalse(ArcherFlankGeometry.ShouldFallBack(31f, 60f, 30f, false), "outside the trigger");
    }

    [TestMethod]
    public void ShouldFallBack_Hysteresis_ReturnsOnlyBeyondOneAndAHalfTimesTheTrigger()
    {
        Assert.IsTrue(ArcherFlankGeometry.ShouldFallBack(40f, 80f, 30f, fallingBack: true), "still inside 45 m: stay behind the wall");
        Assert.IsFalse(ArcherFlankGeometry.ShouldFallBack(46f, 80f, 30f, fallingBack: true), "past 45 m: back to the flank");
    }

    [TestMethod]
    public void ShouldFallBack_NaN_HoldsTheCurrentState()
    {
        Assert.IsFalse(ArcherFlankGeometry.ShouldFallBack(float.NaN, 10f, 30f, false));
        Assert.IsTrue(ArcherFlankGeometry.ShouldFallBack(float.NaN, 10f, 30f, true));
    }

    [TestMethod]
    public void Defaults_MatchTheNumbersAgreedWithMike()
    {
        Assert.AreEqual(10f, ArcherFlankGeometry.Gap);
        Assert.AreEqual(8f, ArcherFlankGeometry.SetBack);
        Assert.AreEqual(30f, ArcherFlankGeometry.FallBackTrigger);
        Assert.AreEqual(15f, ArcherFlankGeometry.BehindDistance);
    }
}
