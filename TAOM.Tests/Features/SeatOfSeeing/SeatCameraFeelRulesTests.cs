using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Client.Features.SeatOfSeeing;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Tests.Features.SeatOfSeeing;

/// <summary>The Seat's camera feel rules with no engine type: the edge scroll, the smooth zoom and the rotation pivot.</summary>
[TestClass]
public class SeatCameraFeelRulesTests
{
    private const float Eps = 0.0005f;
    private const float Margin = SeatEdgeScroll.Margin;

    // ---- SeatEdgeScroll ----

    [TestMethod]
    public void Edge_InTheMiddleOfTheWindow_PushesNothing()
    {
        Assert.AreEqual((0f, 0f), SeatEdgeScroll.Axes(0.5f, 0.5f, Margin));
        Assert.AreEqual((0f, 0f), SeatEdgeScroll.Axes(0.1f, 0.9f, Margin));
    }

    [TestMethod]
    public void Edge_AtTheRightEdge_PushesFullRight_AtTheLeftEdge_FullLeft()
    {
        Assert.AreEqual((1f, 0f), SeatEdgeScroll.Axes(0.995f, 0.5f, Margin));
        Assert.AreEqual((1f, 0f), SeatEdgeScroll.Axes(1f, 0.5f, Margin));
        Assert.AreEqual((-1f, 0f), SeatEdgeScroll.Axes(0.005f, 0.5f, Margin));
        Assert.AreEqual((-1f, 0f), SeatEdgeScroll.Axes(0f, 0.5f, Margin));
    }

    [TestMethod]
    public void Edge_AtTheTopEdge_PushesForward_AtTheBottomEdge_Back()
    {
        var top = SeatEdgeScroll.Axes(0.5f, 0.0f, Margin);
        var bottom = SeatEdgeScroll.Axes(0.5f, 1.0f, Margin);

        Assert.AreEqual(1f, top.Forward, Eps, "the top of the window is ahead of the view");
        Assert.AreEqual(-1f, bottom.Forward, Eps);
    }

    [TestMethod]
    public void Edge_InACorner_PushesBothAxes()
    {
        var (right, forward) = SeatEdgeScroll.Axes(1f, 0f, Margin);

        Assert.AreEqual(1f, right, Eps);
        Assert.AreEqual(1f, forward, Eps);
    }

    [TestMethod]
    public void Edge_RampsDownLinearly_OverTheFurtherTwoPercent()
    {
        // Full at 0.01 from the edge, half at 0.02, nothing at 0.03.
        Assert.AreEqual(1f, SeatEdgeScroll.Axes(1f - 0.01f, 0.5f, Margin).Right, Eps);
        Assert.AreEqual(0.5f, SeatEdgeScroll.Axes(1f - 0.02f, 0.5f, Margin).Right, Eps);
        Assert.AreEqual(0f, SeatEdgeScroll.Axes(1f - 0.03f, 0.5f, Margin).Right, Eps);
        Assert.AreEqual(0f, SeatEdgeScroll.Axes(1f - 0.05f, 0.5f, Margin).Right, Eps);
        Assert.AreEqual(-0.5f, SeatEdgeScroll.Axes(0.02f, 0.5f, Margin).Right, Eps);
    }

    [DataTestMethod]
    [DataRow(-0.001f, 0.5f)]
    [DataRow(1.001f, 0.5f)]
    [DataRow(0.5f, -0.2f)]
    [DataRow(0.5f, 1.5f)]
    [DataRow(1.2f, 1.2f)]
    [DataRow(float.NaN, 0.5f)]
    [DataRow(0.5f, float.NaN)]
    [DataRow(float.PositiveInfinity, 0.5f)]
    [DataRow(0.5f, float.NegativeInfinity)]
    public void Edge_WithThePointerOutsideTheWindow_OrNotANumber_PushesNothing(float x, float y)
    {
        // A windowed game with the pointer on another monitor must not scroll: that reads below 0 or above 1.
        Assert.AreEqual((0f, 0f), SeatEdgeScroll.Axes(x, y, Margin));
    }

    [TestMethod]
    public void Edge_WithABadMargin_UsesTheStandardOne()
    {
        Assert.AreEqual(1f, SeatEdgeScroll.Axes(0.995f, 0.5f, float.NaN).Right, Eps);
        Assert.AreEqual(1f, SeatEdgeScroll.Axes(0.995f, 0.5f, -1f).Right, Eps);
    }

    // ---- SeatZoom ----

