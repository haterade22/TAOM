using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Client.Features.SeatOfSeeing;
using TAOM.Core.Logging;
using TAOM.Features.SeatOfSeeing;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Tests.Features.SeatOfSeeing;

/// <summary>The lift of the hero's third-person view with the order panel open, from the session (build-spec section 11): when it lifts, the writes, the exact zero and its three owners.</summary>
[TestClass]
public class SeatSessionRiseTests
{
    private const float Frame = 1f / 60f;
    private static readonly SeatPermissions Allowed = new(Camera: true, FightOn: true, Orders: true, Time: true, TakeSoldier: true);

    private FakeSeatScreen _screen = null!;
    private FakeSeatHero _hero = null!;
    private FakeSeatSettings _settings = null!;
    private SeatPermissions _permissions;
    private RecordingOutcomeLog _log = null!;
    private SeatSession _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        SeatPresence.Set(false);
        _screen = new FakeSeatScreen();
        _hero = new FakeSeatHero();
        _settings = new FakeSeatSettings { RiseWithPanel = true };
        _permissions = Allowed;
        _log = new RecordingOutcomeLog();
        _sut = new SeatSession(_screen, _hero, _settings, () => _permissions, _log, Substitute.For<IModLogger>(), "7");
    }

    [TestCleanup]
    public void Cleanup() => SeatPresence.Set(false);

    private void Frames(float seconds)
    {
        for (var t = 0f; t < seconds; t += Frame)
            _sut.OnFrameTick(Frame);
    }

    private void OpenAndLift(float seconds = 1f)
    {
        _screen.RisePanelOpen = true;
        Frames(seconds);
    }

    private void SeatUp()
    {
        _sut.OnScreenTick(true);
        for (var i = 0; i < 40 && _sut.State != SeatState.Seated; i++)
            _sut.OnFrame(0.1f);
    }

    [TestMethod]
    public void AnOpenPanel_InThirdPerson_LiftsTheViewToTheFieldHeight_AndWritesOnlyWhenItChanges()
    {
        OpenAndLift();

        Assert.AreEqual(SeatPanelLift.FieldHeight, _screen.LiftWrites.Last());
        Assert.IsTrue(_screen.LiftWrites.Zip(_screen.LiftWrites.Skip(1), (a, b) => b >= a).All(x => x), "the lift only rises while the panel is open");
        var count = _screen.LiftWrites.Count;

        Frames(1f);

        Assert.AreEqual(count, _screen.LiftWrites.Count, "no write while the lift holds");
    }

    [TestMethod]
    public void AnOpenPanel_WithTheSeatTurnedOff_StillLiftsTheView_BecauseTheLiftHasItsOwnSetting()
    {
        _settings.Enabled = false;

        OpenAndLift();

        Assert.AreEqual(SeatPanelLift.FieldHeight, _screen.LiftWrites.Last());
    }

    [TestMethod]
    public void ClosingThePanel_SinksTheViewAndTheLastWriteIsExactlyZero()
    {
        OpenAndLift();
        _screen.RisePanelOpen = false;

        Frames(1f);

        Assert.AreEqual(0f, _screen.LiftWrites.Last(), "the target lock tests the offset for exact zero, so a small leftover would block it for the mission");
        var count = _screen.LiftWrites.Count;

        Frames(1f);

        Assert.AreEqual(count, _screen.LiftWrites.Count, "no write while the lift was and stays zero");
    }

    [TestMethod]
    public void WhileThePanelNeverOpens_NothingIsWritten_AndNoSettingIsRead()
    {
        Frames(2f);

        Assert.AreEqual(0, _screen.LiftWrites.Count);
        Assert.AreEqual(0, _settings.Reads);
        Assert.AreEqual(0, _screen.HeightReads);
    }

    [TestMethod]
    public void TheSettingAndThePermission_AreReadOnlyOnTheOpenEdge()
    {
        _screen.RisePanelOpen = true;

        Frames(2f);

        Assert.AreEqual(1, _settings.Reads);
        Assert.AreEqual(1, _screen.HeightReads);
    }

    [TestMethod]
    public void WithTheSettingOff_NothingLifts()
    {
        _settings.RiseWithPanel = false;

        OpenAndLift();

        Assert.AreEqual(0, _screen.LiftWrites.Count);
    }

    [TestMethod]
    public void WithoutTheOrdersPermission_NothingLifts_ReadWhenThePanelOpens()
    {
        _permissions = Allowed with { Orders = false };
        OpenAndLift();
        Assert.AreEqual(0, _screen.LiftWrites.Count);

        _screen.RisePanelOpen = false;
        Frames(0.1f);
        _permissions = Allowed;
        OpenAndLift();

        Assert.AreEqual(SeatPanelLift.FieldHeight, _screen.LiftWrites.Last(), "the permission was read fresh at the next open edge");
    }

    [TestMethod]
    public void ASiege_LiftsThreeMetres()
    {
        _screen.RiseHeight = SeatPanelLift.SiegeHeight;

        OpenAndLift();

        Assert.AreEqual(3f, _screen.LiftWrites.Last());
    }

    [TestMethod]
    public void AnyOtherMission_HasNoHeight_AndWritesNothing()
    {
        _screen.RiseHeight = 0f;

        OpenAndLift();

        Assert.AreEqual(0, _screen.LiftWrites.Count);
        Assert.AreEqual(0, _log.Lines.Count(l => l.Id == SeatChecks.RiseId));
    }

    [TestMethod]
    public void FirstPerson_DoesNotLift()
    {
        _screen.RiseFirstPerson = true;

        OpenAndLift();

        Assert.AreEqual(0, _screen.LiftWrites.Count);
    }

    [TestMethod]
    public void ASwitchToFirstPersonMidLift_SinksToExactZero()
    {
        OpenAndLift(0.3f);
        Assert.IsTrue(_screen.LiftWrites.Last() > 0f);

        _screen.RiseFirstPerson = true;
        Frames(1f);

        Assert.AreEqual(0f, _screen.LiftWrites.Last());
    }

    [TestMethod]
    public void OutsideBattleMode_DoesNotLift()
    {
        _screen.Facts = _screen.Facts with { BattleMode = false };

        OpenAndLift();

        Assert.AreEqual(0, _screen.LiftWrites.Count);
    }

    [TestMethod]
    public void AQuickCloseAndOpen_StartsFromTheCurrentValue_WithNoJump()
    {
        OpenAndLift(0.3f);
        _screen.RisePanelOpen = false;
        Frames(0.1f);
        _screen.RisePanelOpen = true;
        Frames(0.1f);

        var biggest = _screen.LiftWrites.Zip(_screen.LiftWrites.Skip(1), (a, b) => Math.Abs(b - a)).Max();

        Assert.IsTrue(biggest < 1.2f, "the largest single step was " + biggest);
    }

    // ---- The Seat ----

    [TestMethod]
    public void ThePanelOpenInTheSeat_DoesNotLift()
    {
        SeatUp();

        OpenAndLift();

        Assert.AreEqual(0, _screen.LiftWrites.Count, "the Seat itself does not use this");
    }

    [TestMethod]
    public void TakingTheSeat_ZeroesALift_WithTheExactZero()
    {
        OpenAndLift();
        Assert.IsTrue(_screen.LiftWrites.Last() > 0f);

        SeatUp();

        Assert.AreEqual(0f, _screen.LiftWrites.Last());
        var count = _screen.LiftWrites.Count;
        Frames(1f);
        Assert.AreEqual(count, _screen.LiftWrites.Count, "nothing more while the Seat is up");
    }

    [TestMethod]
    public void ALeaveWithThePanelStillOpen_LiftsAgainOnTheNextOpenEdge()
    {
        SeatUp();
        _screen.RisePanelOpen = true;
        Frames(0.5f);

        _sut.OnScreenTick(true);
        Frames(1f);

        Assert.AreEqual(SeatPanelLift.FieldHeight, _screen.LiftWrites.Last());
    }

    // ---- Fault and finish ----

    [TestMethod]
    public void AFault_WhileLifted_WritesTheExactZeroAtOnce()
    {
        OpenAndLift();
        Assert.AreEqual(SeatPanelLift.FieldHeight, _screen.LiftWrites.Last());
        _screen.PausedThrows = new InvalidOperationException("pause");

        _sut.PressTime(SeatTimeKey.Stay);

        Assert.IsTrue(_sut.Faulted);
        Assert.AreEqual(0f, _screen.LiftWrites.Last(), "the fault zeroes the offset");
    }

    [TestMethod]
    public void TheLiftOfAFaultedSeat_StillWorks_BecauseItDoesNotSitBehindTheFaultReturn()
    {
        SeatUp();
        _screen.ApplyThrows = new InvalidOperationException("camera");
        _sut.OnFrame(0.1f);
        Assert.IsTrue(_sut.Faulted);

        OpenAndLift();

        Assert.AreEqual(SeatPanelLift.FieldHeight, _screen.LiftWrites.Last());
    }

    [TestMethod]
    public void Finish_WritesNothing_BecauseTheOffsetLivesOnTheMissionInstance()
    {
        OpenAndLift();
        var count = _screen.LiftWrites.Count;

        _sut.Finish();

        Assert.AreEqual(count, _screen.LiftWrites.Count);
    }

    [TestMethod]
    public void ALiftThatThrows_FaultsTheSeatOnce_AndTheLiftReadsNoMore()
    {
        _screen.LiftThrows = new InvalidOperationException("offset");

        OpenAndLift();
        var reads = _screen.RiseReads;
        Frames(1f);

        Assert.IsTrue(_sut.Faulted);
        Assert.AreEqual(reads, _screen.RiseReads, "a lift that threw is off for the mission");
    }

    // ---- The check line ----

    [TestMethod]
    public void TheFirstLiftOfAMission_WritesOneRiseLine_WithTheHeight()
    {
        OpenAndLift();
        _screen.RisePanelOpen = false;
        Frames(1f);
        OpenAndLift();

        var lines = _log.Lines.Where(l => l.Id == SeatChecks.RiseId).ToList();
        Assert.AreEqual(1, lines.Count);
        Assert.AreEqual("mission=7 height=7.0", lines[0].Detail);
        Assert.AreEqual(1, _log.Calls("pass", SeatChecks.RiseId), "the call itself is made once, not once a lift");
    }
}
