using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Client.Features.FordsOfWar;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Tests.Features.FordsOfWar.Client;

/// <summary>The commander camera's arithmetic (build-spec 6.1): pan relative to yaw, zoom clamp, pitch from height, boundary clamp.</summary>
[TestClass]
public class FordsCameraMathTests
{
    private const float Eps = 0.001f;
    private const float Dt = 0.1f;

    private static readonly IReadOnlyList<Vec3f> Square = new List<Vec3f>
    {
        new(0f, 0f, 0f), new(1000f, 0f, 0f), new(1000f, 500f, 0f), new(0f, 500f, 0f),
    };

    private static FordsCameraState At(float x, float y, float yaw = 0f, float height = 45f, float tilt = 0f) =>
        new(x, y, yaw, height, tilt);

    private static FordsCameraInput Pan(float forward = 0f, float right = 0f, bool fast = false) =>
        new(forward, right, 0f, 0f, 0f, 0f, fast);

    [TestMethod]
    public void Step_PanForwardAtYawZero_MovesAlongPlusY()
    {
        var next = FordsCameraMath.Step(At(500f, 250f), Pan(forward: 1f), Dt, Square);

        Assert.AreEqual(500f, next.X, Eps);
        Assert.IsTrue(next.Y > 250f);
    }

    [TestMethod]
    public void Step_PanForwardAtQuarterTurn_MovesAlongMinusX()
    {
        var next = FordsCameraMath.Step(At(500f, 250f, yaw: (float)(Math.PI / 2)), Pan(forward: 1f), Dt, Square);

        Assert.IsTrue(next.X < 500f);
        Assert.AreEqual(250f, next.Y, Eps);
    }

    [TestMethod]
    public void Step_PanRightAtYawZero_MovesAlongPlusX()
    {
        var next = FordsCameraMath.Step(At(500f, 250f), Pan(right: 1f), Dt, Square);

        Assert.IsTrue(next.X > 500f);
        Assert.AreEqual(250f, next.Y, Eps);
    }

    [TestMethod]
    public void Step_PanRightAtQuarterTurn_MovesAlongPlusY()
    {
        var next = FordsCameraMath.Step(At(500f, 250f, yaw: (float)(Math.PI / 2)), Pan(right: 1f), Dt, Square);

        Assert.AreEqual(500f, next.X, Eps);
        Assert.IsTrue(next.Y > 250f);
    }

    [TestMethod]
    public void Step_ShiftHeld_DoublesThePanDistance()
    {
        var slow = FordsCameraMath.Step(At(500f, 250f), Pan(forward: 1f), Dt, Square);
        var fast = FordsCameraMath.Step(At(500f, 250f), Pan(forward: 1f, fast: true), Dt, Square);

        Assert.AreEqual(2f * (slow.Y - 250f), fast.Y - 250f, Eps);
    }

    [TestMethod]
    public void Step_DiagonalPan_IsNoFasterThanStraight()
    {
        var straight = FordsCameraMath.Step(At(500f, 250f), Pan(forward: 1f), Dt, Square);
        var diagonal = FordsCameraMath.Step(At(500f, 250f), Pan(forward: 1f, right: 1f), Dt, Square);

        var straightDistance = Math.Abs(straight.Y - 250f);
        var diagonalDistance = (float)Math.Sqrt(Math.Pow(diagonal.X - 500f, 2) + Math.Pow(diagonal.Y - 250f, 2));
        Assert.AreEqual(straightDistance, diagonalDistance, Eps);
    }

    [TestMethod]
    public void PanSpeed_AtTheHighestZoom_IsFasterThanAtTheLowest()
    {
        Assert.IsTrue(FordsCameraMath.PanSpeed(FordsCameraMath.MaxHeight) > FordsCameraMath.PanSpeed(FordsCameraMath.MinHeight));
    }