    [TestMethod]
    public void Zoom_OneNotch_MultipliesTheTargetBy088_AndEasesTheHeightTowardIt()
    {
        var after = SeatZoom.Step(20f, 20f, 1f, 1f / 60f);

        Assert.AreEqual(17.6f, after.Target, Eps);
        Assert.AreEqual(20f + (17.6f - 20f) * (1f - (float)Math.Exp(-12f / 60f)), after.Height, Eps);
        Assert.IsTrue(after.Height < 20f && after.Height > 17.6f, "one frame covers only part of the way");
    }

    [TestMethod]
    public void Zoom_AfterTheNotch_FallsMonotonically_AndReaches088Within05Seconds()
    {
        var state = SeatZoom.Step(20f, 20f, 1f, 1f / 60f);
        var previous = state.Height;
        for (var frame = 0; frame < 30; frame++)
        {
            state = SeatZoom.Step(state.Height, state.Target, 0f, 1f / 60f);
            Assert.IsTrue(state.Height <= previous, "the height never rises while it eases down");
            previous = state.Height;
        }

        Assert.AreEqual(17.6f, state.Height, 17.6f * 0.01f, "half a second of frames covers more than 99% of the way");
    }

    [TestMethod]
    public void Zoom_NotchesThatArriveTogether_StackOnTheTarget()
    {
        var after = SeatZoom.Step(20f, 20f, 3f, 1f / 60f);

        Assert.AreEqual(20f * (float)Math.Pow(0.88, 3), after.Target, Eps);
    }

    [TestMethod]
    public void Zoom_TheWheelBack_RaisesTheTarget()
    {
        var after = SeatZoom.Step(20f, 20f, -1f, 1f / 60f);

        Assert.AreEqual(20f / 0.88f, after.Target, 0.001f);
        Assert.IsTrue(after.Height > 20f);
    }

    [TestMethod]
    public void Zoom_TheTarget_IsHeldToTheLowestHeightAndTheHighest()
    {
        Assert.AreEqual(SeatCameraMath.MinHeight, SeatZoom.Step(2f, 2f, 40f, 1f / 60f).Target, Eps);
        Assert.AreEqual(SeatCameraMath.MaxHeight, SeatZoom.Step(100f, 100f, -40f, 1f / 60f).Target, Eps);
    }

    [TestMethod]
    public void Zoom_TheTarget_IsHeldToTheCeilingTheCapGives()
    {
        var after = SeatZoom.Step(30f, 30f, -5f, 1f / 60f, ceiling: 35f);

        Assert.AreEqual(35f, after.Target, Eps, "the wheel cannot wind a target up past the height limit");
        Assert.AreEqual(SeatCameraMath.MinHeight, SeatZoom.Step(30f, 30f, 0f, 1f / 60f, ceiling: 0.2f).Target, Eps, "a ceiling under the floor is the floor");
    }

    [TestMethod]
    public void Zoom_ATargetAboveANewCeiling_IsBroughtDownToIt()
    {
        var after = SeatZoom.Step(50f, 80f, 0f, 1f / 60f, ceiling: 40f);

        Assert.AreEqual(40f, after.Target, Eps);
        Assert.IsTrue(after.Height < 50f);
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(-0.01f)]
    [DataRow(float.PositiveInfinity)]
    public void Zoom_WithABadDt_ReturnsTheStateAsItIs(float dt)
    {
        var after = SeatZoom.Step(20f, 18f, 1f, dt);

        Assert.AreEqual(20f, after.Height);
        Assert.AreEqual(18f, after.Target);
    }

    [TestMethod]
    public void Zoom_WithBadNotchesTargetOrCeiling_StaysFinite()
    {
        var notches = SeatZoom.Step(20f, 20f, float.NaN, 1f / 60f);
        var target = SeatZoom.Step(20f, float.NaN, 0f, 1f / 60f);
        var ceiling = SeatZoom.Step(20f, 20f, 0f, 1f / 60f, ceiling: float.NaN);

        Assert.AreEqual(20f, notches.Target, Eps, "a notch count that is not a number counts as zero");
        Assert.AreEqual(20f, target.Target, Eps, "a target that is not a number starts from the height");
        Assert.AreEqual(20f, ceiling.Target, Eps);
    }

    [TestMethod]
    public void Zoom_AtDtZero_MovesNoHeight()
    {
        var after = SeatZoom.Step(20f, 20f, 1f, 0f);

        Assert.AreEqual(20f, after.Height);
        Assert.AreEqual(17.6f, after.Target, Eps);
    }

