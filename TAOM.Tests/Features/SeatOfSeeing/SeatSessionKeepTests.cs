using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Client.Features.SeatOfSeeing;
using TAOM.Core.Logging;
using TAOM.Features.SeatOfSeeing;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Tests.Features.SeatOfSeeing;

/// <summary>Stage S3 of the Seat's state machine (build-spec section 7): keeping to a soldier or a company, the orbit and the let-go.</summary>
[TestClass]
public class SeatSessionKeepTests
{
    private const float Eps = 0.01f;
    private const float Dt = 1f / 60f;
    private static readonly SeatPermissions Allowed = new(Camera: true, FightOn: true, Orders: true, Time: true);

    private FakeSeatScreen _screen = null!;
    private FakeSeatHero _hero = null!;
    private FakeSeatSettings _settings = null!;
    private RecordingOutcomeLog _log = null!;
    private SeatSession _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        SeatPresence.Set(false);
        _screen = new FakeSeatScreen();
        _hero = new FakeSeatHero();
        _settings = new FakeSeatSettings();
        _log = new RecordingOutcomeLog();
        _sut = new SeatSession(_screen, _hero, _settings, () => Allowed, _log, Substitute.For<IModLogger>(), "7");
    }

    [TestCleanup]
    public void Cleanup() => SeatPresence.Set(false);

    private void Seated()
    {
        _sut.OnScreenTick(true);
        for (var i = 0; i < 40 && _sut.State != SeatState.Seated; i++)
            _sut.OnFrame(0.1f);
        Assert.AreEqual(SeatState.Seated, _sut.State);
        _sut.OnFrame(Dt);
    }

    private SeatPose LastPose => _screen.Frames.Last().Pose;

    // The target the orbit would start on: the point of the camera's look line at the start distance.
    private SeatTargetRead PointOnTheLookLine()
    {
        var begin = SeatOrbit.Begin(LastPose);
        return new SeatTargetRead(false, begin.PointX, begin.PointY, begin.PointZ);
    }

    private void Keep(SeatTargetRead? target = null)
    {
        _screen.Kept = target ?? PointOnTheLookLine();
        _sut.PressKeep();
        _sut.OnMissionTick();
    }

    private double DistanceTo(SeatPose pose, SeatTargetRead target) =>
        Math.Sqrt(Math.Pow(pose.X - target.X, 2) + Math.Pow(pose.Y - target.Y, 2) + Math.Pow(pose.Z - target.Z, 2));

    private int Lines(string result, string id) => _log.Count(result, id);

    // ---- The press ----

    [TestMethod]
    public void PressKeep_IsStoredForTheMissionTick_NoRayIsCastInTheScreenTick()
    {
        Seated();

        _sut.PressKeep();
        _sut.OnScreenTick(false);
        _sut.OnFrame(Dt);

        Assert.AreEqual(0, _screen.KeepCalls, "the rays are cast in the next mission tick");

        _sut.OnMissionTick();

        Assert.AreEqual(1, _screen.KeepCalls);
    }

    [TestMethod]
    public void PressKeep_WhileOffOrRising_IsIgnored()
    {
        _sut.PressKeep();
        _sut.OnMissionTick();
        _sut.OnScreenTick(true);
        _sut.PressKeep();
        _sut.OnMissionTick();

        Assert.AreEqual(0, _screen.KeepCalls);
    }

    [TestMethod]
    public void PressKeep_DuringTheRise_IsDroppedEvenWhenTheBlendEndsBeforeTheTick()
    {
        _sut.OnScreenTick(true);
        Assert.AreEqual(SeatState.Rising, _sut.State);
        _sut.PressKeep();
        for (var i = 0; i < 40 && _sut.State != SeatState.Seated; i++)
            _sut.OnFrame(0.1f);
        Assert.AreEqual(SeatState.Seated, _sut.State);

        _sut.OnMissionTick();

        Assert.AreEqual(0, _screen.KeepCalls, "a press made while the view was rising is not a press of the seated view");
    }

    [TestMethod]
    public void PressKeep_AfterAFault_IsIgnored()
    {
        Seated();
        _screen.ApplyThrows = new InvalidOperationException("boom");
        _sut.OnFrame(Dt);

        _sut.PressKeep();
        _sut.OnMissionTick();

        Assert.IsTrue(_sut.Faulted);
        Assert.AreEqual(0, _screen.KeepCalls);
    }

    [TestMethod]
    public void PressKeep_ThatLeavesTheSeatBeforeTheTick_IsDropped()
    {
        Seated();
        _sut.PressKeep();
        _sut.OnScreenTick(true);

        _sut.OnMissionTick();

        Assert.AreEqual(SeatState.Off, _sut.State);
        Assert.AreEqual(0, _screen.KeepCalls);
    }

    [TestMethod]
    public void PressKeep_DuringPhotoMode_IsDropped()
    {
        Seated();
        _screen.Facts = _screen.Facts with { PhotoMode = true };

        _sut.PressKeep();
        _sut.OnMissionTick();
        _screen.Facts = _screen.Facts with { PhotoMode = false };
        _sut.OnMissionTick();

        Assert.AreEqual(0, _screen.KeepCalls, "no Seat key acts in photo mode, and the press is not kept for later");
        Assert.AreEqual(0, _log.Lines.Count(l => l.Id == SeatChecks.KeepId));
    }

    [TestMethod]
    public void PressKeep_DuringPhotoMode_ThenPhotoModeEnds_IsDropped()
    {
        Seated();
        _screen.Facts = _screen.Facts with { PhotoMode = true };

        _sut.PressKeep();
        _screen.Facts = _screen.Facts with { PhotoMode = false };
        _sut.OnMissionTick();

        Assert.AreEqual(0, _screen.KeepCalls, "a press made in photo mode is dropped at the press");
        Assert.AreEqual(0, _log.Lines.Count(l => l.Id == SeatChecks.KeepId));
    }

    [TestMethod]
    public void PressKeep_ThenPhotoModeStartsBeforeTheTick_IsDropped()
    {
        Seated();

        _sut.PressKeep();
        _screen.Facts = _screen.Facts with { PhotoMode = true };
        _sut.OnMissionTick();

        Assert.AreEqual(0, _screen.KeepCalls, "photo mode that starts between the press and the tick drops the press");
    }

    [TestMethod]
    public void PressKeep_ToLetGo_DuringPhotoMode_KeepsTheTarget()
    {
        Seated();
        Keep();
        _screen.Facts = _screen.Facts with { PhotoMode = true };

        _sut.PressKeep();
        _sut.OnMissionTick();

        Assert.AreEqual(SeatKeepKind.Soldier, _sut.Keep);
        Assert.AreEqual(0, _screen.Unkeeps);
    }

    [TestMethod]
    public void Keep_OfASoldier_WritesTheKeepLineWithItsKind()
    {
        Seated();
        _screen.KeepResult = SeatKeepKind.Soldier;

        Keep();

        var line = _log.Lines.Single(l => l.Id == SeatChecks.KeepId);
        Assert.AreEqual("pass", line.Result);
        Assert.AreEqual("7:Soldier", line.Key);
        StringAssert.Contains(line.Detail, "kind=Soldier");
    }

    [TestMethod]
    public void Keep_OfACompany_WritesTheKeepLineWithItsKind()
    {
        Seated();
        _screen.KeepResult = SeatKeepKind.Company;

        Keep();

        StringAssert.Contains(_log.Lines.Single(l => l.Id == SeatChecks.KeepId).Detail, "kind=Company");
    }

    [TestMethod]
    public void Keep_WhenNobodyIsThere_ShowsOneMessage_AndWritesOneSkipLine()
    {
        Seated();
        _screen.KeepResult = SeatKeepKind.None;

        Keep();
        Keep();

        Assert.AreEqual(2, _screen.Messages.Count(m => m == SeatMessage.KeepNobody), "each press that finds nobody says so");
        Assert.AreEqual(1, Lines("skip", SeatChecks.KeepId), "the log writes a line once for each (id, result, key)");
        Assert.AreEqual(0, Lines("pass", SeatChecks.KeepId));
        _sut.OnFrame(Dt);
        Assert.AreEqual(0, _screen.Unkeeps);
    }

    [TestMethod]
    public void Keep_ThatThrows_LatchesTheFault()
    {
        Seated();
        _screen.KeepThrows = new InvalidOperationException("ray");

        _sut.PressKeep();
        _sut.OnMissionTick();

        Assert.IsTrue(_sut.Faulted);
        Assert.IsFalse(SeatPresence.IsSeated);
    }

    // ---- The orbit ----

    [TestMethod]
    public void Orbit_FirstFrame_StaysWhereTheViewWas()
    {
        Seated();
        var before = LastPose;
        Keep();

        _sut.OnFrame(Dt);

        var after = LastPose;
        Assert.AreEqual(before.X, after.X, Eps);
        Assert.AreEqual(before.Y, after.Y, Eps);
        Assert.AreEqual(before.Z, after.Z, Eps);
        Assert.AreEqual(before.Yaw, after.Yaw, Eps);
    }

    [TestMethod]
    public void Orbit_ASteadyTarget_IsLookedAtFromTheStartDistance()
    {
        Seated();
        var target = new SeatTargetRead(false, 140f, 260f, 101.2f);
        Keep(target);

        for (var i = 0; i < 240; i++)
            _sut.OnFrame(Dt);

        Assert.AreEqual(SeatOrbit.StartDistance, DistanceTo(LastPose, target), 0.05);
    }

    [TestMethod]
    public void Orbit_FollowsAMovingTarget()
    {
        Seated();
        Keep();
        var target = _screen.Kept;
        for (var i = 0; i < 240; i++)
        {
            target = target with { X = target.X + 0.05f };
            _screen.Kept = target;
            _sut.OnFrame(Dt);
        }

        Assert.AreEqual(SeatOrbit.StartDistance, DistanceTo(LastPose, target), 0.5, "within the lag of the ease");
    }

    [TestMethod]
    public void Orbit_TheMouseTurnsIt_AndTheWheelChangesTheDistance()
    {
        Seated();
        Keep();
        _sut.OnFrame(Dt);
        var yaw = LastPose.Yaw;

        _screen.Input = new SeatInput(0f, 0f, 0f, 0f, 100f, 0f, false);
        _sut.OnFrame(Dt);
        Assert.AreEqual(yaw + 100f * SeatCameraMath.LookPerUnit, LastPose.Yaw, Eps);

        _screen.Input = new SeatInput(0f, 0f, 0f, 3f, 0f, 0f, false);
        var target = _screen.Kept;
        for (var i = 0; i < 300; i++)
            _sut.OnFrame(Dt);
        Assert.AreEqual(SeatOrbit.MinDistance, DistanceTo(LastPose, target), 0.2, "the wheel, rolled away, lowers the distance to its minimum");
    }

    [TestMethod]
    public void Orbit_TheMouseAndTheWheelAndTheFastKey_DoNotLetGo()
    {
        Seated();
        Keep();
        _screen.Input = new SeatInput(0f, 0f, 0f, 1f, 50f, -20f, true);

        _sut.OnFrame(Dt);
        _sut.OnFrame(Dt);

        Assert.AreEqual(0, _screen.Unkeeps);
        Assert.AreEqual(0, Lines("pass", SeatChecks.LetGoId));
    }

    [DataTestMethod]
    [DataRow(1f, 0f, 0f, DisplayName = "right")]
    [DataRow(0f, -1f, 0f, DisplayName = "back")]
    [DataRow(0f, 0f, 1f, DisplayName = "rise")]
    [DataRow(0f, 0f, -1f, DisplayName = "sink")]
    public void Orbit_MovementRiseOrSink_LetsGo_AndThisFrameFliesFromWhereTheViewIs(float right, float forward, float lift)
    {
        Seated();
        Keep();
        for (var i = 0; i < 30; i++)
            _sut.OnFrame(Dt);
        var before = LastPose;

        _screen.Input = new SeatInput(right, forward, lift, 0f, 0f, 0f, false);
        _sut.OnFrame(Dt);

        Assert.AreEqual(1, _screen.Unkeeps);
        Assert.AreEqual(1, _log.Lines.Count(l => l.Id == SeatChecks.LetGoId && l.Key == "7:flight"));
        var moved = Math.Sqrt(Math.Pow(LastPose.X - before.X, 2) + Math.Pow(LastPose.Y - before.Y, 2) + Math.Pow(LastPose.Z - before.Z, 2));
        Assert.IsTrue(moved < 1.5, "no jump: the flight continues from the pose the orbit left, moved " + moved);
        Assert.AreEqual(SeatState.Seated, _sut.State);
        Assert.AreEqual(0, _screen.Messages.Count(m => m == SeatMessage.KeepGone));
    }

    [TestMethod]
    public void Orbit_LettingGoByKey_ContinuesFromWhereTheViewIs()
    {
        Seated();
        Keep();
        for (var i = 0; i < 30; i++)
            _sut.OnFrame(Dt);
        var before = LastPose;

        _sut.PressKeep();
        _sut.OnMissionTick();
        _sut.OnFrame(Dt);

        Assert.AreEqual(1, _log.Lines.Count(l => l.Id == SeatChecks.LetGoId && l.Key == "7:key"));
        Assert.AreEqual(before.X, LastPose.X, Eps);
        Assert.AreEqual(before.Y, LastPose.Y, Eps);
        Assert.AreEqual(before.Z, LastPose.Z, Eps);
        Assert.AreEqual(before.Yaw, LastPose.Yaw, Eps);
        Assert.AreEqual(before.Pitch, LastPose.Pitch, Eps);
    }

    [DataTestMethod]
    [DataRow(0f, DisplayName = "16 metres")]
    [DataRow(11f, DisplayName = "4 metres")]
    public void Orbit_LettingGoOfALowOrbitOverFlatGround_HasNoJump(float wheelNotches)
    {
        Seated();
        Keep(new SeatTargetRead(false, 100f, 216f, _screen.Ground));
        _screen.Input = new SeatInput(0f, 0f, 0f, wheelNotches, 0f, -300f, false);
        _sut.OnFrame(Dt);
        _screen.Input = default;
        for (var i = 0; i < 400; i++)
            _sut.OnFrame(Dt);
        var orbit = LastPose;
        Assert.AreEqual(_screen.Ground + SeatCameraMath.MinHeight, orbit.Z, Eps, "the pitch is at its floor, so the camera stands on the orbit's floor");

        _sut.PressKeep();
        _sut.OnMissionTick();
        _sut.OnFrame(Dt);

        Assert.AreEqual(orbit.Z, LastPose.Z, Eps, "the first free frame is where the orbit was");
    }

    [TestMethod]
    public void Orbit_PressingKeepAgain_ThenOnceMore_KeepsAgain()
    {
        Seated();
        Keep();
        _sut.PressKeep();
        _sut.OnMissionTick();

        Keep();

        Assert.AreEqual(2, _screen.KeepCalls);
        Assert.AreEqual(1, _screen.Unkeeps);
    }

    [TestMethod]
    public void Orbit_WhenTheTargetIsGone_LetsGoWithOneMessage_AndFliesThisFrame()
    {
        Seated();
        Keep();
        _sut.OnFrame(Dt);
        var before = LastPose;
        _screen.Kept = new SeatTargetRead(true, 0f, 0f, 0f);

        _sut.OnFrame(Dt);
        _sut.OnFrame(Dt);

        Assert.AreEqual(1, _screen.Unkeeps);
        Assert.AreEqual(1, _screen.Messages.Count(m => m == SeatMessage.KeepGone));
        Assert.AreEqual(1, _log.Lines.Count(l => l.Id == SeatChecks.LetGoId && l.Key == "7:gone"));
        Assert.AreEqual(before.X, LastPose.X, 0.5f, "the view stays where it was");
        Assert.AreEqual(SeatState.Seated, _sut.State);
    }

    [TestMethod]
    public void Orbit_WhenTheSeatIsLeft_DropsTheTarget_AndWritesTheLeaveLine()
    {
        Seated();
        Keep();
        _sut.OnFrame(Dt);

        _sut.OnScreenTick(true);

        Assert.AreEqual(SeatState.Off, _sut.State);
        Assert.AreEqual(1, _screen.Unkeeps);
        Assert.AreEqual(1, _log.Lines.Count(l => l.Id == SeatChecks.LetGoId && l.Key == "7:leave"));
    }

    [TestMethod]
    public void Orbit_WhenTheMissionEnds_DropsTheTarget()
    {
        Seated();
        Keep();

        _sut.Finish();

        Assert.AreEqual(1, _screen.Unkeeps);
    }

    [TestMethod]
    public void Orbit_AfterTheLeave_TheNextEntryDoesNotKeepTheOldTarget()
    {
        Seated();
        Keep();
        _sut.OnScreenTick(true);
        _sut.OnScreenTick(true);
        for (var i = 0; i < 20 && _sut.State != SeatState.Seated; i++)
            _sut.OnFrame(0.1f);
        _screen.Kept = new SeatTargetRead(false, 500f, 500f, 100f);
        var unkeeps = _screen.Unkeeps;

        for (var i = 0; i < 5; i++)
            _sut.OnFrame(Dt);

        Assert.AreEqual(unkeeps, _screen.Unkeeps);
        Assert.IsTrue(DistanceTo(LastPose, _screen.Kept) > 100.0, "a seated flight, not an orbit of the old target");
    }

    [TestMethod]
    public void Orbit_ReadsTheInputOnce_AndTheGroundOnce_APerFrame()
    {
        Seated();
        Keep();
        var inputs = _screen.InputReads;
        var grounds = _screen.GroundReads.Count;

        _sut.OnFrame(Dt);

        Assert.AreEqual(inputs + 1, _screen.InputReads);
        Assert.AreEqual(grounds + 1, _screen.GroundReads.Count);
    }

    [TestMethod]
    public void Orbit_ATrialInput_ReplacesTheScreensInput()
    {
        Seated();
        Keep();
        var inputs = _screen.InputReads;

        _sut.OnFrame(Dt, new SeatInput(0f, 0f, 0f, 0f, 100f, 0f, false));

        Assert.AreEqual(inputs, _screen.InputReads);
    }

    [TestMethod]
    public void Orbit_StaysAboveTheRawGround()
    {
        Seated();
        Keep(new SeatTargetRead(false, 100f, 210f, 101f));
        _screen.Ground = 130f;

        for (var i = 0; i < 5; i++)
            _sut.OnFrame(Dt);

        Assert.IsTrue(LastPose.Z >= 130f + SeatCameraMath.MinHeight - Eps, "the floor applies to an orbit frame: " + LastPose.Z);
    }

    [TestMethod]
    public void Orbit_IsCappedAtTheHeightLimit()
    {
        _screen.HeightLimit = 105f;
        Seated();
        Keep(new SeatTargetRead(false, 100f, 210f, 101f));

        for (var i = 0; i < 120; i++)
            _sut.OnFrame(Dt);

        Assert.IsTrue(LastPose.Z <= 105f + Eps, "the cap applies to an orbit frame: " + LastPose.Z);
    }

    [TestMethod]
    public void Orbit_IsKeptInsideTheBoundary()
    {
        Seated();
        Keep(new SeatTargetRead(false, 400f, 210f, 101f));
        _screen.ClampX = 150f;

        for (var i = 0; i < 300; i++)
            _sut.OnFrame(Dt);

        Assert.IsTrue(LastPose.X <= 150f + Eps, "the boundary clamp applies to an orbit frame: " + LastPose.X);
    }

    [TestMethod]
    public void Orbit_ABoundaryThatIsNotARealNumber_KeepsTheLastPosition()
    {
        Seated();
        Keep();
        _sut.OnFrame(Dt);
        var before = LastPose;
        _screen.ClampGivesNaN = true;

        _sut.OnFrame(Dt);

        Assert.AreEqual(before.X, LastPose.X, Eps);
        Assert.AreEqual(before.Y, LastPose.Y, Eps);
    }

    [TestMethod]
    public void Orbit_NoLetGoLineWhenNothingWasKept()
    {
        Seated();

        _sut.OnScreenTick(true);

        Assert.AreEqual(0, _screen.Unkeeps);
        Assert.AreEqual(0, _log.Lines.Count(l => l.Id == SeatChecks.LetGoId));
    }
}