    [TestMethod]
    public void Step_RotateKeys_TurnTheYawBothWays()
    {
        var left = FordsCameraMath.Step(At(500f, 250f), new FordsCameraInput(0f, 0f, 1f, 0f, 0f, 0f, false), Dt, Square);
        var right = FordsCameraMath.Step(At(500f, 250f), new FordsCameraInput(0f, 0f, -1f, 0f, 0f, 0f, false), Dt, Square);

        Assert.IsTrue(left.Yaw > 0f);
        Assert.AreEqual(-left.Yaw, right.Yaw, Eps);
    }

    [TestMethod]
    public void Step_YawPastHalfATurn_WrapsIntoMinusPiToPi()
    {
        var next = FordsCameraMath.Step(At(500f, 250f, yaw: 3.1f), new FordsCameraInput(0f, 0f, 1f, 0f, 0f, 0f, false), 1f, Square);

        Assert.IsTrue(next.Yaw <= Math.PI && next.Yaw > -Math.PI);
        Assert.IsTrue(next.Yaw < 0f, "3.1 rad plus a turn of 1.6 rad wraps to the negative side");
    }

    [TestMethod]
    public void Step_ZoomIn_LowersTheHeight()
    {
        var next = FordsCameraMath.Step(At(500f, 250f, height: 60f), new FordsCameraInput(0f, 0f, 0f, 1f, 0f, 0f, false), Dt, Square);

        Assert.IsTrue(next.Height < 60f);
    }

    [TestMethod]
    public void Step_ZoomOut_RaisesTheHeight()
    {
        var next = FordsCameraMath.Step(At(500f, 250f, height: 60f), new FordsCameraInput(0f, 0f, 0f, -1f, 0f, 0f, false), Dt, Square);

        Assert.IsTrue(next.Height > 60f);
    }

    [TestMethod]
    public void Step_ZoomInForever_StopsAtFifteenMetres()
    {
        var next = FordsCameraMath.Step(At(500f, 250f, height: 60f), new FordsCameraInput(0f, 0f, 0f, 500f, 0f, 0f, false), Dt, Square);

        Assert.AreEqual(15f, next.Height, Eps);
    }

    [TestMethod]
    public void Step_ZoomOutForever_StopsAtOneHundredTwentyMetres()
    {
        var next = FordsCameraMath.Step(At(500f, 250f, height: 60f), new FordsCameraInput(0f, 0f, 0f, -500f, 0f, 0f, false), Dt, Square);

        Assert.AreEqual(120f, next.Height, Eps);
    }

    [TestMethod]
    public void Step_AHeightOutsideTheRange_IsPulledBackIn()
    {
        var next = FordsCameraMath.Step(At(500f, 250f, height: 900f), Pan(), Dt, Square);

        Assert.AreEqual(120f, next.Height, Eps);
    }

    [TestMethod]
    public void Pitch_RisesWithHeight()
    {
        var low = FordsCameraMath.Pitch(At(0f, 0f, height: 15f));
        var mid = FordsCameraMath.Pitch(At(0f, 0f, height: 60f));
        var high = FordsCameraMath.Pitch(At(0f, 0f, height: 120f));

        Assert.IsTrue(low < mid && mid < high, "a higher camera looks steeper down");
    }

    [TestMethod]
    public void Pitch_WithTheTiltOffset_IsKeptInsideTheLimits()
    {
        var flat = FordsCameraMath.Pitch(At(0f, 0f, height: 15f, tilt: -10f));
        var steep = FordsCameraMath.Pitch(At(0f, 0f, height: 120f, tilt: 10f));

        Assert.IsTrue(flat >= FordsCameraMath.MinPitch - Eps);
        Assert.IsTrue(steep <= FordsCameraMath.MaxPitch + Eps);
    }

    [TestMethod]
    public void Step_DragDown_TiltsSteeperAndDragUp_Flatter()
    {
        var down = FordsCameraMath.Step(At(500f, 250f), new FordsCameraInput(0f, 0f, 0f, 0f, 0f, 20f, false), Dt, Square);
        var up = FordsCameraMath.Step(At(500f, 250f), new FordsCameraInput(0f, 0f, 0f, 0f, 0f, -20f, false), Dt, Square);

        Assert.IsTrue(FordsCameraMath.Pitch(down) > FordsCameraMath.Pitch(At(500f, 250f)));
        Assert.IsTrue(FordsCameraMath.Pitch(up) < FordsCameraMath.Pitch(At(500f, 250f)));
    }

