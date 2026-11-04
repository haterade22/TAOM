using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Client.Features.FordsOfWar;
using TAOM.Client.Features.SeatOfSeeing;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Tests.Features.SeatOfSeeing;

/// <summary>
/// The Seat's flight arithmetic (build-spec 5.2): every constant and rule of the table is pinned here. Yaw 0 looks along +Y, a
/// positive yaw turns left, pitch is the angle below the horizon.
/// </summary>
[TestClass]
public class SeatCameraMathTests
{
    private const float Eps = 0.001f;
    private const float Dt = 0.1f;
    private const float Pi = (float)Math.PI;

    private static SeatFlight At(float x = 0f, float y = 0f, float height = 20f, float yaw = 0f, float pitch = 0.6f) =>
        new(x, y, height, yaw, pitch);

    private static SeatInput Input(float right = 0f, float forward = 0f, float lift = 0f, float zoom = 0f,
        float lookYaw = 0f, float lookPitch = 0f, bool fast = false) =>
        new(right, forward, lift, zoom, lookYaw, lookPitch, fast);

    // ---- The constants of the table ----

    [TestMethod]
    public void Constants_MatchTheContractTable()
    {
        Assert.AreEqual(1.5f, SeatCameraMath.MinHeight);
        Assert.AreEqual(110f, SeatCameraMath.MaxHeight);
        Assert.AreEqual(-0.35f, SeatCameraMath.MinPitch);
        Assert.AreEqual(1.45f, SeatCameraMath.MaxPitch);
        Assert.AreEqual(8f, SeatCameraMath.LiftSpeedBase);
        Assert.AreEqual(0.5f, SeatCameraMath.LiftSpeedPerMetre);
        Assert.AreEqual(0.88f, SeatCameraMath.ZoomPerNotch);
        Assert.AreEqual(0.0035f, SeatCameraMath.LookPerUnit);
        Assert.AreEqual(22f, SeatCameraMath.EntryHeight);
        Assert.AreEqual(0.60f, SeatCameraMath.EntryPitch);
        Assert.AreEqual(9f, SeatCameraMath.EntryBackstep);
        Assert.AreEqual(0.7f, SeatCameraMath.EntryBlendSeconds);
        Assert.AreEqual(6f, SeatCameraMath.GroundSmoothingRate);
        Assert.AreEqual(16f, SeatCameraMath.ListenerCeiling);
        Assert.AreEqual(0.5f, SeatCameraMath.GroundClearance);
        Assert.AreEqual(50f, SeatCameraMath.MinViewAngle);
        Assert.AreEqual(0.2f, SeatCameraMath.AxisDeadZone);
    }

    [TestMethod]
    public void PanSpeed_IsTheFordsCameraSpeed()
    {
        Assert.AreEqual(FordsCameraMath.PanSpeed(37f), SeatCameraMath.PanSpeed(37f));
        Assert.AreEqual(43.3f, SeatCameraMath.PanSpeed(37f), Eps);
    }

    [TestMethod]
    public void LiftSpeed_Is8MetresPerSecondPlusHalfAMetrePerMetreOfHeight() =>
        Assert.AreEqual(18f, SeatCameraMath.LiftSpeed(20f), Eps);

    // ---- Step: movement ----

    [TestMethod]
    public void Step_ForwardAtYawZero_MovesAlongPlusYAtPanSpeed()
    {
        var next = SeatCameraMath.Step(At(height: 20f), Input(forward: 1f), Dt);

        Assert.AreEqual(0f, next.X, Eps);
        Assert.AreEqual(SeatCameraMath.PanSpeed(20f) * Dt, next.Y, Eps);
    }

    [TestMethod]
    public void Step_RightAtYawZero_MovesAlongPlusX()
    {
        var next = SeatCameraMath.Step(At(), Input(right: 1f), Dt);

        Assert.AreEqual(SeatCameraMath.PanSpeed(20f) * Dt, next.X, Eps);
        Assert.AreEqual(0f, next.Y, Eps);
    }