    // ---- SeatPivot ----

    [TestMethod]
    public void Pivot_IsWhereTheCentreRayMeetsTheGroundPlane()
    {
        // 20 m up, looking along +Y, pitched 0.6 rad down: the ray lands 20 / tan(0.6) ahead on the plane.
        var found = SeatPivot.Find(new SeatPose(100f, 50f, 120f, 0f, 0.6f), 100f);

        Assert.IsTrue(found.Found);
        Assert.AreEqual(100f, found.X, 0.001f);
        Assert.AreEqual(50f + 20f / (float)Math.Tan(0.6), found.Y, 0.001f);
        Assert.AreEqual(100f, found.Z, 0.001f);
        Assert.AreEqual(20f / (float)Math.Sin(0.6), found.Distance, 0.001f);
    }

    [TestMethod]
    public void Pivot_FollowsTheYaw()
    {
        // A positive yaw turns left: yaw 90 degrees looks along -X.
        var found = SeatPivot.Find(new SeatPose(0f, 0f, 30f, (float)(Math.PI / 2), 0.8f), 0f);

        Assert.IsTrue(found.Found);
        Assert.AreEqual(-30f / (float)Math.Tan(0.8), found.X, 0.01f);
        Assert.AreEqual(0f, found.Y, 0.01f);
    }

    [TestMethod]
    public void Pivot_OrbitFromIt_HoldsTheCameraWhereItIs()
    {
        var camera = new SeatPose(10f, -20f, 45f, 0.7f, 0.9f);
        var found = SeatPivot.Find(camera, 5f);

        var back = SeatOrbit.Pose(SeatPivot.Begin(camera, found));

        Assert.AreEqual(camera.X, back.X, 0.01f);
        Assert.AreEqual(camera.Y, back.Y, 0.01f);
        Assert.AreEqual(camera.Z, back.Z, 0.01f);
    }

    [DataTestMethod]
    [DataRow(0.15f)]
    [DataRow(0.1f)]
    [DataRow(0f)]
    [DataRow(-0.3f)]
    public void Pivot_ForAFlatView_ThereIsNone_SoTheTurnStaysInPlace(float pitch)
    {
        Assert.IsFalse(SeatPivot.Find(new SeatPose(0f, 0f, 20f, 0f, pitch), 0f).Found);
    }

    [TestMethod]
    public void Pivot_FurtherThan300Metres_ThereIsNone()
    {
        // 100 m up at pitch 0.3: 100 / sin(0.3) is 338 m along the ray.
        Assert.IsFalse(SeatPivot.Find(new SeatPose(0f, 0f, 100f, 0f, 0.3f), 0f).Found);
        Assert.IsTrue(SeatPivot.Find(new SeatPose(0f, 0f, 80f, 0f, 0.3f), 0f).Found, "80 m up is 271 m along the ray");
    }

    [TestMethod]
    public void Pivot_AtOrBelowTheGround_ThereIsNone()
    {
        Assert.IsFalse(SeatPivot.Find(new SeatPose(0f, 0f, 10f, 0f, 0.8f), 10f).Found);
        Assert.IsFalse(SeatPivot.Find(new SeatPose(0f, 0f, 10f, 0f, 0.8f), 12f).Found);
    }

    [TestMethod]
    public void Pivot_WithAnyValueNotANumber_ThereIsNone()
    {
        Assert.IsFalse(SeatPivot.Find(new SeatPose(0f, 0f, 20f, 0f, 0.8f), float.NaN).Found);
        Assert.IsFalse(SeatPivot.Find(new SeatPose(float.NaN, 0f, 20f, 0f, 0.8f), 0f).Found);
        Assert.IsFalse(SeatPivot.Find(new SeatPose(0f, 0f, 20f, 0f, float.NaN), 0f).Found);
        Assert.IsFalse(SeatPivot.Find(new SeatPose(0f, 0f, float.PositiveInfinity, 0f, 0.8f), 0f).Found);
    }

    [TestMethod]
    public void Pivot_Step_TurnsTheOrbitByTheFlightsLookUnits_AndKeepsThePointAndTheDistance()
    {
        var start = new SeatOrbitState(10f, 20f, 5f, 0.2f, 0.8f, 40f);

        var after = SeatPivot.Step(start, 100f, -50f);

        Assert.AreEqual(0.2f + 100f * SeatCameraMath.LookPerUnit, after.Yaw, Eps);
        Assert.AreEqual(0.8f - 50f * SeatCameraMath.LookPerUnit, after.Pitch, Eps);
        Assert.AreEqual(start.PointX, after.PointX);
        Assert.AreEqual(start.PointY, after.PointY);
        Assert.AreEqual(start.PointZ, after.PointZ);
        Assert.AreEqual(start.Distance, after.Distance, "a distance over the keep-to orbit's range is not clamped: the camera must not jump");
    }