    [TestMethod]
    public void Step_DragForever_KeepsTheTiltWithinItsLimit()
    {
        var next = FordsCameraMath.Step(At(500f, 250f), new FordsCameraInput(0f, 0f, 0f, 0f, 0f, 100000f, false), Dt, Square);

        Assert.IsTrue(Math.Abs(next.Tilt) <= FordsCameraMath.MaxTilt + Eps);
    }

    [TestMethod]
    public void Step_DragSideways_TurnsTheYaw()
    {
        var right = FordsCameraMath.Step(At(500f, 250f), new FordsCameraInput(0f, 0f, 0f, 0f, 40f, 0f, false), Dt, Square);
        var left = FordsCameraMath.Step(At(500f, 250f), new FordsCameraInput(0f, 0f, 0f, 0f, -40f, 0f, false), Dt, Square);

        Assert.IsTrue(right.Yaw < 0f, "dragging right turns the view right");
        Assert.AreEqual(-right.Yaw, left.Yaw, Eps);
    }

    [TestMethod]
    public void Step_PanningPastTheEdge_StopsOnTheBoundary()
    {
        var next = FordsCameraMath.Step(At(500f, 499.9f), Pan(forward: 1f, fast: true), 5f, Square);

        Assert.AreEqual(500f, next.Y, Eps);
        Assert.AreEqual(500f, next.X, Eps);
    }

    [TestMethod]
    public void Step_PanningInsideTheBoundary_IsNotClamped()
    {
        var next = FordsCameraMath.Step(At(500f, 250f), Pan(forward: 1f), Dt, Square);

        Assert.IsTrue(next.Y > 250f && next.Y < 500f);
    }

    [TestMethod]
    public void Step_StartingOutsideTheBoundary_IsPulledToTheNearestEdgePoint()
    {
        var next = FordsCameraMath.Step(At(-40f, 250f), Pan(), Dt, Square);

        Assert.AreEqual(0f, next.X, Eps);
        Assert.AreEqual(250f, next.Y, Eps);
    }

    [TestMethod]
    public void ClampInside_APointInsideTheNotchOfAnLShape_MovesToTheNearestEdge()
    {
        var l = new List<Vec3f>
        {
            new(0f, 0f, 0f), new(100f, 0f, 0f), new(100f, 50f, 0f), new(50f, 50f, 0f), new(50f, 100f, 0f), new(0f, 100f, 0f),
        };

        var (x, y) = FordsCameraMath.ClampInside(l, 80f, 80f);

        var onLowerArm = Math.Abs(x - 80f) < Eps && Math.Abs(y - 50f) < Eps;
        var onLeftArm = Math.Abs(x - 50f) < Eps && Math.Abs(y - 80f) < Eps;
        Assert.IsTrue(onLowerArm || onLeftArm, $"expected (80,50) or (50,80), got ({x},{y})");
    }

    [TestMethod]
    public void Step_APolygonWithFewerThanThreePoints_DoesNotClamp()
    {
        var line = new List<Vec3f> { new(0f, 0f, 0f), new(10f, 0f, 0f) };

        var next = FordsCameraMath.Step(At(5000f, 5000f), Pan(), Dt, line);

        Assert.AreEqual(5000f, next.X, Eps);
        Assert.AreEqual(5000f, next.Y, Eps);
    }

    [TestMethod]
    public void IsInside_PointsInAndOutOfASquare()
    {
        Assert.IsTrue(FordsCameraMath.IsInside(Square, 10f, 10f));
        Assert.IsFalse(FordsCameraMath.IsInside(Square, -1f, 10f));
        Assert.IsFalse(FordsCameraMath.IsInside(Square, 10f, 501f));
    }