    [TestMethod]
    public void Step_ForwardAfterAQuarterTurnLeft_MovesAlongMinusX()
    {
        var next = SeatCameraMath.Step(At(yaw: Pi / 2f), Input(forward: 1f), Dt);

        Assert.AreEqual(-SeatCameraMath.PanSpeed(20f) * Dt, next.X, Eps);
        Assert.AreEqual(0f, next.Y, Eps);
    }

    [TestMethod]
    public void Step_RightAfterAQuarterTurnLeft_MovesAlongPlusY()
    {
        var next = SeatCameraMath.Step(At(yaw: Pi / 2f), Input(right: 1f), Dt);

        Assert.AreEqual(0f, next.X, Eps);
        Assert.AreEqual(SeatCameraMath.PanSpeed(20f) * Dt, next.Y, Eps);
    }

    [TestMethod]
    public void Step_Diagonal_MovesNoFasterThanStraight()
    {
        var next = SeatCameraMath.Step(At(), Input(right: 1f, forward: 1f), Dt);

        var distance = (float)Math.Sqrt(next.X * next.X + next.Y * next.Y);
        Assert.AreEqual(SeatCameraMath.PanSpeed(20f) * Dt, distance, Eps);
    }

    [TestMethod]
    public void Step_PartialStick_ScalesTheSpeed()
    {
        var next = SeatCameraMath.Step(At(), Input(forward: 0.5f), Dt);

        Assert.AreEqual(SeatCameraMath.PanSpeed(20f) * Dt * 0.5f, next.Y, Eps);
    }

    [TestMethod]
    public void Step_Fast_DoublesThePan()
    {
        var slow = SeatCameraMath.Step(At(), Input(forward: 1f), Dt);
        var fast = SeatCameraMath.Step(At(), Input(forward: 1f, fast: true), Dt);

        Assert.AreEqual(slow.Y * 2f, fast.Y, Eps);
    }

    [TestMethod]
    public void Step_Fast_DoublesTheLift()
    {
        var slow = SeatCameraMath.Step(At(), Input(lift: 1f), Dt);
        var fast = SeatCameraMath.Step(At(), Input(lift: 1f, fast: true), Dt);

        Assert.AreEqual((slow.Height - 20f) * 2f, fast.Height - 20f, Eps);
    }

    [TestMethod]
    public void Step_PanSpeedReadsTheHeightBeforeTheZoom()
    {
        var next = SeatCameraMath.Step(At(height: 20f), Input(forward: 1f, zoom: 3f), Dt);

        Assert.AreEqual(SeatCameraMath.PanSpeed(20f) * Dt, next.Y, Eps);
    }

    // ---- Step: height ----

    [TestMethod]
    public void Step_PositiveWheelNotch_LowersTheViewByTheZoomFactor()
    {
        var next = SeatCameraMath.Step(At(height: 50f), Input(zoom: 1f), Dt);

        Assert.AreEqual(44f, next.Height, Eps);
    }

    [TestMethod]
    public void Step_NegativeWheelNotch_RaisesTheView()
    {
        var next = SeatCameraMath.Step(At(height: 44f), Input(zoom: -1f), Dt);

        Assert.AreEqual(44f / 0.88f, next.Height, Eps);
    }

    [TestMethod]
    public void Step_TwoNotches_ApplyTheFactorTwice()
    {
        var next = SeatCameraMath.Step(At(height: 50f), Input(zoom: 2f), Dt);

        Assert.AreEqual(50f * 0.88f * 0.88f, next.Height, Eps);
    }

    [TestMethod]
    public void Step_LiftKey_RaisesAtTheLiftSpeed()
    {
        var next = SeatCameraMath.Step(At(height: 20f), Input(lift: 1f), Dt);

        Assert.AreEqual(20f + SeatCameraMath.LiftSpeed(20f) * Dt, next.Height, Eps);
    }

    [TestMethod]
    public void Step_SinkKey_Lowers()
    {
        var next = SeatCameraMath.Step(At(height: 20f), Input(lift: -1f), Dt);

        Assert.AreEqual(20f - SeatCameraMath.LiftSpeed(20f) * Dt, next.Height, Eps);
    }

