using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Client.Adapters;
using TAOM.Client.Features.SeatOfSeeing;
using TAOM.Core.Logging;
using TAOM.Features.SeatOfSeeing;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Tests.Features.SeatOfSeeing;

/// <summary>The command mouse inside the Seat's state machine: when it is live, what the cursor and the shield do, and how a captured click reaches the engine.</summary>
[TestClass]
public class SeatSessionCommandTests
{
    private static readonly SeatPermissions Allowed = new(Camera: true, FightOn: true, Orders: true, Time: true, TakeSoldier: true);

    private FakeSeatScreen _screen = null!;
    private FakeSeatSettings _settings = null!;
    private FakeSeatCommand _engine = null!;
    private FakeSeatHero _hero = null!;
    private RecordingOutcomeLog _log = null!;
    private SeatPermissions _permissions;
    private bool _network;
    private SeatSession _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        SeatPresence.Set(false);
        _permissions = Allowed;
        _network = false;
        _screen = new FakeSeatScreen();
        _settings = new FakeSeatSettings { CommandMouse = true };
        _engine = new FakeSeatCommand { Selected = 1 };
        _hero = new FakeSeatHero();
        _log = new RecordingOutcomeLog();
        _sut = new SeatSession(_screen, _hero, _settings, () => _permissions, _log, Substitute.For<IModLogger>(), "7", _engine, () => _network);
        _sut.OnScreenTick(true);
        for (var i = 0; i < 20 && _sut.State != SeatState.Seated; i++)
            _sut.OnFrame(0.1f);
        Assert.AreEqual(SeatState.Seated, _sut.State);
    }

    [TestCleanup]
    public void Cleanup() => SeatPresence.Set(false);

    private void Frame() => _sut.OnScreenTick(false);

    private static SeatPointerFrame Click(bool shift = false) =>
        new(true, true, false, false, false, 400f, 300f, 0.4f, 0.3f, shift, false, 1f);

    // ---- live and the cursor ----

    [TestMethod]
    public void Seated_WithTheSettingOn_GoesLive_ShowsTheCursorAndRaisesTheShield()
    {
        Frame();

        Assert.IsTrue(_sut.CommandLive);
        Assert.IsTrue(SeatPresence.CommandMouse);
        Assert.IsTrue(SeatPresence.CursorShown, "the focus patch follows the pointer");
        CollectionAssert.AreEqual(new[] { true }, _screen.CommandWrites);
        Assert.IsTrue(_screen.CursorWrites.Contains(true));
    }

    [TestMethod]
    public void Seated_WithTheSettingOff_ShowsNoCursorWithoutThePanel_AsToday()
    {
        _settings.CommandMouse = false;

        Frame();
        Frame();

        Assert.IsFalse(_sut.CommandLive);
        Assert.IsFalse(SeatPresence.CommandMouse);
        Assert.IsFalse(SeatPresence.CursorShown);
        Assert.AreEqual(0, _screen.CommandWrites.Count);
        Assert.AreEqual(0, _screen.PointerReads, "the pointer is never read with the setting off");
    }

    [TestMethod]
    public void Seated_WhenTheMissionKindGivesNoOrders_StaysOff()
    {
        _permissions = Allowed with { Orders = false };

        Frame();

        Assert.IsFalse(_sut.CommandLive);
        Assert.IsFalse(SeatPresence.CursorShown);
    }

    [TestMethod]
    public void Seated_InANetworkSession_StaysOff()
    {
        _network = true;

        Frame();

        Assert.IsFalse(_sut.CommandLive);
        Assert.IsFalse(SeatPresence.CommandMouse);
    }

    [TestMethod]
    public void TurningTheSettingOff_WhileLive_DropsTheShieldTheCursorAndTheFlag()
    {
        Frame();
        _settings.CommandMouse = false;

        Frame();

        Assert.IsFalse(_sut.CommandLive);
        Assert.IsFalse(SeatPresence.CommandMouse);
        Assert.IsFalse(SeatPresence.CursorShown);
        CollectionAssert.AreEqual(new[] { true, false }, _screen.CommandWrites);
    }

    [TestMethod]
    public void LookingWithTheMiddleButton_HidesTheCursorButStaysLive()
    {
        Frame();
        _screen.Look = true;

        Frame();

        Assert.IsTrue(_sut.CommandLive);
        Assert.IsFalse(SeatPresence.CursorShown);
    }

    [TestMethod]
    public void Leaving_WhileLive_ClearsTheFlagTheShieldAndTheCursor()
    {
        Frame();

        _sut.OnScreenTick(true);

        Assert.AreEqual(SeatState.Off, _sut.State);
        Assert.IsFalse(SeatPresence.CommandMouse);
        Assert.IsFalse(SeatPresence.CursorShown);
        Assert.AreEqual(false, _screen.CommandWrites.Last());
    }

    [TestMethod]
    public void PhotoMode_WhileLive_ParksTheCommandMouseUntilItEnds()
    {
        Frame();
        _screen.Facts = _screen.Facts with { PhotoMode = true };

        Frame();

        Assert.IsFalse(_sut.CommandLive);
        Assert.IsFalse(SeatPresence.CursorShown);

        _screen.Facts = _screen.Facts with { PhotoMode = false };
        Frame();

        Assert.IsTrue(_sut.CommandLive);
    }

    [TestMethod]
    public void AFaultedSeat_ClearsTheCommandMouse()
    {
        Frame();
        _screen.Pointer = default;
        _screen.ApplyThrows = new System.InvalidOperationException("boom");

        _sut.OnFrame(0.02f);

        Assert.IsTrue(_sut.Faulted);
        Assert.IsFalse(SeatPresence.CommandMouse);
        Assert.AreEqual(false, _screen.CommandWrites.Last());
    }

    // ---- clicks ----

    [TestMethod]
    public void ACapturedClick_IsActedOnByTheNextMissionTickOnly()
    {
        _engine.Own = new SeatPicked(true, false);
        _screen.Pointer = Click();
        Frame();

        Assert.AreEqual(0, _engine.Count("pick-own"), "the screen tick only captures");

        _sut.OnMissionTick();
        _sut.OnMissionTick();

        Assert.AreEqual(1, _engine.Count("pick-own"));
        Assert.AreEqual(1, _engine.Count("select:ReplaceWith"));
    }

    [TestMethod]
    public void AMissionTickBeforeTheSeatIsSeated_ActsOnNothing()
    {
        // Rising: the command mouse is not live, so a stray capture never reaches the engine.
        var screen = new FakeSeatScreen { Pointer = Click() };
        var engine = new FakeSeatCommand { Own = new SeatPicked(true, false) };
        var session = new SeatSession(screen, new FakeSeatHero(), _settings, () => Allowed, new RecordingOutcomeLog(), Substitute.For<IModLogger>(), "8", engine);
        session.OnScreenTick(true);

        session.OnScreenTick(false);
        session.OnMissionTick();

        Assert.AreEqual(SeatState.Rising, session.State);
        Assert.AreEqual(0, engine.Calls.Count);
    }

    [TestMethod]
    public void NoClick_IsCapturedAsNothing_AndTheMissionTickDoesNoWork()
    {
        Frame();
        _sut.OnMissionTick();

        Assert.AreEqual(0, _engine.FactsReads);
    }

    [TestMethod]
    public void APausedMission_StillTakesTheClick()
    {
        // A pause ticks the mission with a dt of 0; the session's mission tick has no time in it.
        _engine.Own = new SeatPicked(true, false);
        _screen.Pointer = Click();

        Frame();
        _sut.OnMissionTick();

        Assert.AreEqual(1, _engine.Count("select:ReplaceWith"));
    }

    [TestMethod]
    public void WithThePanelOpen_BothThePanelRelayAndTheCommandMouseRead()
    {
        _screen.PanelOpen = true;
        _screen.Pointer = Click();

        Frame();

        Assert.AreEqual(1, _screen.Relays);
        Assert.AreEqual(1, _screen.PointerReads);
    }

    // ---- after the hero's fall ----

    [TestMethod]
    public void AfterTheHeroFell_TheCursorStays_AndAnOrderIsRefusedWithOneSkipLine()
    {
        Frame();
        _engine.Facts = _engine.Facts with { MainAgentExists = false };
        _screen.Pointer = new SeatPointerFrame(false, false, true, true, false, 500f, 300f, 0.5f, 0.3f, false, false, 1f);
        _engine.Probe = new SeatRightProbe(true, false, true);

        Frame();
        _sut.OnMissionTick();
        Frame();
        _sut.OnMissionTick();

        Assert.IsTrue(SeatPresence.CursorShown, "the pointer stays so the take can use it");
        Assert.IsTrue(_sut.CommandLive);
        Assert.AreEqual(0, _engine.Count("charge"));
        Assert.AreEqual(1, _log.Lines.Count(l => l.Result == "skip" && l.Id == SeatChecks.CommandId));
        StringAssert.Contains(_log.Lines.Single(l => l.Result == "skip" && l.Id == SeatChecks.CommandId).Detail, "hero-gone");
    }

    // ---- the checks ----

    [TestMethod]
    public void TheFirstSelectionAndTheFirstOrder_WriteOnePassLineEach()
    {
        _engine.Own = new SeatPicked(true, false);
        _engine.Probe = new SeatRightProbe(false, false, true);

        for (var i = 0; i < 3; i++)
        {
            _screen.Pointer = Click();
            Frame();
            _sut.OnMissionTick();
            _screen.Pointer = new SeatPointerFrame(false, false, true, true, false, 500f, 300f, 0.5f, 0.3f, false, false, 1f);
            Frame();
            _sut.OnMissionTick();
        }

        Assert.AreEqual(2, _log.Count("pass", SeatChecks.CommandId), "select and move, once each");
    }

    [TestMethod]
    public void TheCheckId_IsValid() => Assert.IsTrue(TAOM.Core.Logging.OutcomeLog.IsValidId(SeatChecks.CommandId));
}
