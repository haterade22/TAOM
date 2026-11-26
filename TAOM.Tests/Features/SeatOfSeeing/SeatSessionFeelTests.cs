using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Client.Features.SeatOfSeeing;
using TAOM.Core.Logging;
using TAOM.Features.SeatOfSeeing;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Tests.Features.SeatOfSeeing;

/// <summary>The Seat's camera feel in the session: the smooth wheel zoom, the edge scroll, the pan speed and the middle button's turn around a pivot.</summary>
[TestClass]
public class SeatSessionFeelTests
{
    private const float Frame = 1f / 60f;
    private static readonly SeatPermissions Allowed = new(Camera: true, FightOn: true, Orders: true, Time: true, TakeSoldier: true);

    private FakeSeatScreen _screen = null!;
    private FakeSeatSettings _settings = null!;
    private SeatSession _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        SeatPresence.Set(false);
        _screen = new FakeSeatScreen();
        _settings = new FakeSeatSettings();
        _sut = new SeatSession(_screen, new FakeSeatHero(), _settings, () => Allowed, new RecordingOutcomeLog(), Substitute.For<IModLogger>(), "9");
    }

    [TestCleanup]
    public void Cleanup() => SeatPresence.Set(false);

    private SeatPose Last => _screen.Frames.Last().Pose;

    private float HeightNow => Last.Z - _screen.Ground;

    private void Seated()
    {
        _sut.OnScreenTick(true);
        for (var i = 0; i < 40 && _sut.State != SeatState.Seated; i++)
            _sut.OnFrame(0.1f);
        Assert.AreEqual(SeatState.Seated, _sut.State);
    }

    private void Frames(int count)
    {
        for (var i = 0; i < count; i++)
            _sut.OnFrame(Frame);
    }

    private void ShowCursor()
    {
        _screen.PanelOpen = true;
        _sut.OnScreenTick(false);
        Assert.IsTrue(_screen.CursorWrites.Last(), "the panel is open, so the cursor shows");
    }

    private static SeatInput Wheel(float notches) => new(0f, 0f, 0f, notches, 0f, 0f, false);

    // ---- smooth wheel zoom ----

    [TestMethod]
    public void Wheel_OneNotch_EasesTheHeightDownToThe088Share_NeverRisingOnTheWay()
    {
        Seated();
        Frames(1);
        var start = HeightNow;

        _screen.Input = Wheel(1f);
        Frames(1);
        _screen.Input = default;
        var first = HeightNow;
        var heights = Enumerable.Range(0, 30).Select(_ =>
        {
            Frames(1);
            return HeightNow;
        }).ToList();

        Assert.IsTrue(first < start && first > start * 0.88f + 0.5f, "the first frame covers only part of the way: " + first);
        Assert.AreEqual(start * 0.88f, heights.Last(), start * 0.01f);
        for (var i = 1; i < heights.Count; i++)
            Assert.IsTrue(heights[i] <= heights[i - 1] + 0.001f, "the height rose at frame " + i);
    }

    [TestMethod]
    public void Wheel_ANotchOnTopOfAnother_StacksOnTheTarget_NotOnTheHeightReachedSoFar()
    {
        Seated();
        Frames(1);
        var start = HeightNow;

        _screen.Input = Wheel(1f);
        Frames(1);
        Frames(1);
        _screen.Input = default;
        Frames(60);

        Assert.AreEqual(start * 0.88f * 0.88f, HeightNow, start * 0.01f);
    }

    [TestMethod]
    public void Wheel_UnderAHeightLimit_TheTargetStopsAtTheCap_SoTheNextNotchDownActsAtOnce()
    {
        _screen.HeightLimit = 130f;
        Seated();
        Frames(1);

        _screen.Input = Wheel(-12f);
        Frames(1);
        _screen.Input = default;
        Frames(120);
        var capped = HeightNow;
        _screen.Input = Wheel(1f);
        Frames(1);
        _screen.Input = default;
        Frames(60);

        Assert.AreEqual(30f, capped, 0.2f, "the limit 130 over the ground 100 leaves 30 m");
        Assert.AreEqual(30f * 0.88f, HeightNow, 0.3f, "a target wound up past the cap would have taken many notches to leave it");
    }

    [TestMethod]
    public void Lift_WhileTheWheelHasATarget_MovesTheHeightAndTheTargetFollows()
    {
        Seated();
        Frames(1);
        _screen.Input = Wheel(1f);
        Frames(1);
        _screen.Input = new SeatInput(0f, 0f, 1f, 0f, 0f, 0f, false);
        Frames(30);
        _screen.Input = default;
        var lifted = HeightNow;
        Frames(60);

        Assert.AreEqual(lifted, HeightNow, 0.05f, "the lift key's height stays: the old target does not pull it back");
    }

    [TestMethod]
    public void Wheel_TheNextEntryStartsFromTheFlightsHeight_NotAnOldTarget()
    {
        Seated();
        Frames(1);
        _screen.Input = Wheel(3f);
        Frames(1);
        _screen.Input = default;
        Frames(2);
        _sut.OnScreenTick(true);
        Assert.AreEqual(SeatState.Off, _sut.State);

        _sut.OnScreenTick(true);
        for (var i = 0; i < 40 && _sut.State != SeatState.Seated; i++)
            _sut.OnFrame(0.1f);
        var entered = HeightNow;
        Frames(30);

        Assert.AreEqual(entered, HeightNow, 0.2f, "the view holds where it entered");
    }

    // ---- pan speed ----

    [TestMethod]
    public void PanSpeed_MultipliesTheKeyFlight()
    {
        Seated();
        _screen.Input = new SeatInput(0f, 1f, 0f, 0f, 0f, 0f, false);
        Frames(1);
        var from = Last;
        Frames(30);
        var normal = Last.Y - from.Y;

        _settings.PanSpeed = 2f;
        var fromFast = Last;
        Frames(30);
        var fast = Last.Y - fromFast.Y;

        Assert.AreEqual(normal * 2f, fast, normal * 0.02f);
    }

    // ---- edge scroll ----

    [TestMethod]
    public void EdgeScroll_WithTheCursorShown_FliesTheViewTheWayThePointerPushes()
    {
        Seated();
        ShowCursor();
        Frames(1);
        var from = Last;

        _screen.Edge = (1f, 0f);
        Frames(30);

        Assert.IsTrue(Last.X - from.X > 3f, "the view flew to the right of its heading: " + (Last.X - from.X));
        Assert.AreEqual(from.Y, Last.Y, 0.001f);
    }

    [TestMethod]
    public void EdgeScroll_AddsToTheKeys_AndTheSumStopsAtOne()
    {
        Seated();
        ShowCursor();
        _screen.Input = new SeatInput(0f, 1f, 0f, 0f, 0f, 0f, false);
        Frames(1);
        var from = Last;
        _screen.Edge = (0f, 1f);
        Frames(30);
        var withEdge = Last.Y - from.Y;

        _screen.Edge = (0f, 0f);
        var fromKeys = Last;
        Frames(30);
        var keysOnly = Last.Y - fromKeys.Y;

        Assert.AreEqual(keysOnly, withEdge, keysOnly * 0.02f, "forward by key and by edge together is no faster than forward by key");
    }

    [TestMethod]
    public void EdgeScroll_FliesAtThePanSpeedSetting()
    {
        Seated();
        ShowCursor();
        _screen.Edge = (1f, 0f);
        Frames(1);
        var from = Last;
        Frames(30);
        var normal = Last.X - from.X;

        _settings.PanSpeed = 0.5f;
        var fromSlow = Last;
        Frames(30);

        Assert.AreEqual(normal * 0.5f, Last.X - fromSlow.X, normal * 0.02f);
    }

    [TestMethod]
    public void EdgeScroll_WithTheSettingOff_DoesNotAskThePointer()
    {
        Seated();
        ShowCursor();
        _settings.EdgeScroll = false;
        _screen.Edge = (1f, 0f);
        var from = Last;

        Frames(30);

        Assert.AreEqual(from.X, Last.X, 0.001f);
        Assert.AreEqual(0, _screen.EdgeReads);
    }

    [TestMethod]
    public void EdgeScroll_WithTheCursorHidden_DoesNotAskThePointer()
    {
        Seated();
        _screen.Edge = (1f, 0f);
        Frames(1);
        var from = Last;

        Frames(30);

        Assert.AreEqual(from.X, Last.X, 0.001f);
        Assert.AreEqual(0, _screen.EdgeReads, "no panel, no cursor, no scroll");
    }

    [TestMethod]
    public void EdgeScroll_WhileTheMiddleButtonLooks_DoesNotAskThePointer()
    {
        Seated();
        ShowCursor();
        _screen.Look = true;
        _screen.Edge = (1f, 0f);
        var from = Last;

        Frames(30);

        Assert.AreEqual(from.X, Last.X, 0.5f, "only the turn moves the view, and its pivot is the view's own");
        Assert.AreEqual(0, _screen.EdgeReads);
    }

    [TestMethod]
    public void EdgeScroll_WithAFieldTrialInput_IsNotAskedEither()
    {
        Seated();
        ShowCursor();
        _screen.Edge = (1f, 0f);

        for (var i = 0; i < 10; i++)
            _sut.OnFrame(Frame, default(SeatInput));

        Assert.AreEqual(0, _screen.EdgeReads, "a trial input replaces the whole flight input");
    }

    // ---- the middle button turns the view around a pivot ----

    private SeatPivotFind CentreOnTheGround() => SeatPivot.Find(Last, _screen.Ground);

    [TestMethod]
    public void Rotate_HeldWithAPivot_SwingsTheCameraAndKeepsTheCentreOnTheSamePoint()
    {
        Seated();
        Frames(2);
        var before = CentreOnTheGround();
        var from = Last;
        Assert.IsTrue(before.Found);

        _screen.Look = true;
        _screen.Input = new SeatInput(0f, 0f, 0f, 0f, 4f, 0f, false);
        Frames(60);

        var after = CentreOnTheGround();
        Assert.IsTrue(after.Found);
        Assert.AreEqual(before.X, after.X, 0.1f);
        Assert.AreEqual(before.Y, after.Y, 0.1f);
        Assert.IsTrue(Math.Abs(Last.X - from.X) + Math.Abs(Last.Y - from.Y) > 3f, "the camera moved round the point, it did not turn in place");
        Assert.AreNotEqual(from.Yaw, Last.Yaw);
    }

    [TestMethod]
    public void Rotate_LookDown_OrbitsThePitch_WithTheCentreHeld()
    {
        Seated();
        Frames(2);
        var before = CentreOnTheGround();

        _screen.Look = true;
        _screen.Input = new SeatInput(0f, 0f, 0f, 0f, 0f, 40f, false);
        Frames(20);

        var after = CentreOnTheGround();
        Assert.IsTrue(Last.Pitch > 0.6f + 0.1f, "the pitch grew");
        Assert.AreEqual(before.X, after.X, 0.1f);
        Assert.AreEqual(before.Y, after.Y, 0.1f);
    }

    [TestMethod]
    public void Rotate_Released_HandsBackToTheFlightWithNoJump_AndTheViewKeepsFlying()
    {
        Seated();
        Frames(2);
        _screen.Look = true;
        _screen.Input = new SeatInput(0f, 0f, 0f, 0f, 4f, 0f, false);
        Frames(30);
        var held = Last;

        _screen.Look = false;
        _screen.Input = default;
        Frames(1);
        var released = Last;

        Assert.AreEqual(held.X, released.X, 0.05f);
        Assert.AreEqual(held.Y, released.Y, 0.05f);
        Assert.AreEqual(held.Z, released.Z, 0.05f);
        Assert.AreEqual(held.Yaw, released.Yaw, 0.001f);

        _screen.Input = new SeatInput(0f, 1f, 0f, 0f, 0f, 0f, false);
        Frames(30);
        Assert.IsTrue(Math.Abs(Last.X - released.X) + Math.Abs(Last.Y - released.Y) > 3f, "the key flight carries on from the new place");
    }

    [TestMethod]
    public void Rotate_WithAFlatView_TurnsInPlace_AsItAlwaysDid()
    {
        Seated();
        _screen.Input = new SeatInput(0f, 0f, 0f, 0f, 0f, -150f, false);
        Frames(1);
        _screen.Input = default;
        Frames(1);
        Assert.IsTrue(Last.Pitch < SeatPivot.MinPitch, "the view looks flat: " + Last.Pitch);
        var from = Last;

        _screen.Look = true;
        _screen.Input = new SeatInput(0f, 0f, 0f, 0f, 4f, 0f, false);
        Frames(30);

        Assert.AreEqual(from.X, Last.X, 0.001f);
        Assert.AreEqual(from.Y, Last.Y, 0.001f);
        Assert.AreNotEqual(from.Yaw, Last.Yaw, "the view turned");
    }

    [TestMethod]
    public void Rotate_TheWheelDuringTheTurn_DoesNotMoveTheHeight()
    {
        Seated();
        Frames(2);
        _screen.Look = true;
        _screen.Input = new SeatInput(0f, 0f, 0f, 2f, 1f, 0f, false);
        var from = Last;
        Frames(30);

        Assert.AreEqual(from.Z, Last.Z, 0.5f, "the height changes with the pitch only, not with the wheel during a turn");
    }

    [TestMethod]
    public void Rotate_WhileKeepingToASoldier_IsLeftToTheKeepOrbit()
    {
        Seated();
        Frames(2);
        _sut.PressKeep();
        _sut.OnMissionTick();
        Assert.AreEqual(SeatKeepKind.Soldier, _sut.Keep);

        _screen.Look = true;
        _screen.Input = new SeatInput(0f, 0f, 0f, 0f, 4f, 0f, false);
        Frames(10);

        Assert.AreEqual(SeatKeepKind.Soldier, _sut.Keep, "a keep orbit stays as it was (S3)");
    }
}