    [TestMethod]
    public void Pivot_Step_HoldsThePitchToTheOrbitsRange_AndBadLookCountsAsZero()
    {
        var start = new SeatOrbitState(0f, 0f, 0f, 0f, 0.8f, 120f);

        Assert.AreEqual(SeatOrbit.MaxPitch, SeatPivot.Step(start, 0f, 1e6f).Pitch, Eps);
        Assert.AreEqual(SeatOrbit.MinPitch, SeatPivot.Step(start, 0f, -1e6f).Pitch, Eps);
        Assert.AreEqual(start, SeatPivot.Step(start, float.NaN, float.PositiveInfinity));
    }

    [TestMethod]
    public void Pivot_TurnedOverManyFrames_KeepsTheCentreOfTheViewOnThePivot()
    {
        var camera = new SeatPose(0f, -30f, 30f, 0f, 0.7f);
        var found = SeatPivot.Find(camera, 0f);
        var state = SeatPivot.Begin(camera, found);
        for (var i = 0; i < 90; i++)
            state = SeatPivot.Step(state, 5f, 1f);

        var pose = SeatOrbit.Pose(state);
        var again = SeatPivot.Find(pose, 0f);

        Assert.IsTrue(again.Found);
        Assert.AreEqual(found.X, again.X, 0.05f);
        Assert.AreEqual(found.Y, again.Y, 0.05f);
        Assert.IsTrue(Math.Abs(pose.X - camera.X) > 5f, "the camera swung round the point");
    }

    // ---- SeatCameraMath: pan speed and the height ceiling ----

    [TestMethod]
    public void Step_PanSpeed_MultipliesTheDistanceFlown()
    {
        var flight = new SeatFlight(0f, 0f, 20f, 0f, 0.6f);
        var forward = new SeatInput(0f, 1f, 0f, 0f, 0f, 0f, false);

        var normal = SeatCameraMath.Step(flight, forward, 0.1f);
        var fast = SeatCameraMath.Step(flight, forward, 0.1f, 2f);
        var slow = SeatCameraMath.Step(flight, forward, 0.1f, 0.25f);

        Assert.AreEqual(normal.Y * 2f, fast.Y, 0.001f);
        Assert.AreEqual(normal.Y * 0.25f, slow.Y, 0.001f);
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(0f)]
    [DataRow(-2f)]
    [DataRow(float.PositiveInfinity)]
    public void Step_APanSpeedThatIsNotUsable_CountsAsOne(float panSpeed)
    {
        var flight = new SeatFlight(0f, 0f, 20f, 0f, 0.6f);
        var forward = new SeatInput(0f, 1f, 0f, 0f, 0f, 0f, false);

        Assert.AreEqual(SeatCameraMath.Step(flight, forward, 0.1f).Y, SeatCameraMath.Step(flight, forward, 0.1f, panSpeed).Y);
    }

    [TestMethod]
    public void HeightCeiling_IsTheHighestHeight_OrTheRoomUnderTheLimit()
    {
        Assert.AreEqual(SeatCameraMath.MaxHeight, SeatCameraMath.HeightCeiling(0f, 100f), "no limit");
        Assert.AreEqual(40f, SeatCameraMath.HeightCeiling(140f, 100f), Eps);
        Assert.AreEqual(SeatCameraMath.MaxHeight, SeatCameraMath.HeightCeiling(1000f, 100f));
        Assert.AreEqual(SeatCameraMath.MinHeight, SeatCameraMath.HeightCeiling(101f, 100f), "the floor wins over a limit under it");
        Assert.AreEqual(SeatCameraMath.MaxHeight, SeatCameraMath.HeightCeiling(140f, float.NaN), "a ground that is not a number gives no ceiling");
    }

    [TestMethod]
    public void HeightCeiling_AgreesWithTheCapFlightFindsForTheSameLimitAndGround()
    {
        var flight = new SeatFlight(0f, 0f, 90f, 0f, 0.6f);

        var capped = SeatCameraMath.CapFlight(flight, 140f, 100f);

        Assert.AreEqual(SeatCameraMath.HeightCeiling(140f, 100f), capped.Height, Eps);
    }
}