    [TestMethod]
    public void Step_ZoomThenLift_BothApply()
    {
        var next = SeatCameraMath.Step(At(height: 50f), Input(zoom: 1f, lift: 1f), Dt);

        Assert.AreEqual(50f * 0.88f + SeatCameraMath.LiftSpeed(50f) * Dt, next.Height, Eps);
    }

    [TestMethod]
    public void Step_Height_ClampsToTheRange()
    {
        Assert.AreEqual(SeatCameraMath.MinHeight, SeatCameraMath.Step(At(height: 1.6f), Input(lift: -1f), 1f).Height, Eps);
        Assert.AreEqual(SeatCameraMath.MaxHeight, SeatCameraMath.Step(At(height: 109f), Input(lift: 1f), 1f).Height, Eps);
        Assert.AreEqual(SeatCameraMath.MaxHeight, SeatCameraMath.Step(At(height: 108f), Input(zoom: -5f), Dt).Height, Eps);
    }

    // ---- Step: look ----

    [TestMethod]
    public void Step_LookYaw_IsScaledByLookPerUnit()
    {
        var next = SeatCameraMath.Step(At(yaw: 0f), Input(lookYaw: 100f), Dt);

        Assert.AreEqual(100f * 0.0035f, next.Yaw, Eps);
    }

    [TestMethod]
    public void Step_LookPitch_IsScaledByLookPerUnit()
    {
        var next = SeatCameraMath.Step(At(pitch: 0.6f), Input(lookPitch: -50f), Dt);

        Assert.AreEqual(0.6f - 50f * 0.0035f, next.Pitch, Eps);
    }

    [TestMethod]
    public void Step_Pitch_ClampsBetweenLookingUpALittleAndNearlyStraightDown()
    {
        Assert.AreEqual(SeatCameraMath.MinPitch, SeatCameraMath.Step(At(pitch: 0f), Input(lookPitch: -10000f), Dt).Pitch, Eps);
        Assert.AreEqual(SeatCameraMath.MaxPitch, SeatCameraMath.Step(At(pitch: 1f), Input(lookPitch: 10000f), Dt).Pitch, Eps);
    }

    [TestMethod]
    public void Step_Yaw_WrapsPastPiToTheOtherSide()
    {
        var next = SeatCameraMath.Step(At(yaw: 3.0f), Input(lookYaw: 100f), Dt);

        Assert.AreEqual(3.0f + 0.35f - 2f * Pi, next.Yaw, Eps);
        Assert.IsTrue(next.Yaw > -Pi && next.Yaw <= Pi);
    }

    [TestMethod]
    public void Step_Yaw_WrapsPastMinusPi()
    {
        var next = SeatCameraMath.Step(At(yaw: -3.0f), Input(lookYaw: -100f), Dt);

        Assert.AreEqual(-3.0f - 0.35f + 2f * Pi, next.Yaw, Eps);
    }

