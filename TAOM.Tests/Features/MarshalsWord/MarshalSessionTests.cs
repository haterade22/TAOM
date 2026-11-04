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

    private MarshalSession NewSession(FakeMarshalOrders orders, FakeMarshalScreen screen) =>
        new(orders, screen, _settings, () =>
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
        var session = new MarshalSession(broken, _screen, _settings, () => true, _log, _logger, "8");
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
    public void OnScreenTick_AThrowWhileArming_LatchesTheFeatureOffToo()
    {
        var settings = Substitute.For<IMarshalSettingsProvider>();
        settings.Enabled.Returns(true);
        settings.KeepArray.Returns(_ => throw new InvalidOperationException("settings broke"));
        var session = new MarshalSession(_orders, _screen, settings, () => true, _log, _logger, "9");
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
        var session = new MarshalSession(broken, _screen, _settings, () => true, _log, _logger, "10");
        session.OnMissionTick();

        Assert.IsTrue(session.Faulted);
        Assert.IsFalse(MarshalHooks.IsLive);
    }

    // ---- both teardown paths ----

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
}
