using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Client.Features.SeatOfSeeing;
using TAOM.Core.Logging;
using TAOM.Features.SeatOfSeeing;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Tests.Features.SeatOfSeeing;

/// <summary>
/// The Seat's state machine (build-spec 5.3 to 5.7) driven against a scripted engine: Off, Rising and Seated, the entry checks,
/// the blend, the hand-back and the fault latch.
/// </summary>
[TestClass]
public class SeatSessionTests
{
    private const float Eps = 0.001f;
    private static readonly SeatPermissions Allowed = new(Camera: true, FightOn: true, Orders: true, Time: true, TakeSoldier: true);
    private static readonly SeatPermissions Denied = default;

    private FakeSeatScreen _screen = null!;
    private FakeSeatHero _hero = null!;
    private FakeSeatSettings _settings = null!;
    private Func<SeatPermissions> _permissions = null!;
    private RecordingOutcomeLog _log = null!;
    private IModLogger _logger = null!;
    private SeatSession _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        SeatPresence.Set(false);
        _screen = new FakeSeatScreen();
        _hero = new FakeSeatHero();
        _settings = new FakeSeatSettings();
        _permissions = () => Allowed;
        _log = new RecordingOutcomeLog();
        _logger = Substitute.For<IModLogger>();
        _sut = new SeatSession(_screen, _hero, _settings, () => _permissions(), _log, _logger, "7");
    }

    [TestCleanup]
    public void Cleanup() => SeatPresence.Set(false);

    private void Tick(bool key = false, bool enabled = true, bool beginInSeat = false, SeatPermissions? permissions = null)
    {
        var perms = permissions ?? Allowed;
        _permissions = () => perms;
        _settings.Enabled = enabled;
        _settings.BeginBattlesInSeat = beginInSeat;
        _sut.OnScreenTick(key);
    }

    private void Enter() => Tick(key: true);

    private void Finish(int frames = 20)
    {
        for (var i = 0; i < frames && _sut.State != SeatState.Seated; i++)
            _sut.OnFrame(0.1f);
    }

    private void Seated()
    {
        Enter();
        Finish();
        Assert.AreEqual(SeatState.Seated, _sut.State);
    }

    // ---- Entry ----

    [TestMethod]
    public void Key_WhileOff_StartsRising_AndStandsTheHeroFast()
    {
        Enter();

        Assert.AreEqual(SeatState.Rising, _sut.State);
        CollectionAssert.AreEqual(new[] { false }, _screen.ControllerWrites);
        Assert.AreEqual(0, _hero.StandFasts, "stand fast is written from the mission tick, never from the key's screen tick");
        Assert.IsTrue(SeatPresence.IsSeated);
    }

    [TestMethod]
    public void Key_WhileOff_WritesTheEnteredCheck()
    {
        Enter();

        Assert.AreEqual(1, _log.Count("pass", SeatChecks.EnterId));
        Assert.AreEqual("7", _log.Lines.Single(l => l.Id == SeatChecks.EnterId).Key);
    }

    [TestMethod]
    public void NoKey_WhileOff_DoesNothing()
    {
        Tick();
        Tick();

        Assert.AreEqual(SeatState.Off, _sut.State);
        Assert.AreEqual(0, _screen.ControllerWrites.Count);
        Assert.AreEqual(0, _log.Lines.Count);
    }

    [TestMethod]
    public void Key_WithoutTheCameraPermission_DoesNothing()
    {
        Tick(key: true, permissions: Denied);

        Assert.AreEqual(SeatState.Off, _sut.State);
        Assert.AreEqual(0, _screen.ControllerWrites.Count);
    }

    [TestMethod]
    public void Key_WithTheSettingOff_DoesNothing()
    {
        Tick(key: true, enabled: false);

        Assert.AreEqual(SeatState.Off, _sut.State);
    }

    [TestMethod]
    public void Key_AskedForPermissionsOnlyWhenItMatters()
    {
        var asked = 0;
        _permissions = () =>
        {
            asked++;
            return Allowed;
        };

        _sut.OnScreenTick(false);

        Assert.AreEqual(0, asked, "an idle frame costs the key test and nothing else");
    }

    [TestMethod]
    public void NoKeyAndNoStoredEdge_ReadsNoSetting()
    {
        _sut.OnScreenTick(false);
        _sut.OnScreenTick(false);

        Assert.AreEqual(0, _settings.Reads);
    }

    [TestMethod]
    public void Key_DuringDeployment_IsRefused()
    {
        _screen.Facts = _screen.Facts with { Deployment = true, BattleMode = false };

        Enter();

        Assert.AreEqual(SeatState.Off, _sut.State);
        Assert.AreEqual(0, _screen.ControllerWrites.Count);
    }

    [TestMethod]
    public void Key_InAConversationBarterOrCutScene_IsRefused()
    {
        _screen.Facts = _screen.Facts with { Interlude = true, BattleMode = false };

        Enter();

        Assert.AreEqual(SeatState.Off, _sut.State);
    }

    [TestMethod]
    public void Key_DuringPhotoMode_IsRefused()
    {
        _screen.Facts = _screen.Facts with { PhotoMode = true };

        Enter();

        Assert.AreEqual(SeatState.Off, _sut.State);
    }

    [TestMethod]
    public void Key_AfterTheMissionEnded_IsRefused()
    {
        _screen.Facts = _screen.Facts with { MissionEnded = true };

        Enter();

        Assert.AreEqual(SeatState.Off, _sut.State);
    }

    [TestMethod]
    public void Key_WithACustomCameraView_IsRefusedAndSaysItIsAnotherView()
    {
        _screen.Facts = _screen.Facts with { CustomCamera = true };

        Enter();

        Assert.AreEqual(SeatState.Off, _sut.State);
        CollectionAssert.AreEqual(new[] { SeatMessage.OtherView }, _screen.Messages);
        Assert.AreEqual(0, _screen.ControllerWrites.Count);
    }

    [TestMethod]
    public void Key_WhenTheCameraCannotBeRead_StaysOff()
    {
        _screen.CameraReadable = false;

        Enter();

        Assert.AreEqual(SeatState.Off, _sut.State);
        Assert.AreEqual(0, _screen.ControllerWrites.Count);
    }

    [TestMethod]
    public void Key_WhenTheCameraFrameIsNotFinite_StaysOff()
    {
        _screen.Camera = new SeatPose(float.NaN, 0f, 0f, 0f, 0f);

        Enter();

        Assert.AreEqual(SeatState.Off, _sut.State);
        Assert.AreEqual(0, _screen.ControllerWrites.Count);
    }

    [TestMethod]
    public void FirstEntry_ShowsOneMessage_AndLaterEntriesShowNone()
    {
        Seated();
        Tick(key: true);
        Tick(key: true);

        CollectionAssert.AreEqual(new[] { SeatMessage.FirstEntry }, _screen.Messages);
    }

    // ---- The blend ----

    [TestMethod]
    public void Rising_FirstFrame_StartsAtTheVanillaCamera()
    {
        Enter();

        _sut.OnFrame(0.0f);

        Assert.AreEqual(_screen.Camera.X, _screen.Frames[0].Pose.X, Eps);
        Assert.AreEqual(_screen.Camera.Z, _screen.Frames[0].Pose.Z, Eps);
    }

    [TestMethod]
    public void Rising_AfterTheBlendSeconds_BecomesSeatedAtTheEntryPose()
    {
        Enter();

        Assert.IsTrue(_sut.OnFrame(0.35f));
        Assert.AreEqual(SeatState.Rising, _sut.State);
        Assert.IsTrue(_sut.OnFrame(0.36f));

        Assert.AreEqual(SeatState.Seated, _sut.State);
        var last = _screen.Frames.Last().Pose;
        Assert.AreEqual(100f, last.X, Eps);
        Assert.AreEqual(200f - 9f, last.Y, Eps);
        Assert.AreEqual(100f + 22f, last.Z, Eps);
        Assert.AreEqual(0.60f, last.Pitch, Eps);
    }

    [TestMethod]
    public void Rising_ReadsNoFlightInput()
    {
        _screen.Input = new SeatInput(1f, 1f, 1f, 1f, 5f, 5f, false);
        Enter();

        _sut.OnFrame(0.1f);
        _sut.OnFrame(0.1f);

        Assert.AreEqual(0, _screen.InputReads, "input does nothing during the blend");
    }

    [TestMethod]
    public void Frame_WhileRising_OwnsTheCamera()
    {
        Enter();

        Assert.IsTrue(_sut.OnFrame(0.1f));
    }

    [TestMethod]
    public void Frame_WhileOff_DoesNotOwnTheCamera()
    {
        Assert.IsFalse(_sut.OnFrame(0.1f));
        Assert.AreEqual(0, _screen.Frames.Count);
    }

    [TestMethod]
    public void Frame_UsesTheViewAngleTheCameraHadAtEntry_FlooredAtFifty()
    {
        _screen.ViewAngleDegrees = 37f;
        Enter();

        _sut.OnFrame(0.1f);

        Assert.AreEqual(50f, _screen.Frames[0].ViewAngle);
    }

    [TestMethod]
    public void Frame_KeepsTheEntryViewAngleWhenItIsWide()
    {
        _screen.ViewAngleDegrees = 80f;
        Enter();

        _sut.OnFrame(0.1f);

        Assert.AreEqual(80f, _screen.Frames[0].ViewAngle);
    }

    [TestMethod]
    public void Frame_ListenerStaysSixteenMetresAboveTheGround()
    {
        Seated();
        _screen.Input = new SeatInput(0f, 0f, 1f, 0f, 0f, 0f, false);

        for (var i = 0; i < 100; i++)
            _sut.OnFrame(0.1f);

        var frame = _screen.Frames.Last();
        Assert.IsTrue(frame.Pose.Z > 100f + 16f);
        Assert.AreEqual(100f + 16f, frame.ListenerZ, Eps);
    }

    // ---- Seated flight ----

    [TestMethod]
    public void Seated_ReadsTheFlightInputOncePerFrame_AndMoves()
    {
        Seated();
        var before = _screen.Frames.Last().Pose;
        _screen.Input = new SeatInput(0f, 1f, 0f, 0f, 0f, 0f, false);
        var reads = _screen.InputReads;

        _sut.OnFrame(0.1f);

        Assert.AreEqual(reads + 1, _screen.InputReads);
        Assert.IsTrue(_screen.Frames.Last().Pose.Y > before.Y);
    }

    [TestMethod]
    public void Seated_BoundaryClampLimitsTheFlight()
    {
        Seated();
        _screen.ClampX = 100.5f;
        _screen.Input = new SeatInput(1f, 0f, 0f, 0f, 0f, 0f, false);

        for (var i = 0; i < 10; i++)
            _sut.OnFrame(0.1f);

        Assert.AreEqual(100.5f, _screen.Frames.Last().Pose.X, Eps);
    }

    [TestMethod]
    public void Seated_GroundFollowIsSmoothed_NotSnapped()
    {
        Seated();
        _screen.Ground = 110f;

        _sut.OnFrame(0.1f);

        var z = _screen.Frames.Last().Pose.Z;
        var smoothed = 100f + 10f * (1f - (float)Math.Exp(-0.6)) + 22f;
        Assert.AreEqual(smoothed, z, 0.01f);
        Assert.IsTrue(z < 110f + 22f);
    }

    [TestMethod]
    public void Seated_SamplesTheGroundUnderTheFlightPosition_NotUnderTheHero()
    {
        _screen.GroundAt = (x, y) => 100f + y * 0.01f;
        Seated();
        _screen.Input = new SeatInput(0f, 1f, 0f, 0f, 0f, 0f, false);

        for (var i = 0; i < 40; i++)
            _sut.OnFrame(0.1f);

        var pose = _screen.Frames.Last().Pose;
        var groundUnder = 100f + pose.Y * 0.01f;
        Assert.AreEqual(22f, pose.Z - groundUnder, 0.1f, "the camera keeps its height above the ground under it, smoothed");
        Assert.IsTrue(groundUnder > 100.5f, "the flight reached the rising hillside");
    }

    [TestMethod]
    public void Seated_NonFiniteDt_LeavesTheFlightWhereItWas()
    {
        Seated();
        _screen.Input = new SeatInput(1f, 1f, 1f, 1f, 9f, 9f, true);
        var before = _screen.Frames.Last().Pose;

        Assert.IsTrue(_sut.OnFrame(float.NaN));

        Assert.AreEqual(before, _screen.Frames.Last().Pose);
    }

    [TestMethod]
    public void Seated_OwnedFrames_CountOnTheHotPathCounter()
    {
        Seated();
        var counter = _log.Counter(SeatChecks.FrameCounterId);
        var before = counter.Passes;

        _sut.OnFrame(0.01f);
        _sut.OnFrame(0.01f);

        Assert.AreEqual(before + 2, counter.Passes);
    }

    [TestMethod]
    public void OwnedFrames_ReachTheThreshold_WritesTheFramesCheckOnce()
    {
        Enter();
        for (var i = 0; i < SeatChecks.FramesThreshold + 20; i++)
            _sut.OnFrame(0.02f);

        Assert.AreEqual(1, _log.Count("pass", SeatChecks.FramesId));
    }

    // ---- The floor (5.2) ----

    private void SinkToTheMinimumHeight()
    {
        _screen.Input = new SeatInput(0f, 0f, -1f, 0f, 0f, 0f, false);
        for (var i = 0; i < 100; i++)
            _sut.OnFrame(0.1f);
        _screen.Input = default;
        _sut.OnFrame(0.1f);
        Assert.AreEqual(100f + SeatCameraMath.MinHeight, _screen.Frames.Last().Pose.Z, 0.01f, "the flight sits at its minimum height");
    }

    [TestMethod]
    public void Floor_GroundJumpsThreeMetresInOneFrameAtMinimumHeight_PoseStaysHalfAMetreAboveTheRawGround()
    {
        Seated();
        SinkToTheMinimumHeight();
        _screen.Ground = 103f;

        _sut.OnFrame(0.1f);

        // The smoothed ground has covered 1.35 m of the 3 m, so smoothing alone would put the view at 102.85, under the hill.
        var z = _screen.Frames.Last().Pose.Z;
        Assert.IsTrue(z >= 103f + SeatCameraMath.GroundClearance - Eps, "z=" + z);
        Assert.AreEqual(103.5f, z, Eps);
    }

    [TestMethod]
    public void Floor_TheSmoothedGroundStillDrivesThePoseWhenItIsTheHigherOne()
    {
        Seated();
        SinkToTheMinimumHeight();
        _screen.Ground = 90f;

        _sut.OnFrame(0.1f);

        // The smoothed ground has only come 4.5 m of the way from 100 to 90, far above the raw ground plus the clearance.
        var smoothed = 100f + (90f - 100f) * (1f - (float)Math.Exp(-0.6));
        Assert.AreEqual(smoothed + SeatCameraMath.MinHeight, _screen.Frames.Last().Pose.Z, 0.01f);
        Assert.IsTrue(_screen.Frames.Last().Pose.Z > 90f + SeatCameraMath.GroundClearance);
    }

    [TestMethod]
    public void Floor_ReadsTheRawGroundUnderThePosesOwnXAndY_OnceASeatedFrame()
    {
        Seated();
        _screen.GroundReads.Clear();

        _sut.OnFrame(0.1f);

        var pose = _screen.Frames.Last().Pose;
        Assert.AreEqual(1, _screen.GroundReads.Count, "only the blend has a second read");
        Assert.AreEqual((pose.X, pose.Y), _screen.GroundReads[0]);
    }

    [TestMethod]
    public void Floor_WhileRising_ReadsTheGroundASecondTimeAtTheBlendedPosesXAndY()
    {
        Enter();
        _screen.GroundReads.Clear();

        _sut.OnFrame(0.35f);

        var pose = _screen.Frames.Last().Pose;
        Assert.AreEqual(2, _screen.GroundReads.Count);
        Assert.AreEqual((pose.X, pose.Y), _screen.GroundReads[1], "the second read is under the pose the screen gets");
        Assert.AreNotEqual(_screen.GroundReads[0], _screen.GroundReads[1], "the flight's point differs from the blended one while rising");
    }

    [TestMethod]
    public void Floor_WhileRising_LiftsAPoseOnAHillThatTheFlightsOwnGroundDoesNotShow()
    {
        // Camera at (100, 200); the flight's entry point is nine metres behind it at y=191. The hill starts at y=194.
        _screen.GroundAt = (x, y) => y > 194f ? 200f : 100f;
        _screen.Camera = new SeatPose(100f, 200f, 203f, 0f, 0.1f);
        Enter();

        _sut.OnFrame(0.35f);

        var pose = _screen.Frames.Last().Pose;
        Assert.IsTrue(pose.Y > 194f, "the blended pose is on the hill");
        Assert.IsTrue(pose.Z >= 200f + SeatCameraMath.GroundClearance - Eps, "z=" + pose.Z);
    }

    [TestMethod]
    public void Floor_RawGroundNotFinite_LeavesThePoseAsTheFlightMakesIt()
    {
        Seated();
        _screen.GroundAt = (x, y) => float.NaN;

        _sut.OnFrame(0.1f);

        Assert.AreEqual(100f + 22f, _screen.Frames.Last().Pose.Z, 0.01f, "the smoothed ground is kept and nothing is floored");
    }

    [TestMethod]
    public void Floor_WhatTheScreenGetsIsWhatTheGlideUses()
    {
        Seated();
        SinkToTheMinimumHeight();
        _screen.Ground = 103f;
        _sut.OnFrame(0.1f);
        var floored = _screen.Frames.Last().Pose;

        Tick(key: true);

        Assert.AreEqual(floored.Yaw, _screen.Looks.Single().Yaw, Eps);
        Assert.AreEqual(floored.Pitch, _screen.Looks.Single().Pitch, Eps);
    }

    // ---- Warg rage (5.5) ----

    private void Rage(bool holds) => _screen.Facts = _screen.Facts with { RageHoldsHero = holds };

    [TestMethod]
    public void Rage_KeyWhileOff_DoesNotEnter_AndSaysWhy()
    {
        Rage(true);

        Enter();

        Assert.AreEqual(SeatState.Off, _sut.State);
        Assert.AreEqual(0, _screen.ControllerWrites.Count);
        Assert.AreEqual(0, _hero.StandFasts);
        CollectionAssert.AreEqual(new[] { SeatMessage.RageHolds }, _screen.Messages);
        Assert.IsFalse(SeatPresence.IsSeated);
    }

    [TestMethod]
    public void Rage_KeyWhileOff_SaysItEachTimeItIsRefused()
    {
        Rage(true);

        Enter();
        Enter();

        Assert.AreEqual(2, _screen.Messages.Count(m => m == SeatMessage.RageHolds));
    }

    [TestMethod]
    public void Rage_AfterItEnds_TheKeyEntersAgain()
    {
        Rage(true);
        Enter();
        Rage(false);

        Enter();

        Assert.AreEqual(SeatState.Rising, _sut.State);
        CollectionAssert.AreEqual(new[] { SeatMessage.RageHolds, SeatMessage.FirstEntry }, _screen.Messages);
    }

    [TestMethod]
    public void Rage_BattleStartEntry_IsRefusedWithNoMessage()
    {
        _screen.Facts = _screen.Facts with { Deployment = false, BattleMode = true, RageHoldsHero = true };
        _sut.OnDeploymentEnded();

        Tick(beginInSeat: true);

        Assert.AreEqual(SeatState.Off, _sut.State);
        Assert.AreEqual(0, _screen.Messages.Count);
        Assert.AreEqual(0, _screen.ControllerWrites.Count);
    }

    [TestMethod]
    public void Rage_StartsWhileSeated_LeavesAtOnce_WithTheGlideInBattleMode()
    {
        Seated();
        Rage(true);

        Tick();

        Assert.AreEqual(SeatState.Off, _sut.State);
        Assert.AreEqual(1, _screen.GlideCalls);
        Assert.IsFalse(SeatPresence.IsSeated);
        Assert.IsFalse(_sut.OnFrame(0.1f));
    }

    [TestMethod]
    public void Rage_StartsWhileRising_LeavesAtOnce()
    {
        Enter();
        _sut.OnFrame(0.1f);
        Rage(true);

        Tick();

        Assert.AreEqual(SeatState.Off, _sut.State);
        Assert.AreEqual(1, _screen.GlideCalls);
    }

    [TestMethod]
    public void Rage_StartsWhileSeatedOutsideBattleMode_LeavesWithNoGlide()
    {
        Seated();
        _screen.Facts = _screen.Facts with { BattleMode = false, RageHoldsHero = true };

        Tick();

        Assert.AreEqual(SeatState.Off, _sut.State);
        Assert.AreEqual(0, _screen.GlideCalls);
    }

    [TestMethod]
    public void Rage_Leaving_StillAsksTheScreenAdapterForTheController_WhichRefusesWhileTheRageHolds()
    {
        Seated();
        Rage(true);

        Tick();

        CollectionAssert.AreEqual(new[] { false, true }, _screen.ControllerWrites);
    }

    [TestMethod]
    public void Rage_StartsWhileSeated_IsNotAFault()
    {
        Seated();
        Rage(true);

        Tick();

        Assert.IsFalse(_sut.Faulted);
        Assert.AreEqual(0, _log.Count("fail", SeatChecks.FaultId));
    }

    // ---- Leaving ----

    [TestMethod]
    public void Key_WhileSeated_LeavesAndHandsTheHeroBack()
    {
        Seated();

        Tick(key: true);

        Assert.AreEqual(SeatState.Off, _sut.State);
        CollectionAssert.AreEqual(new[] { false, true }, _screen.ControllerWrites);
        Assert.IsFalse(SeatPresence.IsSeated);
        Assert.IsFalse(_sut.OnFrame(0.1f));
    }

    [TestMethod]
    public void Key_WhileRising_AlsoLeaves()
    {
        Enter();
        _sut.OnFrame(0.1f);

        Tick(key: true);

        Assert.AreEqual(SeatState.Off, _sut.State);
        CollectionAssert.AreEqual(new[] { false, true }, _screen.ControllerWrites);
    }

    [TestMethod]
    public void Leave_InBattleMode_WritesTheLastLook_ThenBeginsTheGlide()
    {
        Seated();
        var last = _screen.Frames.Last().Pose;

        Tick(key: true);

        Assert.AreEqual(1, _screen.Looks.Count);
        Assert.AreEqual(last.Yaw, _screen.Looks[0].Yaw, Eps);
        Assert.AreEqual(last.Pitch, _screen.Looks[0].Pitch, Eps);
        Assert.AreEqual(1, _screen.GlideCalls);
        var line = _log.Lines.Single(l => l.Id == SeatChecks.GlideId);
        Assert.AreEqual("pass", line.Result);
        StringAssert.Contains(line.Detail, "elevation=set");
    }

    [TestMethod]
    public void Leave_InAnotherMode_WritesTheLook_SnapsToTheHero_WithNoGlide()
    {
        Seated();
        _screen.Facts = _screen.Facts with { BattleMode = false };

        Tick(key: true);

        Assert.AreEqual(1, _screen.Looks.Count, "the look is written on every leave but Finish, in any mode");
        Assert.AreEqual(0, _screen.GlideCalls);
        Assert.AreEqual(0, _log.Lines.Count(l => l.Id == SeatChecks.GlideId));
        Assert.AreEqual(SeatState.Off, _sut.State);
    }

    [TestMethod]
    public void Leave_WhenTheElevationSetterIsMissing_WritesAFailCheck_AndStillLeavesAndGlides()
    {
        Seated();
        _screen.LookWritten = false;

        Tick(key: true);

        Assert.AreEqual(SeatState.Off, _sut.State);
        var line = _log.Lines.Single(l => l.Id == SeatChecks.GlideId && l.Result == "fail");
        StringAssert.Contains(line.Detail, "elevation=missing");
        Assert.AreEqual(1, _screen.GlideCalls, "the glide still runs, from an unrelated elevation");
        Assert.AreEqual(0, _log.Count("pass", SeatChecks.GlideId));
    }

    [TestMethod]
    public void Leave_WithNoAgentToFollow_SkipsTheGlideWithAReason_AndKeepsTheLook()
    {
        Seated();
        _screen.GlideBegins = false;

        Tick(key: true);

        Assert.AreEqual(SeatState.Off, _sut.State);
        Assert.AreEqual(1, _screen.Looks.Count);
        var line = _log.Lines.Single(l => l.Id == SeatChecks.GlideId);
        Assert.AreEqual("skip", line.Result);
        StringAssert.Contains(line.Detail, "reason=no-agent");
        Assert.AreEqual(0, _log.Count("pass", SeatChecks.GlideId));
        Assert.AreEqual(0, _log.Count("fail", SeatChecks.GlideId));
    }

    [TestMethod]
    public void Leave_NoAgentToFollowAndNoSetter_WritesBothLines()
    {
        Seated();
        _screen.GlideBegins = false;
        _screen.LookWritten = false;

        Tick(key: true);

        Assert.AreEqual(1, _log.Count("fail", SeatChecks.GlideId));
        Assert.AreEqual(1, _log.Count("skip", SeatChecks.GlideId));
    }

    [TestMethod]
    public void Leave_WritesTheLeftCheckWithTheFrameCount()
    {
        Seated();
        _sut.OnFrame(0.1f);

        Tick(key: true);

        var line = _log.Lines.Single(l => l.Id == SeatChecks.LeaveId);
        Assert.AreEqual("pass", line.Result);
        StringAssert.Contains(line.Detail, "frames=");
    }

    [TestMethod]
    public void ModeChange_ToConversation_LeavesWithoutAGlide()
    {
        Seated();
        _screen.Facts = _screen.Facts with { Interlude = true, BattleMode = false };

        Tick();

        Assert.AreEqual(SeatState.Off, _sut.State);
        Assert.AreEqual(0, _screen.GlideCalls);
        CollectionAssert.AreEqual(new[] { false, true }, _screen.ControllerWrites);
        Assert.AreEqual(1, _screen.Looks.Count, "the look is written when a conversation takes the mode");
    }

    [TestMethod]
    public void TurningTheSettingOff_WhileSeated_Leaves()
    {
        Seated();

        Tick(enabled: false);

        Assert.AreEqual(SeatState.Off, _sut.State);
    }

    [TestMethod]
    public void MissionEnd_WhileSeated_ReleasesTheHero_WithoutAGlide()
    {
        Seated();

        _sut.Finish();

        Assert.AreEqual(SeatState.Off, _sut.State);
        CollectionAssert.AreEqual(new[] { false, true }, _screen.ControllerWrites);
        Assert.AreEqual(0, _screen.GlideCalls);
        Assert.AreEqual(0, _screen.Looks.Count, "the mission is over: Finish writes no look");
        Assert.IsFalse(SeatPresence.IsSeated);
    }

    [TestMethod]
    public void MissionEnd_WhileOff_TouchesNothing()
    {
        _sut.Finish();

        Assert.AreEqual(0, _screen.ControllerWrites.Count);
    }

    [TestMethod]
    public void ReEntry_KeepsTheLastHeightAndPitch()
    {
        Seated();
        _screen.Input = new SeatInput(0f, 0f, 0f, 3f, 0f, -40f, false);
        _sut.OnFrame(0.1f);
        var flown = _screen.Frames.Last().Pose;
        Tick(key: true);
        _screen.Input = default;
        _screen.Frames.Clear();

        Enter();
        Finish();

        var again = _screen.Frames.Last().Pose;
        Assert.AreEqual(flown.Z - 100f, again.Z - 100f, Eps, "the height above the ground carries over");
        Assert.AreEqual(flown.Pitch, again.Pitch, Eps);
    }

    // ---- Photo mode ----

    [TestMethod]
    public void PhotoMode_WhileSeated_FrameIsNotOwned_AndStateWaits()
    {
        Seated();
        _screen.Facts = _screen.Facts with { PhotoMode = true };

        Assert.IsFalse(_sut.OnFrame(0.1f));
        Tick();

        Assert.AreEqual(SeatState.Seated, _sut.State);
    }

    [TestMethod]
    public void PhotoMode_KeyDoesNothing_AndCarriesOnAfterwards()
    {
        Seated();
        _screen.Facts = _screen.Facts with { PhotoMode = true };

        Tick(key: true);
        Assert.AreEqual(SeatState.Seated, _sut.State);

        _screen.Facts = _screen.Facts with { PhotoMode = false };
        Assert.IsTrue(_sut.OnFrame(0.1f));
    }

    [TestMethod]
    public void CustomCamera_WhileSeated_FrameIsNotOwned()
    {
        Seated();
        _screen.Facts = _screen.Facts with { CustomCamera = true };

        Assert.IsFalse(_sut.OnFrame(0.1f));
    }

    // ---- Begin battles in the Seat ----

    [TestMethod]
    public void DeploymentEnds_WithTheSettingOn_EntersSeatedAtOnceAtTheCurrentFrame()
    {
        _screen.Camera = new SeatPose(10f, 20f, 145f, 0.5f, 0.3f);
        _sut.OnDeploymentEnded();

        Tick(beginInSeat: true);

        Assert.AreEqual(SeatState.Seated, _sut.State);
        Assert.IsTrue(_sut.OnFrame(0.1f));
        var pose = _screen.Frames.Last().Pose;
        Assert.AreEqual(10f, pose.X, Eps);
        Assert.AreEqual(20f, pose.Y, Eps);
        Assert.AreEqual(145f, pose.Z, Eps, "no blend and no backstep");
        Assert.AreEqual(0.5f, pose.Yaw, Eps);
        Assert.AreEqual(0.3f, pose.Pitch, Eps);
    }

    [TestMethod]
    public void DeploymentEnds_WithTheSettingOff_StaysOff_AndTheEdgeIsConsumed()
    {
        _sut.OnDeploymentEnded();

        Tick();
        Assert.AreEqual(SeatState.Off, _sut.State);

        Tick(beginInSeat: true);
        Assert.AreEqual(SeatState.Off, _sut.State, "the setting turned on later in the battle does not enter on an old edge");
    }

    [TestMethod]
    public void DeploymentEnds_WithTheBattleStartSettingOnButTheSeatDisabled_StaysOff()
    {
        _sut.OnDeploymentEnded();

        Tick(enabled: false, beginInSeat: true);

        Assert.AreEqual(SeatState.Off, _sut.State);
    }

    [TestMethod]
    public void DeploymentEnds_StoresOneEdge_AndOneEntryConsumesIt()
    {
        _sut.OnDeploymentEnded();
        _sut.OnDeploymentEnded();
        Tick(beginInSeat: true);
        Assert.AreEqual(SeatState.Seated, _sut.State);

        Tick(key: true);
        Assert.AreEqual(SeatState.Off, _sut.State);
        Tick(beginInSeat: true);

        Assert.AreEqual(SeatState.Off, _sut.State, "the edge was used up by the first entry");
    }

    [TestMethod]
    public void DeploymentEnds_WithoutTheCameraPermission_StaysOff()
    {
        _sut.OnDeploymentEnded();

        Tick(beginInSeat: true, permissions: Denied);

        Assert.AreEqual(SeatState.Off, _sut.State);
        Assert.AreEqual(0, _log.Lines.Count, "a refusal with no key press writes no line");
    }

    [TestMethod]
    public void BeginInSeat_KeyInDeploymentWithSettingOff_ThenSettingOn_DoesNotEnter()
    {
        _screen.Facts = _screen.Facts with { Deployment = true, BattleMode = false };
        Tick(key: true, beginInSeat: false);
        _screen.Facts = _screen.Facts with { Deployment = false, BattleMode = true };

        Tick(beginInSeat: true);

        Assert.AreEqual(SeatState.Off, _sut.State, "the edge comes from the engine's mode change, not from a key pressed in Deployment");
    }

    [TestMethod]
    public void NoKeyAndNoStoredEdge_ReadsNoFacts_EvenWithTheBattleStartSettingOn()
    {
        Tick(beginInSeat: true);
        Tick(beginInSeat: true);
        Tick();

        Assert.AreEqual(0, _screen.FactsReads, "an idle frame costs the key test and nothing else");
    }

    [TestMethod]
    public void BeginInSeat_FiresOnTheDeploymentEdgeOnly()
    {
        Tick(beginInSeat: true);
        Tick(beginInSeat: true);

        Assert.AreEqual(SeatState.Off, _sut.State, "a mission that never deployed is not a deployment ending");
    }

    // ---- The main agent changes while seated ----

    [TestMethod]
    public void Entering_WritesNothingToTheHeroUntilTheMissionTick()
    {
        Enter();

        Assert.AreEqual(0, _hero.StandFasts, "the key arrives in the screen tick, and the hero is written only from the mission tick");

        _sut.OnMissionTick();

        Assert.AreEqual(1, _hero.StandFasts);
    }

    [TestMethod]
    public void MainAgentChanged_WhileSeated_WritesNothingUntilTheMissionTick()
    {
        Seated();
        _sut.OnMissionTick();
        var written = _hero.StandFasts;

        _sut.OnMainAgentChanged();

        Assert.AreEqual(written, _hero.StandFasts, "the handler may run inside a native callback: it only sets a flag");

        _sut.OnMissionTick();

        Assert.AreEqual(written + 1, _hero.StandFasts);
    }

    [TestMethod]
    public void OnMissionTick_WithNothingOwed_WritesNothingMore()
    {
        Seated();
        _sut.OnMissionTick();
        _sut.OnMissionTick();
        _sut.OnMissionTick();

        Assert.AreEqual(1, _hero.StandFasts, "a paused mission still ticks, and the write is done once");
    }

    [TestMethod]
    public void OnMissionTick_AfterLeaving_WritesNothing()
    {
        Seated();
        Tick(key: true);

        _sut.OnMissionTick();

        Assert.AreEqual(0, _hero.StandFasts);
    }

    [TestMethod]
    public void OnMissionTick_WhileOff_WritesNothing_EvenAfterAMainAgentChange()
    {
        _sut.OnMainAgentChanged();

        _sut.OnMissionTick();

        Assert.AreEqual(0, _hero.StandFasts);
    }

    [TestMethod]
    public void OnMissionTick_AfterAFault_WritesNothing()
    {
        Seated();
        _screen.ApplyThrows = new InvalidOperationException("boom");
        _sut.OnFrame(0.1f);

        _sut.OnMissionTick();

        Assert.AreEqual(0, _hero.StandFasts);
    }

    [TestMethod]
    public void OnMissionTick_WhenTheHeroWriteThrows_LatchesAndRestoresTheController()
    {
        _hero.StandFastThrows = new InvalidOperationException("agent");
        Enter();

        _sut.OnMissionTick();

        Assert.IsTrue(_sut.Faulted);
        Assert.AreEqual(SeatState.Off, _sut.State);
        Assert.AreEqual(true, _screen.ControllerWrites.Last(), "the controller is never left disabled");
        Assert.AreEqual(1, _log.Count("fail", SeatChecks.FaultId));
    }

    // ---- The deployment edge in any state (5.3) ----

    [TestMethod]
    public void DeploymentEdge_WhileSeated_IsDropped_NoEntryAfterLeaving()
    {
        Seated();
        _sut.OnDeploymentEnded();

        Tick(beginInSeat: true);
        Assert.AreEqual(SeatState.Seated, _sut.State, "the edge changes nothing while the Seat is up");
        Tick(key: true, beginInSeat: true);
        Assert.AreEqual(SeatState.Off, _sut.State);

        Tick(beginInSeat: true);

        Assert.AreEqual(SeatState.Off, _sut.State, "the edge that arrived while seated was dropped, so it cannot enter after the leave");
    }

    [TestMethod]
    public void DeploymentEdge_ModeNoLongerBattle_DoesNotEnter()
    {
        _sut.OnDeploymentEnded();
        _screen.Facts = _screen.Facts with { BattleMode = false };

        Tick(beginInSeat: true);

        Assert.AreEqual(SeatState.Off, _sut.State, "the automatic entry needs Battle mode on the frame that consumes the edge");
        Assert.AreEqual(0, _screen.ControllerWrites.Count);
        Assert.AreEqual(0, _log.Lines.Count);
    }

    [TestMethod]
    public void DeploymentEdge_WithAKeyPress_InAnotherMode_StillEntersWithABlend()
    {
        _sut.OnDeploymentEnded();
        _screen.Facts = _screen.Facts with { BattleMode = false };

        Tick(key: true, beginInSeat: true);

        Assert.AreEqual(SeatState.Rising, _sut.State, "the key press is the player's own, so it blends in as any key press does");
    }

    // ---- The blend waits on a real dt (5.2) ----

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(-0.5f)]
    [DataRow(0f)]
    public void Rising_ABadDt_DoesNotAdvanceTheBlend(float dt)
    {
        Enter();

        Assert.IsTrue(_sut.OnFrame(dt));
        Assert.IsTrue(_sut.OnFrame(dt));

        Assert.AreEqual(SeatState.Rising, _sut.State);
        Assert.AreEqual(_screen.Camera.X, _screen.Frames.Last().Pose.X, Eps, "the blend is still at its start");
        Assert.IsTrue(SeatCameraMath.IsFinite(_screen.Frames.Last().Pose));
        Assert.IsFalse(_sut.Faulted);
    }

    // ---- Fault latch, from every entry (5.7) ----

    private void AssertLatchedOnce()
    {
        Assert.IsTrue(_sut.Faulted);
        Assert.AreEqual(SeatState.Off, _sut.State);
        Assert.IsFalse(SeatPresence.IsSeated);
        Assert.AreEqual(1, _log.Count("fail", SeatChecks.FaultId));
        Assert.AreEqual(1, _screen.Messages.Count(m => m == SeatMessage.Faulted));
        if (_screen.ControllerWrites.Count > 0)
            Assert.AreEqual(true, _screen.ControllerWrites.Last(), "the controller is never left disabled");
    }

    [TestMethod]
    public void Fault_WhenWritingTheLookThrows_LatchesOnce_AfterTheControllerIsBack()
    {
        Seated();
        _screen.LookThrows = new InvalidOperationException("look");

        Tick(key: true);
        Tick(key: true);
        _sut.OnFrame(0.1f);

        AssertLatchedOnce();
        CollectionAssert.AreEqual(new[] { false, true }, _screen.ControllerWrites);
    }

    [TestMethod]
    public void Fault_WhenBeginningTheGlideThrows_LatchesOnce()
    {
        Seated();
        _screen.GlideThrows = new InvalidOperationException("glide");

        Tick(key: true);
        Tick(key: true);

        AssertLatchedOnce();
        CollectionAssert.AreEqual(new[] { false, true }, _screen.ControllerWrites);
    }

    [TestMethod]
    public void Fault_WhenThePermissionsThrow_LatchesOnce_BeforeAnyControllerWrite()
    {
        _permissions = () => throw new InvalidOperationException("classifier");

        _sut.OnScreenTick(true);
        _sut.OnScreenTick(true);

        AssertLatchedOnce();
        Assert.AreEqual(0, _screen.ControllerWrites.Count);
    }

    [TestMethod]
    public void Fault_WhenGivingTheControllerBackThrowsOnFinish_LatchesOnce()
    {
        Seated();
        _screen.ControllerOnThrows = new InvalidOperationException("controller");

        _sut.Finish();
        _sut.Finish();

        AssertLatchedOnce();
        CollectionAssert.AreEqual(new[] { false, true }, _screen.ControllerWrites);
    }

    [TestMethod]
    public void Fault_WhenTheLoggerAndTheMessageThrow_StillLatches_AndNamesTheSwallowedSteps()
    {
        Seated();
        _logger.When(l => l.LogError(Arg.Any<string>())).Do(_ => throw new InvalidOperationException("log"));
        _screen.ApplyThrows = new InvalidOperationException("boom");

        _sut.OnFrame(0.1f);

        Assert.IsTrue(_sut.Faulted);
        Assert.AreEqual(1, _log.Count("fail", SeatChecks.FaultId), "every step of the latch is independent");
        Assert.AreEqual(1, _log.Lines.Count(l => l.Result == "swallowed" && l.Id == "SeatOfSeeing.FaultLatch"));
    }

    // ---- Fault latch ----

    [TestMethod]
    public void FaultInTheFrame_LatchesOff_ReenablesTheHero_AndHandsBackTheCamera()
    {
        Seated();
        _screen.ApplyThrows = new InvalidOperationException("boom");

        var owned = _sut.OnFrame(0.1f);

        Assert.IsFalse(owned);
        Assert.AreEqual(SeatState.Off, _sut.State);
        Assert.IsTrue(_sut.Faulted);
        CollectionAssert.AreEqual(new[] { false, true }, _screen.ControllerWrites);
        Assert.AreEqual(1, _log.Count("fail", SeatChecks.FaultId));
        CollectionAssert.Contains(_screen.Messages, SeatMessage.Faulted);
        _logger.Received(1).LogError(Arg.Is<string>(s => s.Contains("boom")));
        Assert.IsFalse(SeatPresence.IsSeated);
    }

    [TestMethod]
    public void Fault_IsLatchedForTheMission_NoKeyBringsTheSeatBack()
    {
        Seated();
        _screen.ApplyThrows = new InvalidOperationException("boom");
        _sut.OnFrame(0.1f);
        _screen.ApplyThrows = null;
        _screen.ControllerWrites.Clear();

        Tick(key: true);

        Assert.AreEqual(SeatState.Off, _sut.State);
        Assert.AreEqual(0, _screen.ControllerWrites.Count);
        Assert.IsFalse(_sut.OnFrame(0.1f));
    }

    [TestMethod]
    public void Fault_ShowsOneMessageAndOneFailLine_EvenWhenFrameKeepsThrowing()
    {
        Seated();
        _screen.ApplyThrows = new InvalidOperationException("boom");

        _sut.OnFrame(0.1f);
        _sut.OnFrame(0.1f);
        _sut.OnFrame(0.1f);

        Assert.AreEqual(1, _log.Count("fail", SeatChecks.FaultId));
        Assert.AreEqual(1, _screen.Messages.Count(m => m == SeatMessage.Faulted));
    }

    [TestMethod]
    public void Fault_DoesNotArmTheGlide()
    {
        Seated();
        _screen.ApplyThrows = new InvalidOperationException("boom");

        _sut.OnFrame(0.1f);

        Assert.AreEqual(0, _screen.GlideCalls);
        Assert.AreEqual(0, _screen.Looks.Count);
    }

    // ---- Check lines for a refusal and for every leave (5.10) ----

    private (string Result, string Id, string Key, string Detail) Line(string result, string id) =>
        _log.Lines.Single(l => l.Result == result && l.Id == id);

    [TestMethod]
    public void Key_WithTheSettingOff_WritesASkipLine()
    {
        Tick(key: true, enabled: false);

        var line = Line("skip", SeatChecks.EnterId);
        Assert.AreEqual("7:setting-off", line.Key, "the reason is in the key, so each reason logs once a mission");
        Assert.AreEqual("mission=7 reason=setting-off", line.Detail);
    }

    [TestMethod]
    public void Key_WithoutTheCameraPermission_WritesASkipLine()
    {
        Tick(key: true, permissions: Denied);

        var line = Line("skip", SeatChecks.EnterId);
        Assert.AreEqual("7:denied", line.Key);
        Assert.AreEqual("mission=7 reason=denied", line.Detail);
    }

    [TestMethod]
    public void Key_WhileTheRageHolds_WritesASkipLine()
    {
        Rage(true);

        Enter();

        var line = Line("skip", SeatChecks.EnterId);
        Assert.AreEqual("7:rage", line.Key);
        Assert.AreEqual("mission=7 reason=rage", line.Detail);
    }

    [TestMethod]
    public void Key_WithACustomCameraView_WritesASkipLine()
    {
        _screen.Facts = _screen.Facts with { CustomCamera = true };

        Enter();

        var line = Line("skip", SeatChecks.EnterId);
        Assert.AreEqual("7:custom-camera", line.Key);
        Assert.AreEqual("mission=7 reason=custom-camera", line.Detail);
    }

    [TestMethod]
    public void TwoDifferentRefusals_WriteTwoLines_AndTheSameRefusalOnce()
    {
        Tick(key: true, permissions: Denied);
        Tick(key: true, permissions: Denied);
        Rage(true);
        Enter();

        Assert.AreEqual(2, _log.Lines.Count(l => l.Result == "skip" && l.Id == SeatChecks.EnterId));
    }

    [DataTestMethod]
    [DataRow("deployment")]
    [DataRow("interlude")]
    [DataRow("photo-mode")]
    [DataRow("mission-ended")]
    public void Key_InAStateThePlayerCannotActOn_WritesNoLine(string state)
    {
        _screen.Facts = state switch
        {
            "deployment" => _screen.Facts with { Deployment = true, BattleMode = false },
            "interlude" => _screen.Facts with { Interlude = true, BattleMode = false },
            "photo-mode" => _screen.Facts with { PhotoMode = true },
            _ => _screen.Facts with { MissionEnded = true },
        };

        Enter();

        Assert.AreEqual(SeatState.Off, _sut.State);
        Assert.AreEqual(0, _log.Lines.Count, state + " is not a refusal the player can act on");
    }

    [TestMethod]
    public void Key_WithTheSettingOffInDeployment_WritesNoLine()
    {
        _screen.Facts = _screen.Facts with { Deployment = true, BattleMode = false };

        Tick(key: true, enabled: false);

        Assert.AreEqual(0, _log.Lines.Count);
    }

    [TestMethod]
    public void Key_WhenTheCameraCannotBeRead_WritesAFailLine()
    {
        _screen.CameraReadable = false;

        Enter();

        var line = Line("fail", SeatChecks.EnterId);
        Assert.AreEqual("7:no-camera", line.Key);
        Assert.AreEqual("mission=7 reason=no-camera", line.Detail);
    }

    [TestMethod]
    public void Key_WhenTheCameraFrameIsNotFinite_WritesAFailLine()
    {
        _screen.Camera = new SeatPose(float.NaN, 0f, 0f, 0f, 0f);

        Enter();

        Assert.AreEqual("7:no-camera", Line("fail", SeatChecks.EnterId).Key);
    }

    [TestMethod]
    public void Key_WhenTheGroundIsNotFinite_WritesAFailLine()
    {
        _screen.Ground = float.NaN;

        Enter();

        var line = Line("fail", SeatChecks.EnterId);
        Assert.AreEqual("7:no-ground", line.Key);
        Assert.AreEqual("mission=7 reason=no-ground", line.Detail);
        Assert.AreEqual(SeatState.Off, _sut.State);
    }

    [TestMethod]
    public void BattleStartEntry_WhenTheGroundIsNotFinite_WritesTheFailLineToo()
    {
        _screen.Ground = float.NaN;
        _sut.OnDeploymentEnded();

        Tick(beginInSeat: true);

        Assert.AreEqual("7:no-ground", Line("fail", SeatChecks.EnterId).Key);
    }

    private void AssertLeave(string reason, string glide)
    {
        var line = Line("pass", SeatChecks.LeaveId);
        Assert.AreEqual("7:" + reason, line.Key);
        StringAssert.StartsWith(line.Detail, "mission=7 frames=");
        StringAssert.EndsWith(line.Detail, " glide=" + glide + " reason=" + reason);
        Assert.AreEqual(1, _log.Count("pass", SeatChecks.LeaveId));
    }

    [TestMethod]
    public void Leave_OnTheKey_WritesTheLeaveLineWithReasonKey()
    {
        Seated();

        Tick(key: true);

        AssertLeave("key", "1");
    }

    [TestMethod]
    public void Leave_OnTheSettingTurnedOff_WritesTheLeaveLineWithReasonSettingOff()
    {
        Seated();

        Tick(enabled: false);

        AssertLeave("setting-off", "1");
    }

    [TestMethod]
    public void Leave_OnAnInterlude_WritesTheLeaveLineWithReasonInterlude()
    {
        Seated();
        _screen.Facts = _screen.Facts with { Interlude = true, BattleMode = false };

        Tick();

        AssertLeave("interlude", "0");
    }

    [TestMethod]
    public void Leave_OnTheRage_WritesTheLeaveLineWithReasonRage()
    {
        Seated();
        Rage(true);

        Tick();

        AssertLeave("rage", "1");
    }

    [TestMethod]
    public void Leave_OnFinish_WritesTheLeaveLineWithReasonFinish()
    {
        Seated();

        _sut.Finish();

        AssertLeave("finish", "0");
    }

    [TestMethod]
    public void Leave_WhenTheGlideDoesNotBegin_WritesGlideZero()
    {
        Seated();
        _screen.GlideBegins = false;

        Tick(key: true);

        AssertLeave("key", "0");
    }

    [TestMethod]
    public void Finish_Twice_ReleasesTheHeroOnce()
    {
        Seated();

        _sut.Finish();
        _sut.Finish();

        CollectionAssert.AreEqual(new[] { false, true }, _screen.ControllerWrites);
        Assert.AreEqual(1, _log.Count("pass", SeatChecks.LeaveId));
        Assert.IsFalse(_sut.Faulted);
    }

    [TestMethod]
    public void Finish_AfterAFault_TouchesNothingMore()
    {
        Seated();
        _screen.ApplyThrows = new InvalidOperationException("boom");
        _sut.OnFrame(0.1f);
        var writes = _screen.ControllerWrites.Count;

        _sut.Finish();

        Assert.AreEqual(writes, _screen.ControllerWrites.Count);
    }

    // ---- The height limit (5.4) ----

    [TestMethod]
    public void HeightLimit_IsReadOncePerEntry()
    {
        _screen.HeightLimit = 150f;

        Seated();
        for (var i = 0; i < 10; i++)
            _sut.OnFrame(0.1f);

        Assert.AreEqual(1, _screen.HeightLimitReads);
    }

    [TestMethod]
    public void HeightLimit_CapsThePose_AndTheFlightHeightSoTheWheelDoesNotWindUp()
    {
        _screen.HeightLimit = 110f;
        Seated();
        _screen.Input = new SeatInput(0f, 0f, 1f, 0f, 0f, 0f, false);

        for (var i = 0; i < 100; i++)
            _sut.OnFrame(0.1f);

        Assert.AreEqual(110f, _screen.Frames.Last().Pose.Z, 0.01f, "ten metres above the ground at 100");

        _screen.Input = new SeatInput(0f, 0f, -1f, 0f, 0f, 0f, false);
        _sut.OnFrame(0.1f);

        Assert.IsTrue(_screen.Frames.Last().Pose.Z < 110f - 0.5f, "one frame of sinking leaves the cap at once; nothing was wound up past it");
    }

    [TestMethod]
    public void HeightLimit_BelowTheGround_FloorsTheFlightAtMinimumHeight_AndTheViewStaysAboveTheGround()
    {
        _screen.HeightLimit = 50f;
        Seated();

        _sut.OnFrame(0.1f);

        var z = _screen.Frames.Last().Pose.Z;
        Assert.AreEqual(100f + SeatCameraMath.GroundClearance, z, 0.01f, "the floor goes last and beats the limit");
    }

    [DataTestMethod]
    [DataRow(0f)]
    [DataRow(-5f)]
    [DataRow(float.NaN)]
    public void HeightLimit_NoTagOrNoPositiveZ_MeansNoLimit(float limit)
    {
        _screen.HeightLimit = limit;
        Seated();
        _screen.Input = new SeatInput(0f, 0f, 1f, 0f, 0f, 0f, false);

        for (var i = 0; i < 200; i++)
            _sut.OnFrame(0.1f);

        Assert.AreEqual(100f + SeatCameraMath.MaxHeight, _screen.Frames.Last().Pose.Z, 0.01f);
    }

    [TestMethod]
    public void HeightLimit_AboveTheFlight_ChangesNothing()
    {
        _screen.HeightLimit = 1000f;
        Seated();

        Assert.AreEqual(100f + 22f, _screen.Frames.Last().Pose.Z, 0.01f);
    }

    // ---- A boundary position that is not a real number (5.4) ----

    [TestMethod]
    public void Seated_BoundaryClampGivingNaN_KeepsThePreviousPosition()
    {
        Seated();
        var before = _screen.Frames.Last().Pose;
        _screen.ClampGivesNaN = true;
        _screen.Input = new SeatInput(0f, 1f, 0f, 0f, 0f, 0f, false);

        Assert.IsTrue(_sut.OnFrame(0.1f));

        var pose = _screen.Frames.Last().Pose;
        Assert.IsTrue(SeatCameraMath.IsFinite(pose));
        Assert.AreEqual(before.X, pose.X, Eps);
        Assert.AreEqual(before.Y, pose.Y, Eps);
    }

    [TestMethod]
    public void Enter_BoundaryClampGivingNaN_KeepsTheFlightFromBeforeTheClamp()
    {
        _screen.ClampGivesNaN = true;

        Enter();
        Finish();

        var pose = _screen.Frames.Last().Pose;
        Assert.AreEqual(100f, pose.X, Eps);
        Assert.AreEqual(200f - 9f, pose.Y, Eps);
        Assert.IsFalse(_sut.Faulted);
    }

    // ---- What the field trial reads and sets (build-spec section 12) ----

    [TestMethod]
    public void OnFrame_WithAnInputOverride_FliesOnItAndReadsNoScreenInput()
    {
        Seated();
        var before = _screen.Frames.Last().Pose;
        _screen.Input = new SeatInput(1f, 0f, 0f, 0f, 0f, 0f, false);
        var reads = _screen.InputReads;

        Assert.IsTrue(_sut.OnFrame(0.1f, new SeatInput(0f, 1f, 0f, 0f, 0f, 0f, false)));

        Assert.AreEqual(reads, _screen.InputReads, "the override replaces ReadInput");
        var pose = _screen.Frames.Last().Pose;
        Assert.IsTrue(pose.Y > before.Y, "forward moved the view");
        Assert.AreEqual(before.X, pose.X, Eps, "the screen's sideways input was not used");
    }

    [TestMethod]
    public void OnFrame_WithAnInputOverrideWhileRising_StillGivesTheBlendNoInput()
    {
        var wild = new SeatInput(1f, 1f, 1f, 1f, 5f, 5f, false);
        Enter();
        _sut.OnFrame(0.1f, wild);
        var withOverride = _screen.Frames.Last().Pose;
        Assert.AreEqual(SeatState.Rising, _sut.State);

        Setup();
        Enter();
        _sut.OnFrame(0.1f);

        Assert.AreEqual(_screen.Frames.Last().Pose, withOverride, "the blend moved the same with and without the override");
    }

    [TestMethod]
    public void Frames_CountsOwnedFramesOfTheStay_AndStartsAgainOnTheNextEntry()
    {
        Enter();
        Assert.AreEqual(0, _sut.Frames);

        _sut.OnFrame(0.1f);
        _sut.OnFrame(0.1f);
        _sut.OnFrame(0.1f);
        Assert.AreEqual(3, _sut.Frames);

        Finish();
        Tick(key: true);
        Enter();
        Assert.AreEqual(0, _sut.Frames);
    }

    [TestMethod]
    public void GlideBegan_IsTrueAfterALeaveThatArmedTheGlide_AndFalseBeforeAnyLeave()
    {
        Assert.IsFalse(_sut.GlideBegan);
        Seated();
        Assert.IsFalse(_sut.GlideBegan);

        Tick(key: true);

        Assert.IsTrue(_sut.GlideBegan);
    }

    [TestMethod]
    public void GlideBegan_IsFalseWhenThereWasNoAgentToFollow_AndAfterALeaveOutsideBattleMode()
    {
        _screen.GlideBegins = false;
        Seated();
        Tick(key: true);
        Assert.IsFalse(_sut.GlideBegan, "no agent to follow");

        _screen.GlideBegins = true;
        Seated();
        _screen.Facts = _screen.Facts with { BattleMode = false };
        Tick(key: true);
        Assert.IsFalse(_sut.GlideBegan, "the glide runs in Battle mode only, and the latest leave is the one that counts");
    }
}