    // ---- Step: bad numbers ----

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(float.NegativeInfinity)]
    [DataRow(-0.1f)]
    public void Step_BadDt_LeavesTheStateUnchanged(float dt)
    {
        var state = At(12f, 34f, 20f, 1f, 0.5f);

        var next = SeatCameraMath.Step(state, Input(1f, 1f, 1f, 1f, 10f, 10f, true), dt);

        Assert.AreEqual(state, next);
    }

    [TestMethod]
    public void Step_ZeroDt_MovesNothingButStillAppliesTheWheel()
    {
        var next = SeatCameraMath.Step(At(height: 50f), Input(forward: 1f, zoom: 1f), 0f);

        Assert.AreEqual(0f, next.Y, Eps);
        Assert.AreEqual(44f, next.Height, Eps);
    }

    [TestMethod]
    public void Step_NonFiniteInputFields_AreIgnoredAndNeverReachTheState()
    {
        var next = SeatCameraMath.Step(At(), Input(float.NaN, float.PositiveInfinity, float.NaN, float.NegativeInfinity,
            float.NaN, float.PositiveInfinity), Dt);

        Assert.AreEqual(At(), next);
    }

    [TestMethod]
    public void Step_OneBadInputField_DoesNotStopTheGoodOnes()
    {
        var next = SeatCameraMath.Step(At(), Input(forward: 1f, zoom: float.NaN), Dt);

        Assert.AreEqual(SeatCameraMath.PanSpeed(20f) * Dt, next.Y, Eps);
        Assert.AreEqual(20f, next.Height, Eps);
    }

    // ---- Pose ----

    [TestMethod]
    public void Pose_PutsTheCameraTheHeightAboveTheGround()
    {
        var pose = SeatCameraMath.Pose(At(5f, 6f, 20f, 0.3f, 0.7f), groundZ: 100f);

        Assert.AreEqual(new SeatPose(5f, 6f, 120f, 0.3f, 0.7f), pose);
    }

    // ---- Entry ----

    [TestMethod]
    public void Enter_FirstTime_RisesToTheEntryHeightAndPitchBehindTheCamera()
    {
        var camera = new SeatPose(100f, 200f, 103f, 0f, 0.1f);

        var flight = SeatCameraMath.Enter(camera, last: null);

        Assert.AreEqual(100f, flight.X, Eps);
        Assert.AreEqual(200f - 9f, flight.Y, Eps);
        Assert.AreEqual(22f, flight.Height, Eps);
        Assert.AreEqual(0.60f, flight.Pitch, Eps);
        Assert.AreEqual(0f, flight.Yaw, Eps);
    }

    [TestMethod]
    public void Enter_Backstep_RunsAgainstTheLookDirection()
    {
        var camera = new SeatPose(0f, 0f, 3f, Pi / 2f, 0.1f);

        var flight = SeatCameraMath.Enter(camera, last: null);

        // Yaw a quarter turn left looks along -X, so the camera backs off along +X.
        Assert.AreEqual(9f, flight.X, Eps);
        Assert.AreEqual(0f, flight.Y, Eps);
        Assert.AreEqual(Pi / 2f, flight.Yaw, Eps);
    }

    [TestMethod]
    public void Enter_LaterTimes_KeepTheLastHeightAndPitch()
    {
        var camera = new SeatPose(0f, 0f, 3f, 0f, 0.1f);
        var last = At(height: 63f, pitch: 1.1f, yaw: 2f, x: 500f);

        var flight = SeatCameraMath.Enter(camera, last);

        Assert.AreEqual(63f, flight.Height, Eps);
        Assert.AreEqual(1.1f, flight.Pitch, Eps);
        Assert.AreEqual(0f, flight.Yaw, Eps, "yaw and position come from the camera again");
        Assert.AreEqual(-9f, flight.Y, Eps);
    }

    [TestMethod]
    public void Enter_LastHeightOutsideTheRange_IsClamped()
    {
        var flight = SeatCameraMath.Enter(new SeatPose(0f, 0f, 3f, 0f, 0f), At(height: 500f, pitch: 9f));

        Assert.AreEqual(SeatCameraMath.MaxHeight, flight.Height, Eps);
        Assert.AreEqual(SeatCameraMath.MaxPitch, flight.Pitch, Eps);
    }

    [TestMethod]
    public void Hold_TakesTheCameraFrameAsItIsWithNoBackstep()
    {
        var camera = new SeatPose(10f, 20f, 145f, 0.5f, 0.3f);

        var flight = SeatCameraMath.Hold(camera, groundZ: 100f);

        Assert.AreEqual(new SeatFlight(10f, 20f, 45f, 0.5f, 0.3f), flight);
    }

    [TestMethod]
    public void Hold_ClampsAHeightBelowTheGroundToTheMinimum()
    {
        var flight = SeatCameraMath.Hold(new SeatPose(0f, 0f, 90f, 0f, 3f), groundZ: 100f);

        Assert.AreEqual(SeatCameraMath.MinHeight, flight.Height, Eps);
        Assert.AreEqual(SeatCameraMath.MaxPitch, flight.Pitch, Eps);
    }

    // ---- Blend ----

    [TestMethod]
    public void Smoothstep_PinsItsShape()
    {
        Assert.AreEqual(0f, SeatCameraMath.Smoothstep(0f), Eps);
        Assert.AreEqual(1f, SeatCameraMath.Smoothstep(1f), Eps);
        Assert.AreEqual(0.5f, SeatCameraMath.Smoothstep(0.5f), Eps);
        Assert.AreEqual(0.15625f, SeatCameraMath.Smoothstep(0.25f), Eps);
        Assert.AreEqual(0f, SeatCameraMath.Smoothstep(-3f), Eps);
        Assert.AreEqual(1f, SeatCameraMath.Smoothstep(3f), Eps);
    }

    [TestMethod]
    public void BlendFraction_RunsToOneOverTheBlendSeconds()
    {
        Assert.AreEqual(0f, SeatCameraMath.BlendFraction(0f), Eps);
        Assert.AreEqual(0.5f, SeatCameraMath.BlendFraction(0.35f), Eps);
        Assert.AreEqual(1f, SeatCameraMath.BlendFraction(0.7f), Eps);
        Assert.AreEqual(1f, SeatCameraMath.BlendFraction(5f), Eps);
        Assert.AreEqual(0f, SeatCameraMath.BlendFraction(-1f), Eps);
    }

    [TestMethod]
    public void Blend_EndsAreTheTwoPoses()
    {
        var from = new SeatPose(0f, 0f, 10f, 0.2f, 0.1f);
        var to = new SeatPose(100f, 50f, 40f, 0.6f, 0.9f);

        Assert.AreEqual(from, SeatCameraMath.Blend(from, to, 0f));
        Assert.AreEqual(to, SeatCameraMath.Blend(from, to, 1f));
    }

    [TestMethod]
    public void Blend_Midpoint_IsHalfwayInPositionAndAngles()
    {
        var from = new SeatPose(0f, 0f, 10f, 0.2f, 0.1f);
        var to = new SeatPose(100f, 50f, 40f, 0.6f, 0.9f);

        var mid = SeatCameraMath.Blend(from, to, 0.5f);

        Assert.AreEqual(new SeatPose(50f, 25f, 25f, 0.4f, 0.5f).X, mid.X, Eps);
        Assert.AreEqual(25f, mid.Y, Eps);
        Assert.AreEqual(25f, mid.Z, Eps);
        Assert.AreEqual(0.4f, mid.Yaw, Eps);
        Assert.AreEqual(0.5f, mid.Pitch, Eps);
    }

    [TestMethod]
    public void Blend_QuarterWay_UsesTheSmoothstep()
    {
        var from = new SeatPose(0f, 0f, 0f, 0f, 0f);
        var to = new SeatPose(100f, 0f, 0f, 0f, 0f);

        Assert.AreEqual(15.625f, SeatCameraMath.Blend(from, to, 0.25f).X, Eps);
    }

    [TestMethod]
    public void Blend_YawTakesTheShortestArcAcrossThePiSeam()
    {
        var from = new SeatPose(0f, 0f, 0f, 3.0f, 0f);
        var to = new SeatPose(0f, 0f, 0f, -3.0f, 0f);

        var mid = SeatCameraMath.Blend(from, to, 0.5f);

        // The short way round is 0.283 rad, through pi. The long way would pass through yaw 0.
        Assert.AreEqual(Pi, Math.Abs(mid.Yaw), 0.001f);
    }

    [TestMethod]
    public void Blend_YawAcrossTheSeamOtherWay_AlsoTakesTheShortArc()
    {
        var from = new SeatPose(0f, 0f, 0f, -3.0f, 0f);
        var to = new SeatPose(0f, 0f, 0f, 3.0f, 0f);

        var quarter = SeatCameraMath.Blend(from, to, 0.25f);

        // Smoothstep(0.25) = 0.15625 of the 0.283 rad arc, walked from -3.0 away from zero.
        var expected = -3.0f - 0.15625f * (2f * Pi - 6f);
        Assert.AreEqual(expected, quarter.Yaw, 0.001f);
    }

    [TestMethod]
    public void Blend_YawResult_StaysWithinMinusPiAndPi()
    {
        var from = new SeatPose(0f, 0f, 0f, 3.1f, 0f);
        var to = new SeatPose(0f, 0f, 0f, -3.1f, 0f);

        for (var t = 0f; t <= 1.0001f; t += 0.1f)
        {
            var yaw = SeatCameraMath.Blend(from, to, t).Yaw;
            Assert.IsTrue(yaw > -Pi - 0.0001f && yaw <= Pi + 0.0001f, "t=" + t + " yaw=" + yaw);
        }
    }

    // ---- Ground, listener, view angle ----

    [TestMethod]
    public void GroundSmoothing_IsOneMinusExpOfSixDt()
    {
        Assert.AreEqual(1f - (float)Math.Exp(-0.6), SeatCameraMath.GroundSmoothing(0.1f), 1e-5f);
        Assert.AreEqual(0f, SeatCameraMath.GroundSmoothing(0f), 1e-6f);
    }

    [TestMethod]
    public void SmoothGround_MovesPartWayToTheTarget()
    {
        var next = SeatCameraMath.SmoothGround(100f, 110f, 0.1f);

        Assert.AreEqual(100f + 10f * (1f - (float)Math.Exp(-0.6)), next, 1e-4f);
    }

    [TestMethod]
    public void SmoothGround_BadDtOrTarget_KeepsTheCurrentGround()
    {
        Assert.AreEqual(100f, SeatCameraMath.SmoothGround(100f, 110f, float.NaN));
        Assert.AreEqual(100f, SeatCameraMath.SmoothGround(100f, float.NaN, 0.1f));
        Assert.AreEqual(100f, SeatCameraMath.SmoothGround(100f, float.PositiveInfinity, 0.1f));
    }

    // ---- The floor (5.2) ----

    [TestMethod]
    public void AboveGround_PoseBelowTheClearance_IsLiftedToTheRawGroundPlusHalfAMetre()
    {
        var pose = new SeatPose(1f, 2f, 101f, 0.3f, 0.4f);

        var floored = SeatCameraMath.AboveGround(pose, rawGround: 103f);

        Assert.AreEqual(new SeatPose(1f, 2f, 103.5f, 0.3f, 0.4f), floored, "only Z moves");
    }

    [TestMethod]
    public void AboveGround_PoseAlreadyAboveTheClearance_IsReturnedAsItIs()
    {
        var pose = new SeatPose(1f, 2f, 130f, 0.3f, 0.4f);

        Assert.AreEqual(pose, SeatCameraMath.AboveGround(pose, rawGround: 103f));
    }

    [TestMethod]
    public void AboveGround_ExactlyAtTheClearance_IsReturnedAsItIs()
    {
        var pose = new SeatPose(0f, 0f, 103.5f, 0f, 0f);

        Assert.AreEqual(pose, SeatCameraMath.AboveGround(pose, rawGround: 103f));
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(float.NegativeInfinity)]
    public void AboveGround_RawGroundNotFinite_ReturnsThePoseAsItIs(float rawGround)
    {
        var pose = new SeatPose(1f, 2f, -50f, 0.3f, 0.4f);

        Assert.AreEqual(pose, SeatCameraMath.AboveGround(pose, rawGround));
    }

    // ---- The height limit (5.4) ----

    [DataTestMethod]
    [DataRow(0f)]
    [DataRow(-1f)]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    public void HasHeightLimit_NoTagOrNoPositiveZ_IsFalse(float limit)
    {
        Assert.IsFalse(SeatCameraMath.HasHeightLimit(limit));
    }

    [TestMethod]
    public void HasHeightLimit_APositiveZ_IsTrue()
    {
        Assert.IsTrue(SeatCameraMath.HasHeightLimit(0.01f));
        Assert.IsTrue(SeatCameraMath.HasHeightLimit(250f));
    }

    [TestMethod]
    public void CapHeight_PoseAboveTheLimit_IsLoweredToIt_AndOnlyZMoves()
    {
        var pose = new SeatPose(1f, 2f, 140f, 0.3f, 0.4f);

        Assert.AreEqual(new SeatPose(1f, 2f, 120f, 0.3f, 0.4f), SeatCameraMath.CapHeight(pose, limit: 120f));
    }

    [TestMethod]
    public void CapHeight_PoseUnderTheLimit_IsReturnedAsItIs()
    {
        var pose = new SeatPose(1f, 2f, 100f, 0.3f, 0.4f);

        Assert.AreEqual(pose, SeatCameraMath.CapHeight(pose, limit: 120f));
    }

    [DataTestMethod]
    [DataRow(0f)]
    [DataRow(-5f)]
    [DataRow(float.NaN)]
    public void CapHeight_NoLimit_ReturnsThePoseAsItIs(float limit)
    {
        var pose = new SeatPose(1f, 2f, 140f, 0.3f, 0.4f);

        Assert.AreEqual(pose, SeatCameraMath.CapHeight(pose, limit));
    }

    [TestMethod]
    public void CapFlight_HeightAboveTheRoom_IsLoweredToLimitMinusGround()
    {
        var flight = new SeatFlight(1f, 2f, 60f, 0.3f, 0.4f);

        var capped = SeatCameraMath.CapFlight(flight, limit: 130f, ground: 100f);

        Assert.AreEqual(new SeatFlight(1f, 2f, 30f, 0.3f, 0.4f), capped, "only the height moves");
    }

    [TestMethod]
    public void CapFlight_HeightUnderTheRoom_IsReturnedAsItIs()
    {
        var flight = new SeatFlight(1f, 2f, 10f, 0.3f, 0.4f);

        Assert.AreEqual(flight, SeatCameraMath.CapFlight(flight, limit: 130f, ground: 100f));
    }

    [TestMethod]
    public void CapFlight_LimitBelowTheGround_FloorsTheHeightAtMinimumHeight()
    {
        var flight = new SeatFlight(1f, 2f, 40f, 0.3f, 0.4f);

        var capped = SeatCameraMath.CapFlight(flight, limit: 50f, ground: 100f);

        Assert.AreEqual(SeatCameraMath.MinHeight, capped.Height, Eps);
    }

    [TestMethod]
    public void CapFlight_NoLimit_ReturnsTheFlightAsItIs()
    {
        var flight = new SeatFlight(1f, 2f, 90f, 0.3f, 0.4f);

        Assert.AreEqual(flight, SeatCameraMath.CapFlight(flight, limit: 0f, ground: 100f));
    }

    [TestMethod]
    public void ListenerHeight_StaysAtMostSixteenMetresAboveTheGround()
    {
        Assert.AreEqual(116f, SeatCameraMath.ListenerHeight(cameraZ: 200f, groundZ: 100f), Eps);
        Assert.AreEqual(105f, SeatCameraMath.ListenerHeight(cameraZ: 105f, groundZ: 100f), Eps);
    }

    [TestMethod]
    public void ViewAngle_IsFlooredAtFiftyDegrees()
    {
        Assert.AreEqual(50f, SeatCameraMath.ViewAngle(37f));
        Assert.AreEqual(65f, SeatCameraMath.ViewAngle(65f));
        Assert.AreEqual(50f, SeatCameraMath.ViewAngle(float.NaN));
    }

    [TestMethod]
    public void ZoomFactor_IsSixtyFiveOverTheViewAngle() =>
        Assert.AreEqual(1.3f, SeatCameraMath.ZoomFactor(50f), Eps);

    // ---- Reading the engine's input ----

    [TestMethod]
    public void DeadZone_ZeroesAnAxisBelowTwoTenths()
    {
        Assert.AreEqual(0f, SeatCameraMath.DeadZone(0.19f));
        Assert.AreEqual(0f, SeatCameraMath.DeadZone(-0.19f));
        Assert.AreEqual(0.2f, SeatCameraMath.DeadZone(0.2f));
        Assert.AreEqual(-0.7f, SeatCameraMath.DeadZone(-0.7f));
        Assert.AreEqual(0f, SeatCameraMath.DeadZone(float.NaN));
    }

    [TestMethod]
    public void Look_MouseRight_TurnsTheViewRight()
    {
        var (yaw, _) = SeatCameraMath.Look(moveX: 10f, moveY: 0f, stickX: 0f, stickY: 0f, dt: Dt, sensitivity: 1f, invertMouse: false, mouseActive: true);

        Assert.AreEqual(-10f, yaw, Eps, "a positive yaw turns left, so rolling the mouse right is negative");
    }

    [TestMethod]
    public void Look_MouseDown_LooksDown_AndInvertFlipsIt()
    {
        var (_, plain) = SeatCameraMath.Look(0f, 10f, 0f, 0f, Dt, 1f, invertMouse: false, mouseActive: true);
        var (_, inverted) = SeatCameraMath.Look(0f, 10f, 0f, 0f, Dt, 1f, invertMouse: true, mouseActive: true);

        Assert.AreEqual(10f, plain, Eps, "pitch is the angle below the horizon");
        Assert.AreEqual(-10f, inverted, Eps);
    }

    [TestMethod]
    public void Look_ScalesByTheMouseSensitivity()
    {
        var (yaw, pitch) = SeatCameraMath.Look(10f, 10f, 0f, 0f, Dt, 2.5f, false, true);

        Assert.AreEqual(-25f, yaw, Eps);
        Assert.AreEqual(25f, pitch, Eps);
    }

    [TestMethod]
    public void Look_WithAnActiveMouse_IgnoresTheSticks()
    {
        var (yaw, pitch) = SeatCameraMath.Look(0f, 0f, 1f, 1f, Dt, 1f, false, mouseActive: true);

        Assert.AreEqual(0f, yaw, Eps);
        Assert.AreEqual(0f, pitch, Eps);
    }

    [TestMethod]
    public void Look_WithNoActiveMouse_AddsTheSticksScaledByDtOverNineTenThousandths()
    {
        var (yaw, pitch) = SeatCameraMath.Look(0f, 0f, 1f, 1f, 0.009f, 1f, false, mouseActive: false);

        // 0.009 / 0.0009 = 10 units. Stick right turns right and stick up looks up, the same signs as the mouse.
        Assert.AreEqual(-10f, yaw, Eps);
        Assert.AreEqual(-10f, pitch, Eps);
    }

    [TestMethod]
    public void Look_NonFiniteInput_GivesZero()
    {
        var (yaw, pitch) = SeatCameraMath.Look(float.NaN, float.PositiveInfinity, 0f, 0f, Dt, 1f, false, true);

        Assert.AreEqual(0f, yaw);
        Assert.AreEqual(0f, pitch);
    }

    [TestMethod]
    public void PoseFromLook_HorizontalLookAlongPlusY_IsYawZeroPitchZero()
    {
        var pose = SeatCameraMath.PoseFromLook(1f, 2f, 3f, 0f, 1f, 0f);

        Assert.AreEqual(new SeatPose(1f, 2f, 3f, 0f, 0f).X, pose.X);
        Assert.AreEqual(0f, pose.Yaw, Eps);
        Assert.AreEqual(0f, pose.Pitch, Eps);
    }

    [TestMethod]
    public void PoseFromLook_LookingLeftAndDown_GivesPositiveYawAndPitch()
    {
        // Along -X and down at 45 degrees.
        var pose = SeatCameraMath.PoseFromLook(0f, 0f, 0f, -0.7071f, 0f, -0.7071f);

        Assert.AreEqual(Pi / 2f, pose.Yaw, 0.001f);
        Assert.AreEqual(Pi / 4f, pose.Pitch, 0.001f);
    }

    [TestMethod]
    public void PoseFromLook_StraightDown_KeepsYawZero()
    {
        var pose = SeatCameraMath.PoseFromLook(0f, 0f, 0f, 0f, 0f, -1f);

        Assert.AreEqual(0f, pose.Yaw, Eps);
        Assert.AreEqual(Pi / 2f, pose.Pitch, 0.001f);
    }

    [TestMethod]
    public void IsFinite_RejectsAnyNonFiniteMember()
    {
        Assert.IsTrue(SeatCameraMath.IsFinite(new SeatPose(1f, 2f, 3f, 0.1f, 0.2f)));
        Assert.IsFalse(SeatCameraMath.IsFinite(new SeatPose(float.NaN, 2f, 3f, 0.1f, 0.2f)));
        Assert.IsFalse(SeatCameraMath.IsFinite(new SeatPose(1f, 2f, float.PositiveInfinity, 0.1f, 0.2f)));
        Assert.IsFalse(SeatCameraMath.IsFinite(new SeatPose(1f, 2f, 3f, 0.1f, float.NegativeInfinity)));
    }
}
