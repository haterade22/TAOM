using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.MountAndBlade;
using TAOM.Adapters;
using TAOM.Client.Features.MarshalsWord;
using TAOM.Core.Logging;
using TAOM.Features.MarshalsWord;
using TAOM.Features.MarshalsWord.Domain;
using TAOM.Tests.Features.SeatOfSeeing;

namespace TAOM.Tests.Features.MarshalsWord;

/// <summary>
/// The session's life (build-spec section 2): when it goes live, what it arms, the one fault latch, and the finish that both teardown
/// paths share. It builds payloads from engine types, so it carries the same tag as the other tests that do.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class MarshalSessionTests
{
    private FakeMarshalOrders _orders = null!;
    private FakeMarshalScreen _screen = null!;
    private FakeMarshalSettings _settings = null!;
    private RecordingOutcomeLog _log = null!;
    private IModLogger _logger = null!;
    private int _mayRunCalls;
    private bool _mayRun;
    private MarshalSession _session = null!;

    [TestInitialize]
    public void Setup()
    {
        MarshalHooks.Clear();
        _orders = new FakeMarshalOrders();
        _screen = new FakeMarshalScreen();
        _settings = new FakeMarshalSettings();
        _log = new RecordingOutcomeLog();
        _logger = Substitute.For<IModLogger>();
        _mayRun = true;
        _mayRunCalls = 0;
        _session = NewSession(_orders, _screen);
    }

    [TestCleanup]
    public void Cleanup() => MarshalHooks.Clear();

    internal FakeMarshalFire Fire { get; } = new();

    internal FakeMarshalOutlines Outlines { get; } = new();

    private MarshalSession NewSession(FakeMarshalOrders orders, FakeMarshalScreen screen) =>
        new(orders, Fire, new FakeMarshalThrowers(), Outlines, screen, _settings, () =>
        {
            _mayRunCalls++;
            return _mayRun;
        }, _log, _logger, "7");

    private static MarshalOrderPayload Move() => MarshalOrderPayload.ForOrder(OrderType.Charge);

    // A table that names no formation is still a published table: the tests only ask whether it is still there.
    private static MarshalPaceTable ATable() => new(Array.Empty<Formation>(), Array.Empty<float>());

    private static MarshalScreenInput Frame(bool queue = false, bool widths = false, bool panel = true, float time = 0f) =>
        new(queue, widths, panel, time);

    private void GoLive()
    {
        _session.OnMissionTick();
        Assert.IsTrue(_session.IsLive, "the session should be live");
    }

    // ---- going live ----

    [TestMethod]
    public void OnMissionTick_FirstTick_WithAYesAndAPlayerTeam_GoesLive_AndGivesThePatchesItsController()
    {
        _session.OnMissionTick();

        Assert.IsTrue(_session.IsLive);
        Assert.IsTrue(MarshalHooks.IsLive);
        Assert.AreEqual(1, _orders.Attached);
        Assert.IsTrue(MarshalHooks.IsOwnedBy(_session));
    }

    [TestMethod]
    public void OnMissionTick_GoingLive_CopiesTheSettingsAtOnce_NotOnlyAtTheNextScreenTick()
    {
        _session.OnMissionTick();

        Assert.IsTrue(MarshalHooks.KeepArray);
        Assert.IsTrue(MarshalHooks.ArriveTogether);
        Assert.IsFalse(MarshalHooks.QueueArmed);
    }

    [TestMethod]
    public void OnMissionTick_BeforeTheFirstTick_NothingIsLive()
    {
        Assert.IsFalse(_session.IsLive);
        Assert.IsFalse(MarshalHooks.IsLive);
    }

    [TestMethod]
    public void OnMissionTick_TheSettingOff_NeverGoesLive_AndAsksNothingElse()
    {
        _settings.Enabled = false;

        _session.OnMissionTick();
        _session.OnMissionTick();

        Assert.IsFalse(_session.IsLive);
        Assert.AreEqual(0, _mayRunCalls, "no mission facts are read when the feature is off");
    }

    [TestMethod]
    public void OnMissionTick_TheSettingOff_WritesOneSkipLineOnTheCapturedCheck_SoTheManifestCanSayWhy()
    {
        _settings.Enabled = false;

        _session.OnMissionTick();
        _session.OnMissionTick();

        Assert.AreEqual(1, _log.Count("skip", MarshalChecks.CapturedId));
        StringAssert.Contains(_log.Lines.Single(l => l.Id == MarshalChecks.CapturedId).Detail, "reason=setting-off");
    }

    [TestMethod]
    public void OnMissionTick_AMissionThatMayNotRun_WritesOneSkipLineNamingTheRefusal()
    {
        _mayRun = false;

        _session.OnMissionTick();
        _session.OnMissionTick();

        Assert.AreEqual(1, _log.Count("skip", MarshalChecks.CapturedId));
        StringAssert.Contains(_log.Lines.Single(l => l.Id == MarshalChecks.CapturedId).Detail, "reason=mission-refused");
    }

    [TestMethod]
    public void OnMissionTick_AYes_WritesNoSkipLine()
    {
        _session.OnMissionTick();

        Assert.AreEqual(0, _log.Count("skip", MarshalChecks.CapturedId));
    }

    [TestMethod]
    public void OnMissionTick_AMissionThatMayNotRun_StaysOff_UntilAModeChangeAsksAgain()
    {
        _mayRun = false;
        _session.OnMissionTick();
        _session.OnMissionTick();
        Assert.IsFalse(_session.IsLive);
        Assert.AreEqual(1, _mayRunCalls, "the answer is kept until the next mode change");

        _mayRun = true;
        _session.OnModeChange();
        _session.OnMissionTick();

        Assert.IsTrue(_session.IsLive);
        Assert.AreEqual(2, _mayRunCalls);
    }

    [TestMethod]
    public void OnMissionTick_AYesWithNoPlayerTeamYet_GoesLiveWhenTheTeamAppears_WithNoModeChange()
    {
        _screen.PlayerTeamExists = false;
        _session.OnMissionTick();
        Assert.IsFalse(_session.IsLive);

        _screen.PlayerTeamExists = true;
        _session.OnMissionTick();

        Assert.IsTrue(_session.IsLive);
        Assert.AreEqual(1, _mayRunCalls, "the kind was asked once; only the team is tested again");
    }

    [TestMethod]
    public void OnModeChange_WhileLive_DoesNotAskAgain()
    {
        GoLive();

        _session.OnModeChange();
        _session.OnMissionTick();

        Assert.AreEqual(1, _mayRunCalls);
        Assert.IsTrue(_session.IsLive);
    }

    // ---- arming ----

    [TestMethod]
    public void OnScreenTick_TheQueueKeyDownWithThePanelOpen_ArmsTheQueue()
    {
        GoLive();

        _session.OnScreenTick(Frame(queue: true, panel: true));

        Assert.IsTrue(MarshalHooks.QueueArmed);
    }

    [TestMethod]
    public void OnScreenTick_TheQueueKeyDownWithThePanelClosed_ArmsNothing()
    {
        GoLive();

        _session.OnScreenTick(Frame(queue: true, widths: true, panel: false));

        Assert.IsFalse(MarshalHooks.QueueArmed);
        Assert.IsFalse(MarshalHooks.KeepWidthsHeld);
    }

    [TestMethod]
    public void OnScreenTick_ReleasingTheKey_DisarmsAtOnce()
    {
        GoLive();
        _session.OnScreenTick(Frame(queue: true, widths: true));

        _session.OnScreenTick(Frame());

        Assert.IsFalse(MarshalHooks.QueueArmed);
        Assert.IsFalse(MarshalHooks.KeepWidthsHeld);
    }

    [TestMethod]
    public void OnScreenTick_CopiesTheSettings_AndFollowsTheirChange()
    {
        GoLive();
        _session.OnScreenTick(Frame());
        Assert.IsTrue(MarshalHooks.KeepArray);
        Assert.IsTrue(MarshalHooks.ArriveTogether);

        _settings.KeepArray = false;
        _settings.ArriveTogether = false;
        _session.OnScreenTick(Frame());

        Assert.IsFalse(MarshalHooks.KeepArray);
        Assert.IsFalse(MarshalHooks.ArriveTogether);
    }

    [TestMethod]
    public void OnScreenTick_WhenNotLive_ArmsNothing()
    {
        _session.OnScreenTick(Frame(queue: true));

        Assert.IsFalse(MarshalHooks.QueueArmed);
    }

    // ---- capture ----

    [TestMethod]
    public void TryCapture_WhileLive_StoresTheOrder_AndTellsVanillaToStandDown()
    {
        GoLive();
        _orders.Selected.Add(1);

        var captured = _session.TryCapture(Move());

        Assert.IsTrue(captured);
        Assert.AreEqual(1, _session.Queue.CountOf(1));
    }

    [TestMethod]
    public void TryCapture_NothingSelected_AnswersNotQueued()
    {
        GoLive();

        Assert.IsFalse(_session.TryCapture(Move()));
    }

    [TestMethod]
    public void TryCapture_BeforeTheSessionIsLive_AnswersNotQueued()
    {
        _orders.Selected.Add(1);

        Assert.IsFalse(_session.TryCapture(Move()));
        Assert.AreEqual(0, _session.Queue.TotalSteps);
    }

    [TestMethod]
    public void TryCapture_AFullQueue_SwallowsTheOrder_AndSaysSo()
    {
        GoLive();
        _orders.Selected.Add(1);
        for (var i = 0; i < MarshalTuning.MaxStepsPerCompany; i++)
            _session.TryCapture(Move());

        var captured = _session.TryCapture(Move());

        Assert.IsTrue(captured, "a decline would run the order now and wipe the twelve steps");
        CollectionAssertSaid(MarshalMessage.QueueFull);
    }

    [TestMethod]
    public void TryCapture_TheOrderThrowsInsideTheCapture_AnswersNotQueued_AndLatchesTheFeatureOff()
    {
        var broken = Substitute.For<IMarshalOrderAdapter>();
        broken.SelectedCompanies().Returns(_ => throw new InvalidOperationException("selection broke"));
        var session = new MarshalSession(broken, new FakeMarshalFire(), new FakeMarshalThrowers(), new FakeMarshalOutlines(), _screen, _settings, () => true, _log, _logger, "8");
        session.OnMissionTick();
        Assert.IsTrue(session.IsLive);

        var captured = session.TryCapture(Move());

        Assert.IsFalse(captured, "vanilla must run the order");
        Assert.IsTrue(session.Faulted);
        Assert.IsFalse(MarshalHooks.IsLive);
    }

    // ---- the fault latch ----

    [TestMethod]
    public void OnMissionTick_AThrowInThePass_LatchesTheFeatureOff_ClearsTheHooks_AndWritesOneFailLine()
    {
        GoLive();
        _session.OnScreenTick(Frame(queue: true));
        _orders.FailOnNow = new InvalidOperationException("boom");

        _session.OnMissionTick();
        _session.OnMissionTick();

        Assert.IsTrue(_session.Faulted);
        Assert.IsFalse(_session.IsLive);
        Assert.IsFalse(MarshalHooks.IsLive);
        Assert.IsFalse(MarshalHooks.QueueArmed, "the statics clear with the latch");
        Assert.AreEqual(1, _log.Count("fail", MarshalChecks.FaultId));
        CollectionAssertSaid(MarshalMessage.Faulted);
    }

    [TestMethod]
    public void AFault_WithFlagsShown_HidesThemBeforeTheTeardownDropsThePool()
    {
        // A fault comes with the mission still running, so the flags stay in the scene unless something hides them (build-spec section 2).
        GoLive();
        _orders.Selected.Add(1);
        _session.Queue.Enqueue(new[] { 1 }, StepEnd.Arrival, new FakePayload("move") { HasPoint = true }, needsWaitTest: false);
        _session.OnScreenTick(Frame(panel: true, time: 1f));
        Assert.AreEqual(0, _screen.Hidden);
        _orders.FailOnNow = new InvalidOperationException("boom");

        _session.OnMissionTick();

        Assert.IsTrue(_session.Faulted);
        CollectionAssert.AreEqual(new[] { "hide", "drop" }, _screen.Calls);
    }

    [TestMethod]
    public void AFault_WithAPaceTablePublished_ReleasesTheSlowedCompanies()
    {
        GoLive();
        MarshalHooks.Paces = ATable();
        _orders.FailOnNow = new InvalidOperationException("boom");

        _session.OnMissionTick();

        Assert.IsTrue(_session.Faulted);
        Assert.IsNull(MarshalHooks.Paces, "a march must not keep a company slowed after the feature has faulted");
    }

    [TestMethod]
    public void AFault_WhenHidingTheFlagsThrows_StillTearsDown()
    {
        GoLive();
        _screen.HideThrows = new InvalidOperationException("scene gone");
        _orders.FailOnNow = new InvalidOperationException("boom");

        _session.OnMissionTick();

        Assert.IsTrue(_session.Faulted);
        Assert.IsFalse(MarshalHooks.IsLive);
        Assert.AreEqual(1, _screen.Dropped, "a throw while hiding does not stop the teardown");
    }

    [TestMethod]
    public void AFault_InTheScreenTick_WritesNoContour_AndTheNextMissionTickPaysTheClearOnce()
    {
        // Build-spec section 2: the screen tick writes only TAOM fields, and the fault path may hide flags, nothing more. A contour is an agent write.
        var settings = Substitute.For<IMarshalSettingsProvider>();
        settings.Enabled.Returns(true);
        var session = new MarshalSession(_orders, Fire, new FakeMarshalThrowers(), Outlines, _screen, settings, () => true, _log, _logger, "11");
        session.OnMissionTick();
        Assert.IsTrue(session.IsLive);
        settings.KeepArray.Returns(_ => throw new InvalidOperationException("settings broke"));

        session.OnScreenTick(Frame());

        Assert.IsTrue(session.Faulted);
        Assert.AreEqual(0, Outlines.Cleared, "no contour is written from the screen tick");
        Assert.AreEqual(0, Outlines.Dropped, "the dictionary is what remembers who wears a contour, so it stays until the clear");
        session.OnMissionTick();
        Assert.AreEqual(1, Outlines.Cleared);
        Assert.AreEqual(1, Outlines.Dropped);
        session.OnMissionTick();
        Assert.AreEqual(1, Outlines.Cleared, "owed once");
    }

    [TestMethod]
    public void AFault_InTheCapture_WritesNoContour_AndTheNextMissionTickPaysTheClear()
    {
        var broken = Substitute.For<IMarshalOrderAdapter>();
        broken.SelectedCompanies().Returns(_ => throw new InvalidOperationException("selection broke"));
        var session = new MarshalSession(broken, new FakeMarshalFire(), new FakeMarshalThrowers(), Outlines, _screen, _settings, () => true, _log, _logger, "12");
        session.OnMissionTick();
        Assert.IsTrue(session.IsLive);

        var captured = session.TryCapture(Move());

        Assert.IsFalse(captured);
        Assert.IsTrue(session.Faulted);
        Assert.AreEqual(0, Outlines.Cleared, "the capture runs in the screen tick");
        session.OnMissionTick();
        Assert.AreEqual(1, Outlines.Cleared);
        Assert.AreEqual(1, Outlines.Dropped);
    }

    [TestMethod]
    public void AFault_InTheScreenTick_ThenTheFinishBeforeAnyMissionTick_IsSilent_ItOnlyDropsTheDictionary()
    {
        var settings = Substitute.For<IMarshalSettingsProvider>();
        settings.Enabled.Returns(true);
        var session = new MarshalSession(_orders, Fire, new FakeMarshalThrowers(), Outlines, _screen, settings, () => true, _log, _logger, "13");
        session.OnMissionTick();
        settings.KeepArray.Returns(_ => throw new InvalidOperationException("settings broke"));
        session.OnScreenTick(Frame());

        session.Finish();
        session.OnMissionTick();

        Assert.AreEqual(0, Outlines.Cleared, "the finish makes no native call, and nothing is owed after it");
        Assert.AreEqual(1, Outlines.Dropped);
    }

    [TestMethod]
    public void AFault_InTheScreenTick_TurnsNoCallbackOff_AndTheNextMissionTickPaysTheFireReleaseOnce()
    {
        // The release is an agent write, so a fault in the screen tick owes it to the next mission tick, as the contour clear is owed.
        var settings = Substitute.For<IMarshalSettingsProvider>();
        settings.Enabled.Returns(true);
        var session = new MarshalSession(_orders, Fire, new FakeMarshalThrowers(), Outlines, _screen, settings, () => true, _log, _logger, "14");
        session.OnMissionTick();
        settings.KeepArray.Returns(_ => throw new InvalidOperationException("settings broke"));

        session.OnScreenTick(Frame());

        Assert.IsTrue(session.Faulted);
        Assert.AreEqual(0, Fire.Released, "no callback is turned off from the screen tick");
        Assert.AreEqual(0, Fire.Dropped, "the adapter's set is what remembers who holds the callback, so it stays until the release");
        session.OnMissionTick();
        Assert.AreEqual(1, Fire.Released);
        Assert.AreEqual(1, Fire.Dropped);
        session.OnMissionTick();
        Assert.AreEqual(1, Fire.Released, "owed once");
    }

    [TestMethod]
    public void AFault_InTheCapture_OwesTheFireReleaseToo()
    {
        var broken = Substitute.For<IMarshalOrderAdapter>();
        broken.SelectedCompanies().Returns(_ => throw new InvalidOperationException("selection broke"));
        var session = new MarshalSession(broken, Fire, new FakeMarshalThrowers(), Outlines, _screen, _settings, () => true, _log, _logger, "15");
        session.OnMissionTick();

        session.TryCapture(Move());

        Assert.IsTrue(session.Faulted);
        Assert.AreEqual(0, Fire.Released, "the capture runs in the screen tick");
        session.OnMissionTick();
        Assert.AreEqual(1, Fire.Released);
    }

    [TestMethod]
    public void AFault_InTheMissionTick_TurnsTheCallbacksOffAtOnce_ThenDrops()
    {
        GoLive();
        _orders.FailOnNow = new InvalidOperationException("boom");

        _session.OnMissionTick();

        Assert.IsTrue(_session.Faulted);
        Assert.AreEqual(1, Fire.Released, "the mission tick may write agents, so nothing is owed");
        Assert.AreEqual(1, Fire.Dropped);
    }

    [TestMethod]
    public void AFault_InTheScreenTick_ThenTheFinishBeforeAnyMissionTick_TurnsNothingOff_ItOnlyDrops()
    {
        var settings = Substitute.For<IMarshalSettingsProvider>();
        settings.Enabled.Returns(true);
        var session = new MarshalSession(_orders, Fire, new FakeMarshalThrowers(), Outlines, _screen, settings, () => true, _log, _logger, "16");
        session.OnMissionTick();
        settings.KeepArray.Returns(_ => throw new InvalidOperationException("settings broke"));
        session.OnScreenTick(Frame());

        session.Finish();
        session.OnMissionTick();

        Assert.AreEqual(0, Fire.Released, "the finish makes no native call, and nothing is owed after it");
        Assert.AreEqual(1, Fire.Dropped);
    }

    [TestMethod]
    public void AFault_WhenTheFireReleaseThrows_TheOutlineClearAndTheDropsStillRun()
    {
        Fire.ReleaseThrows = new InvalidOperationException("slot gone");
        GoLive();
        _orders.FailOnNow = new InvalidOperationException("boom");

        _session.OnMissionTick();

        Assert.AreEqual(1, Outlines.Cleared);
        Assert.AreEqual(1, Fire.Dropped);
        Assert.AreEqual(1, Outlines.Dropped);
    }

    [TestMethod]
    public void AFault_InTheScreenTick_WhenTheOwedReleaseThrows_NothingEscapesTheTick_AndTheDropRuns()
    {
        var session = ASessionFaultedInTheScreenTick("17");
        Fire.ReleaseThrows = new InvalidOperationException("slot gone");

        session.OnMissionTick();

        Assert.AreEqual(1, Fire.Released);
        Assert.AreEqual(1, Fire.Dropped, "the throw does not stop the drop");
        Assert.AreEqual(1, Outlines.Cleared);
    }

    [TestMethod]
    public void AFault_InTheScreenTick_WhenTheOwedClearThrows_TheReleaseIsStillPaid()
    {
        var session = ASessionFaultedInTheScreenTick("18");
        Outlines.ClearThrows = new InvalidOperationException("contour gone");

        session.OnMissionTick();

        Assert.AreEqual(1, Outlines.Cleared);
        Assert.AreEqual(1, Outlines.Dropped, "the throw does not stop the drop");
        Assert.AreEqual(1, Fire.Released, "the fire release is paid whatever the contour clear did");
        Assert.AreEqual(1, Fire.Dropped);
    }

    [TestMethod]
    public void AFault_InTheMissionTick_WhenTheOutlineClearThrows_TheReleaseIsStillPaid()
    {
        Outlines.ClearThrows = new InvalidOperationException("contour gone");
        GoLive();
        _orders.FailOnNow = new InvalidOperationException("boom");

        _session.OnMissionTick();

        Assert.AreEqual(1, Fire.Released);
        Assert.AreEqual(1, Fire.Dropped);
    }

    private MarshalSession ASessionFaultedInTheScreenTick(string key)
    {
        var settings = Substitute.For<IMarshalSettingsProvider>();
        settings.Enabled.Returns(true);
        var session = new MarshalSession(_orders, Fire, new FakeMarshalThrowers(), Outlines, _screen, settings, () => true, _log, _logger, key);
        session.OnMissionTick();
        settings.KeepArray.Returns(_ => throw new InvalidOperationException("settings broke"));
        session.OnScreenTick(Frame());
        Assert.IsTrue(session.Faulted);
        return session;
    }

    [TestMethod]
    public void OnScreenTick_AThrowWhileArming_LatchesTheFeatureOffToo()
    {
        var settings = Substitute.For<IMarshalSettingsProvider>();
        settings.Enabled.Returns(true);
        settings.KeepArray.Returns(_ => throw new InvalidOperationException("settings broke"));
        var session = new MarshalSession(_orders, new FakeMarshalFire(), new FakeMarshalThrowers(), new FakeMarshalOutlines(), _screen, settings, () => true, _log, _logger, "9");
        session.OnMissionTick();

        session.OnScreenTick(Frame());

        Assert.IsTrue(session.Faulted);
        Assert.AreEqual(1, _log.Count("fail", MarshalChecks.FaultId));
    }

    [TestMethod]
    public void AfterAFault_NoTickGoesLiveAgain_AndACaptureAnswersNotQueued()
    {
        GoLive();
        _orders.Selected.Add(1);
        _orders.FailOnNow = new InvalidOperationException("boom");
        _session.OnMissionTick();
        _orders.FailOnNow = null;

        _session.OnMissionTick();
        _session.OnScreenTick(Frame(queue: true));

        Assert.IsFalse(_session.IsLive);
        Assert.IsFalse(MarshalHooks.QueueArmed);
        Assert.IsFalse(_session.TryCapture(Move()));
    }

    [TestMethod]
    public void AFaultDuringTeardown_StillClearsTheHooks()
    {
        var broken = Substitute.For<IMarshalOrderAdapter>();
        broken.When(o => o.Detach()).Do(_ => throw new InvalidOperationException("detach broke"));
        broken.Now.Returns(_ => throw new InvalidOperationException("boom"));
        var session = new MarshalSession(broken, new FakeMarshalFire(), new FakeMarshalThrowers(), new FakeMarshalOutlines(), _screen, _settings, () => true, _log, _logger, "10");
        session.OnMissionTick();

        Assert.IsTrue(session.Faulted);
        Assert.IsFalse(MarshalHooks.IsLive);
    }

    // ---- both teardown paths ----

    [TestMethod]
    public void AnAdvanceTheOrderAdapterRaises_EndsTheThrowerRunAtTheNextPass_AndAnotherStartsForTheSameCompany()
    {
        var engine = new FakeMarshalThrowers();
        engine.Standard(1);
        var session = new MarshalSession(_orders, Fire, engine, Outlines, _screen, _settings, () => true, _log, _logger, "17");
        session.OnMissionTick();
        var first = session.Throwers.FactsOf(1)!.Value.Serial;

        _orders.RaiseAdvanceGiven(1);
        engine.Now = 0.25;
        session.OnMissionTick();

        _logger.Received().LogInfo(Arg.Is<string>(line => line.Contains("throwers company=1 state=released why=new-advance")));
        Assert.AreEqual(first + 1, session.Throwers.FactsOf(1)!.Value.Serial, "the player's new Advance is a new run");
    }

    [TestMethod]
    public void Finish_ClearsTheStatics_UnsubscribesTheEvents_AndDropsTheFlags()
    {
        GoLive();
        _session.OnScreenTick(Frame(queue: true, widths: true));
        MarshalHooks.Paces = ATable();
        Assert.IsTrue(_orders.HasDirectOrderListeners);

        _session.Finish();

        Assert.IsFalse(MarshalHooks.IsLive);
        Assert.IsFalse(MarshalHooks.QueueArmed);
        Assert.IsNull(MarshalHooks.PlayerController);
        Assert.IsNull(MarshalHooks.Paces);
        Assert.IsFalse(_orders.HasDirectOrderListeners, "no event handler outlives the session");
        Assert.IsFalse(_orders.HasAdvanceListeners, "the throwers hear the Advance event, and let go of it too");
        Assert.AreEqual(1, _orders.Detached);
        Assert.AreEqual(1, _screen.Dropped);
        Assert.AreEqual(0, _screen.Hidden, "the finish makes no native call: the flags go with the scene");
    }

    [TestMethod]
    public void Finish_CalledTwice_IsSafe_AndTheSecondDoesNothing()
    {
        GoLive();

        _session.Finish();
        _session.Finish();

        Assert.AreEqual(1, _orders.Detached);
        Assert.AreEqual(1, _screen.Dropped);
    }

    [TestMethod]
    public void Finish_ThenATick_NeverBeginsAgain()
    {
        _session.Finish();

        _session.OnMissionTick();
        _session.OnScreenTick(Frame(queue: true));

        Assert.IsFalse(_session.IsLive);
        Assert.IsFalse(MarshalHooks.IsLive);
        Assert.AreEqual(0, _orders.Attached, "a removed view's late tick must not start a session in a mission that is ending");
    }

    [TestMethod]
    public void Finish_ASessionThatNeverWentLive_IsSafe()
    {
        _session.Finish();

        Assert.AreEqual(1, _screen.Dropped);
        Assert.IsFalse(MarshalHooks.IsLive);
    }

    [TestMethod]
    public void Finish_ALateOldSession_DoesNotClearTheNextMissionsHooks()
    {
        GoLive();
        var nextOrders = new FakeMarshalOrders();
        var next = NewSession(nextOrders, new FakeMarshalScreen());
        MarshalHooks.Clear();
        next.OnMissionTick();
        Assert.IsTrue(MarshalHooks.IsOwnedBy(next));

        _session.Finish();

        Assert.IsTrue(MarshalHooks.IsLive, "the next mission's session stays live");
        Assert.IsTrue(MarshalHooks.IsOwnedBy(next));
    }

    // ---- events the engine raises ----

    [TestMethod]
    public void ADirectPlayerOrder_ClearsThatCompanysQueue()
    {
        GoLive();
        _orders.Selected.Add(1);
        _session.TryCapture(Move());
        Assert.AreEqual(1, _session.Queue.CountOf(1));

        _orders.RaiseDirectOrder(1);

        Assert.AreEqual(0, _session.Queue.CountOf(1));
    }

    [TestMethod]
    public void AChangeOfPlayerTeam_ClearsEveryQueue()
    {
        GoLive();
        _orders.Selected.AddRange(new[] { 1, 2 });
        _session.TryCapture(Move());

        _orders.RaiseTeamChanged();

        Assert.AreEqual(0, _session.Queue.TotalSteps);
    }

    [TestMethod]
    public void AChangeOfPlayerTeam_ClearsARunningMarch_BecauseItsCompaniesNameFormationsOfTheOldTeam()
    {
        GoLive();
        _orders.Legs = new[] { new MarchLeg(1, 100f, 4f, 0f), new MarchLeg(2, 50f, 4f, 0f) };
        _session.MarchRequested(new[] { new MarchTarget(1, 0f, 0f), new MarchTarget(2, 5f, 0f) });
        _orders.Now = 1.0;
        _session.OnMissionTick();
        Assert.AreEqual(2, _orders.Published.Last().Count, "the march is running and has published its paces");

        _orders.RaiseTeamChanged();

        Assert.AreEqual(0, _orders.Published.Last().Count, "the paces are cleared at once");
        StringAssert.Contains(_log.Lines.Single(l => l.Id == MarshalChecks.SpreadId).Detail, "reason=team-changed");
        var published = _orders.Published.Count;
        _orders.Now = 2.0;
        _session.OnMissionTick();
        Assert.AreEqual(published, _orders.Published.Count, "and the march does not run again against the new team's formations");
    }

    [TestMethod]
    public void AfterAnEvent_ThenFinish_TheEventNoLongerReachesTheSession()
    {
        GoLive();
        _orders.Selected.Add(1);
        _session.TryCapture(Move());
        _session.Finish();

        _orders.RaiseDirectOrder(1);

        Assert.AreEqual(1, _session.Queue.CountOf(1), "an unsubscribed session is not touched");
    }

    // ---- the hook sink ----

    [TestMethod]
    public void ArraySolved_WritesOneLinePerKind()
    {
        GoLive();

        _session.ArraySolved(click: true, companies: 3);
        _session.ArraySolved(click: false, companies: 2);

        Assert.AreEqual(2, _log.Count("pass", MarshalChecks.ArrayId));
        StringAssert.Contains(_log.Lines.First(l => l.Id == MarshalChecks.ArrayId).Detail, "kind=click companies=3");
    }

    [TestMethod]
    public void ArrayFault_WritesAFailLine_ButDoesNotEndTheSession()
    {
        GoLive();

        _session.ArrayFault(new InvalidOperationException("layout broke"));

        Assert.AreEqual(1, _log.Count("fail", MarshalChecks.ArrayId));
        Assert.IsTrue(_session.IsLive, "the takeover has its own latch; the queue keeps working");
        Assert.IsFalse(_session.Faulted);
    }

    [TestMethod]
    public void MarchRequested_FeedsTheMarchDriver_OnTheNextPass()
    {
        GoLive();
        _orders.Legs = new[] { new MarchLeg(1, 100f, 4f, 0f), new MarchLeg(2, 50f, 4f, 0f) };

        _session.MarchRequested(new[] { new MarchTarget(1, 0f, 0f), new MarchTarget(2, 5f, 0f) });
        _orders.Now = 1.0;
        _session.OnMissionTick();

        Assert.AreEqual(1, _log.Count("pass", MarshalChecks.MarchId));
        Assert.IsTrue(_orders.Published.Any(p => p.Count == 2));
    }

    [TestMethod]
    public void MarchRequested_WithTheSettingOff_StartsNoMarch()
    {
        GoLive();
        _settings.ArriveTogether = false;
        _session.OnScreenTick(Frame());
        _orders.Legs = new[] { new MarchLeg(1, 100f, 4f, 0f), new MarchLeg(2, 50f, 4f, 0f) };

        _session.MarchRequested(new[] { new MarchTarget(1, 0f, 0f), new MarchTarget(2, 5f, 0f) });
        _orders.Now = 1.0;
        _session.OnMissionTick();

        Assert.AreEqual(0, _log.Count("pass", MarshalChecks.MarchId));
    }

    // ---- the road ----

    [TestMethod]
    public void OnScreenTick_APanelOpenWithAQueuedStepThatHasAPoint_DrawsOneFlagAtIt()
    {
        GoLive();
        _orders.Selected.Add(1);
        var payload = new FakePayload("move") { HasPoint = true, X = 5f, Y = 6f, Z = 7f };
        _session.Queue.Enqueue(new[] { 1 }, StepEnd.Arrival, payload, needsWaitTest: false);

        _session.OnScreenTick(Frame(panel: true, time: 1f));

        var points = _screen.Shown.Last();
        Assert.AreEqual(1, points.Count);
        Assert.AreEqual(5f, points[0].X);
        Assert.AreEqual(7f, points[0].Z);
    }

    [TestMethod]
    public void OnScreenTick_AStepSharedByTwoSelectedCompanies_DrawsOneFlag()
    {
        GoLive();
        _orders.Selected.AddRange(new[] { 1, 2 });
        _session.Queue.Enqueue(new[] { 1, 2 }, StepEnd.Arrival, new FakePayload("move") { HasPoint = true }, needsWaitTest: false);

        _session.OnScreenTick(Frame(panel: true, time: 1f));

        Assert.AreEqual(1, _screen.Shown.Last().Count);
    }

    [TestMethod]
    public void OnScreenTick_APointOnlyForSelectedCompanies_AnUnselectedQueueDrawsNothing()
    {
        GoLive();
        _orders.Selected.Add(1);
        _session.Queue.Enqueue(new[] { 2 }, StepEnd.Arrival, new FakePayload("move") { HasPoint = true }, needsWaitTest: false);

        _session.OnScreenTick(Frame(panel: true, time: 1f));

        Assert.AreEqual(0, _screen.Shown.Last().Count);
    }

    [TestMethod]
    public void OnScreenTick_ThePanelClosing_HidesTheFlags_Once()
    {
        GoLive();
        _orders.Selected.Add(1);
        _session.Queue.Enqueue(new[] { 1 }, StepEnd.Arrival, new FakePayload("move") { HasPoint = true }, needsWaitTest: false);
        _session.OnScreenTick(Frame(panel: true, time: 1f));

        _session.OnScreenTick(Frame(panel: false, time: 1.1f));
        _session.OnScreenTick(Frame(panel: false, time: 1.2f));

        Assert.AreEqual(1, _screen.Hidden);
    }

    [TestMethod]
    public void OnScreenTick_APanelOpenForManyFrames_RedrawsOnlyOnAQueueChangeOrEveryQuarterSecond()
    {
        GoLive();
        _orders.Selected.Add(1);
        _session.OnScreenTick(Frame(panel: true, time: 1f));
        var drawn = _screen.Shown.Count;

        _session.OnScreenTick(Frame(panel: true, time: 1.1f));
        Assert.AreEqual(drawn, _screen.Shown.Count, "nothing changed within a quarter second");

        _session.Queue.Enqueue(new[] { 1 }, StepEnd.Arrival, new FakePayload("move") { HasPoint = true }, needsWaitTest: false);
        _session.OnScreenTick(Frame(panel: true, time: 1.12f));
        Assert.AreEqual(drawn + 1, _screen.Shown.Count, "a queue change redraws at once");

        _session.OnScreenTick(Frame(panel: true, time: 1.5f));
        Assert.AreEqual(drawn + 2, _screen.Shown.Count, "and the road refreshes at 4 Hz");
    }

    [TestMethod]
    public void OnScreenTick_EmptyQueue_DrawsOnceThenStops()
    {
        GoLive();
        _orders.Selected.Add(1);

        _session.OnScreenTick(Frame(panel: true, time: 1f));
        Assert.AreEqual(1, _screen.Shown.Count, "the first frame with the panel open draws the road, empty");
        _session.OnScreenTick(Frame(panel: true, time: 1.5f));
        _session.OnScreenTick(Frame(panel: true, time: 2f));
        _session.OnScreenTick(Frame(panel: true, time: 9f));

        Assert.AreEqual(1, _screen.Shown.Count, "nothing is queued, so the quarter-second refresh has nothing to draw");
    }

    [TestMethod]
    public void OnScreenTick_TheQueueEmptyingAgain_RedrawsOnceMoreAndThenStops()
    {
        GoLive();
        _orders.Selected.Add(1);
        var step = _session.Queue.Enqueue(new[] { 1 }, StepEnd.Arrival, new FakePayload("move") { HasPoint = true }, needsWaitTest: false);
        _session.OnScreenTick(Frame(panel: true, time: 1f));
        Assert.AreEqual(1, _screen.Shown.Last().Count);

        _session.Queue.Start(step, 1.0, 30.0);
        _session.Queue.Finish(step, 1);
        _session.OnScreenTick(Frame(panel: true, time: 1.01f));
        var drawn = _screen.Shown.Count;
        _session.OnScreenTick(Frame(panel: true, time: 5f));

        Assert.AreEqual(0, _screen.Shown.Last().Count, "the flag of the finished step is gone");
        Assert.AreEqual(drawn, _screen.Shown.Count, "and an empty queue is not redrawn every quarter second");
    }

    private void CollectionAssertSaid(MarshalMessage message) =>
        Assert.IsTrue(_screen.Said.Contains(message), "expected the message " + message + " but got " + string.Join(",", _screen.Said));

    // ---- stage M3 and M4: the pressed keys, the mark and the outlines ----

    private static MarshalScreenInput Keys(bool mark = false, bool queue = false, bool panel = true, bool indicators = false, bool fire = false,
        bool word = false) => new(queue, false, panel, 0f, mark, indicators, fire, word);

    private static MarshalOrderPayload ChargeOnACompany() =>
        MarshalOrderPayload.ForFormation(OrderType.Charge, (Formation)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Formation)));

    [TestMethod]
    public void OnScreenTick_TheMarkKeyDownWithThePanelOpen_ArmsTheMark_AndWithItClosedArmsNothing()
    {
        GoLive();

        _session.OnScreenTick(Keys(mark: true));
        Assert.IsTrue(MarshalHooks.MarkArmed);

        _session.OnScreenTick(Keys(mark: true, panel: false));
        Assert.IsFalse(MarshalHooks.MarkArmed);
    }

    [TestMethod]
    public void OnScreenTick_AFirePress_ChangesNothingUntilTheNextMissionTick()
    {
        GoLive();
        _orders.Selected.Add(1);

        _session.OnScreenTick(Keys(fire: true));
        Assert.AreEqual(FireMode.Free, MarshalFireTable.ModeOf(1), "the screen tick only stores the press");

        _session.OnMissionTick();
        Assert.AreEqual(FireMode.OnWord, MarshalFireTable.ModeOf(1));
        CollectionAssert.AreEqual(new[] { MarshalMessage.FireOnWord }, _screen.Said);
    }

    [TestMethod]
    public void OnScreenTick_AWordPress_GivesTheWordAtTheNextMissionTick()
    {
        GoLive();
        _orders.Selected.Add(1);
        _session.OnScreenTick(Keys(fire: true));
        _session.OnMissionTick();
        _orders.Now = 2.0;

        _session.OnScreenTick(Keys(word: true));
        _session.OnMissionTick();

        Assert.AreEqual(2.0, MarshalFireTable.WordTimeOf(1));
    }

    [TestMethod]
    public void OnMissionTick_PassesTheOutlinesWhetherTheyShouldShow_FromThePanelOrTheIndicatorsKey()
    {
        GoLive();

        _session.OnScreenTick(Keys(panel: true));
        _session.OnMissionTick();
        _session.OnScreenTick(Keys(panel: false, indicators: true));
        _session.OnMissionTick();
        _session.OnScreenTick(Keys(panel: false));
        _session.OnMissionTick();

        CollectionAssert.AreEqual(new[] { true, true, false }, Outlines.Shows.Skip(Outlines.Shows.Count - 3).ToArray());
    }

    // The outlines as the real service plans them, over an adapter that records each paint and clear.
    private sealed class RecordingOutlineAdapter : IMarshalOutlineAdapter<int>
    {
        internal readonly System.Collections.Generic.Dictionary<int, uint> Wanted = new();
        internal readonly System.Collections.Generic.List<(int Unit, uint? Colour)> Painted = new();

        public void ReadWanted(System.Collections.Generic.Dictionary<int, uint> into)
        {
            foreach (var pair in Wanted)
                into[pair.Key] = pair.Value;
        }

        public void Paint(int unit, uint? colour) => Painted.Add((unit, colour));
    }

    [TestMethod]
    public void OnMissionTick_TheOutlinesSettingFlippedOffInABattle_ClearsEverythingPaintedInOnePass_WithThePanelStillOpen()
    {
        // Build-spec 8.2: "A test covers the panel closing and the setting going off in a battle." The panel test is in MarshalMarkAndOutlineTests.
        var adapter = new RecordingOutlineAdapter
        {
            Wanted = { [1] = MarshalTuning.SelectedOutline, [2] = MarshalTuning.SelectedOutline, [3] = MarshalTuning.MarkedOutline },
        };
        var service = new MarshalOutlineService<int>(adapter);
        var session = new MarshalSession(_orders, Fire, new FakeMarshalThrowers(), service, _screen, _settings, () => true, _log, _logger, "14");
        session.OnMissionTick();
        _orders.Now = 1.0;
        session.OnScreenTick(Keys(panel: true));
        session.OnMissionTick();
        Assert.AreEqual(3, service.PaintedCount, "painted while the setting holds and the panel is open");
        adapter.Painted.Clear();

        _settings.Outlines = false;
        _orders.Now = 1.25;
        session.OnScreenTick(Keys(panel: true));
        session.OnMissionTick();

        Assert.AreEqual(0, service.PaintedCount, "one mission tick after the flip, nothing is left painted");
        CollectionAssert.AreEquivalent(new[] { (1, (uint?)null), (2, (uint?)null), (3, (uint?)null) }, adapter.Painted);
        Assert.IsTrue(MarshalHooks.IsLive, "turning the setting off hides the outlines and leaves the rest of the feature running");
    }

    [TestMethod]
    public void OnMissionTick_TheOutlinesSettingOff_NeverShows_EvenWithThePanelOpen()
    {
        _settings.Outlines = false;
        GoLive();

        _session.OnScreenTick(Keys(panel: true, indicators: true));
        _session.OnMissionTick();

        Assert.IsFalse(Outlines.Shows.Last());
    }

    [TestMethod]
    public void TryCapture_TheMarkKeyAlone_AndAnOrderThatIsNotACharge_AnswersNotQueued_SoVanillaRunsIt()
    {
        GoLive();
        _orders.Selected.Add(1);
        _session.OnScreenTick(Keys(mark: true));

        var captured = _session.TryCapture(Move());

        Assert.IsFalse(captured);
        Assert.AreEqual(0, _session.Queue.TotalSteps);
    }

    [TestMethod]
    public void TryCapture_TheMarkKeyAndAChargeOnACompany_StoresAMark_AppliedAtTheNextMissionTick_WithNoQueueStep()
    {
        GoLive();
        _orders.Selected.Add(1);
        _session.OnScreenTick(Keys(mark: true));

        var captured = _session.TryCapture(ChargeOnACompany());
        _session.OnMissionTick();

        Assert.IsTrue(captured, "vanilla must not run the charge");
        Assert.AreEqual(0, _session.Queue.TotalSteps);
        Assert.IsTrue(((MarshalOrderPayload)_orders.Issued.Single().Payload).IsMark);
        CollectionAssert.AreEqual(new[] { 1 }, _orders.Issued.Single().Companies);
    }

    [TestMethod]
    public void TryCapture_TheMarkKeyAndAChargeWithNoCompanyUnderTheirControl_AnswersNotQueued_SoTheClickIsNotSwallowed()
    {
        GoLive();
        _orders.Selected.Add(1);
        _orders.Lost.Add(1);
        _session.OnScreenTick(Keys(mark: true));

        Assert.IsFalse(_session.TryCapture(ChargeOnACompany()));
    }

    [TestMethod]
    public void TryCapture_BothKeys_QueuesAMarkStepThatEndsAtOnce_AndIssuesNothingYet()
    {
        GoLive();
        _orders.Selected.Add(1);
        _session.OnScreenTick(Keys(mark: true, queue: true));

        var captured = _session.TryCapture(ChargeOnACompany());

        Assert.IsTrue(captured);
        Assert.AreEqual(1, _session.Queue.StepsOf(1).Count);
        Assert.AreEqual(StepEnd.AtOnce, _session.Queue.StepsOf(1)[0].End);
        Assert.IsTrue(((MarshalOrderPayload)_session.Queue.StepsOf(1)[0].Payload!).IsMark);
        Assert.AreEqual(0, _orders.Issued.Count);
    }

    [TestMethod]
    public void ArraySolved_CountsEachCommit()
    {
        GoLive();

        _session.ArraySolved(click: true, companies: 2);
        _session.ArraySolved(click: false, companies: 2);

        Assert.AreEqual(2, _session.ArraysSolved);
    }

    [TestMethod]
    public void OnMissionTick_AThrowInThePass_ClearsTheOutlinesBeforeTheTeardown()
    {
        GoLive();

        _orders.FailOnNow = new InvalidOperationException("boom");
        _session.OnMissionTick();

        Assert.AreEqual(1, Outlines.Cleared);
        Assert.AreEqual(1, Outlines.Dropped);
        Assert.AreEqual(1, Fire.Dropped);
    }

    [TestMethod]
    public void Finish_DropsTheOutlinesAndTheSoldiersWithNoNativeClear_AndTwiceIsTheSame()
    {
        GoLive();

        _session.Finish();
        _session.Finish();

        Assert.AreEqual(0, Outlines.Cleared, "the finish makes no native call");
        Assert.AreEqual(1, Outlines.Dropped);
        Assert.AreEqual(1, Fire.Dropped);
        Assert.AreEqual(FireMode.Free, MarshalFireTable.ModeOf(1));
    }

    [TestMethod]
    public void Finish_OfASessionThatDoesNotOwnTheHooks_LeavesTheNextMissionsFireTableAlone()
    {
        GoLive();
        var next = NewSession(new FakeMarshalOrders(), new FakeMarshalScreen());
        next.OnMissionTick();
        MarshalFireTable.SetMode(2, FireMode.OnWord);

        _session.Finish();

        Assert.AreEqual(FireMode.OnWord, MarshalFireTable.ModeOf(2));
    }

    [TestMethod]
    public void TryCapture_BothKeysAndOneSelectedCompanyUnderTheAi_StillMarksTheOthers_SoTheMarkNeverFallsBackToACharge()
    {
        GoLive();
        _orders.Selected.AddRange(new[] { 1, 2 });
        _orders.Lost.Add(2);
        _session.OnScreenTick(Keys(mark: true, queue: true));

        var captured = _session.TryCapture(ChargeOnACompany());
        _session.OnMissionTick();

        Assert.IsTrue(captured, "the queue declines an AI-controlled selection, and the mark still must not run vanilla's charge");
        Assert.AreEqual(0, _session.Queue.TotalSteps);
        CollectionAssert.AreEqual(new[] { 1 }, _orders.Issued.Single().Companies);
    }

    // ---- the panel's entries ----

    [TestMethod]
    public void PanelPress_WhileLive_StoresTheClicksCompanies_AndTheNextMissionTickAppliesThem()
    {
        GoLive();

        _session.PanelPress(PanelAction.Fire, new[] { 4 });

        Assert.AreEqual(FireMode.Free, MarshalFireTable.ModeOf(4), "an engine write waits for the mission tick");
        _session.OnMissionTick();
        Assert.AreEqual(FireMode.OnWord, MarshalFireTable.ModeOf(4));
    }

    [TestMethod]
    public void PanelPress_TheWordAndTheHold_ReachTheirOwnPresses()
    {
        GoLive();
        MarshalFireTable.SetMode(4, FireMode.OnWord);

        _session.PanelPress(PanelAction.Word, new[] { 4 });
        _session.PanelPress(PanelAction.Hold, new[] { 4 });
        _orders.Now = 5.0;
        _session.OnMissionTick();

        Assert.AreEqual(5.0, MarshalFireTable.WordTimeOf(4));
        Assert.IsTrue(MarshalFireTable.HoldOf(4));
    }

    [TestMethod]
    public void PanelPress_BeforeTheSessionIsLive_StoresNothing()
    {
        _session.PanelPress(PanelAction.Fire, new[] { 4 });
        GoLive();
        _session.OnMissionTick();

        Assert.AreEqual(FireMode.Free, MarshalFireTable.ModeOf(4));
    }

    [TestMethod]
    public void PanelPress_AfterTheFinish_StoresNothing()
    {
        GoLive();
        _session.Finish();

        _session.PanelPress(PanelAction.Hold, new[] { 4 });

        Assert.IsFalse(MarshalFireTable.HoldOf(4));
    }
}