    [TestMethod]
    public void Start_LooksFromTheStartPointTowardTheLookAtPoint()
    {
        var start = FordsCameraMath.Start(new Vec3f(40f, 25f, 60f), new Vec3f(960f, 25f, 0f), 15f);

        Assert.AreEqual(40f, start.X, Eps);
        Assert.AreEqual(25f, start.Y, Eps);
        Assert.AreEqual(45f, start.Height, Eps);
        var forward = FordsCameraMath.Forward(start.Yaw);
        Assert.AreEqual(1f, forward.X, Eps, "east is +X");
        Assert.AreEqual(0f, forward.Y, Eps);
    }

    [TestMethod]
    public void Start_LookingDownThePlusYAxis_HasYawZero()
    {
        var start = FordsCameraMath.Start(new Vec3f(0f, 0f, 30f), new Vec3f(0f, 100f, 0f), 0f);

        Assert.AreEqual(0f, start.Yaw, Eps);
    }

    [TestMethod]
    public void Start_AHeightOutsideTheRange_IsClamped()
    {
        Assert.AreEqual(15f, FordsCameraMath.Start(new Vec3f(0f, 0f, 2f), new Vec3f(0f, 100f, 0f), 0f).Height, Eps);
        Assert.AreEqual(120f, FordsCameraMath.Start(new Vec3f(0f, 0f, 700f), new Vec3f(0f, 100f, 0f), 0f).Height, Eps);
    }

    [TestMethod]
    public void Start_LookAtDirectlyBelow_KeepsYawZeroInsteadOfNaN()
    {
        var start = FordsCameraMath.Start(new Vec3f(10f, 10f, 40f), new Vec3f(10f, 10f, 0f), 0f);

        Assert.AreEqual(0f, start.Yaw, Eps);
    }

    [TestMethod]
    public void LookDirection_AtZeroPitch_IsHorizontalAndAtNinetyDegrees_StraightDown()
    {
        var flat = FordsCameraMath.LookDirection(0f, 0f);
        var down = FordsCameraMath.LookDirection(0f, (float)(Math.PI / 2));

        Assert.AreEqual(0f, flat.X, Eps);
        Assert.AreEqual(1f, flat.Y, Eps);
        Assert.AreEqual(0f, flat.Z, Eps);
        Assert.AreEqual(-1f, down.Z, Eps);
    }

    [TestMethod]
    public void Basis_IsOrthonormalAndTheBackAxisOpposesTheLookDirection()
    {
        var look = FordsCameraMath.LookDirection(0.7f, 0.9f);

        var (side, up, back) = FordsCameraMath.Basis(0.7f, 0.9f);

        Assert.AreEqual(1f, Length(side), Eps);
        Assert.AreEqual(1f, Length(up), Eps);
        Assert.AreEqual(1f, Length(back), Eps);
        Assert.AreEqual(0f, Dot(side, up), Eps);
        Assert.AreEqual(0f, Dot(side, back), Eps);
        Assert.AreEqual(0f, Dot(up, back), Eps);
        Assert.AreEqual(-look.X, back.X, Eps);
        Assert.AreEqual(-look.Y, back.Y, Eps);
        Assert.AreEqual(-look.Z, back.Z, Eps);
        Assert.IsTrue(up.Z > 0f, "the image up axis points toward the sky");
    }

    [TestMethod]
    public void Basis_RightHandedAsTheEngineExpects_SideCrossUpIsBack()
    {
        var (side, up, back) = FordsCameraMath.Basis(-1.2f, 0.6f);

        var cross = new Vec3f(side.Y * up.Z - side.Z * up.Y, side.Z * up.X - side.X * up.Z, side.X * up.Y - side.Y * up.X);
        Assert.AreEqual(back.X, cross.X, Eps);
        Assert.AreEqual(back.Y, cross.Y, Eps);
        Assert.AreEqual(back.Z, cross.Z, Eps);
    }

    private static float Length(Vec3f v) => (float)Math.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);

    private static float Dot(Vec3f a, Vec3f b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
}
