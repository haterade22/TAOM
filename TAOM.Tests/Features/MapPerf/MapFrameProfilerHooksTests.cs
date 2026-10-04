using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem;
using TAOM.Core.Logging;
using TAOM.Features.BattleLoadDiagnostics;
using TAOM.Features.MapPerf;
using TAOM.Features.MapPerf.Hooks;
using TAOM.Features.TimeAcceleration;

namespace TAOM.Tests.Features.MapPerf;

/// <summary>
/// The Patch101 hooks: the timed TickEvent dispatch, the phase and view helpers, and the session logic behind
/// the frame boundary (<see cref="MapSessionHooks.Step"/>), driven with plain objects standing for campaigns
/// and synthetic ticks (a window every 1000). Invokes a real <see cref="MbEvent{T}"/>, so it needs the game
/// assemblies.
/// </summary>
[TestCategory("RequiresGame")]
[TestClass]
public class MapFrameProfilerHooksTests
{
    private sealed class OwnerA { }

    private sealed class OwnerB { }

    private sealed class ProbeView { }

    private IModLogger _logger = null!;
    private IBattleLoadDiagnosticsSettingsProvider _settings = null!;
    private ITimeControlAdapter _time = null!;
    private readonly object _campaignA = new object();
    private readonly object _campaignB = new object();

    private static MapFrameProfiler Profiler => MapFrameProfilerHooks.Profiler!;

    [TestInitialize]
    public void Setup()
    {
        MapFrameProfilerHooks.Profiler = new MapFrameProfiler(1000, windowSeconds: 1);
        _logger = Substitute.For<IModLogger>();
        MapFrameProfilerHooks.Logger = _logger;
        _settings = Substitute.For<IBattleLoadDiagnosticsSettingsProvider>();
        _settings.MapProfilerEnabled.Returns(true);
        _settings.TickProfilerTopN.Returns(8);
        var acceleration = Substitute.For<ITimeAccelerationSettingsProvider>();
        acceleration.FastForwardMultiplier.Returns(4);
        acceleration.ExtraFastForwardMultiplier.Returns(8);
        acceleration.CtrlSpaceMultiplier.Returns(16);
        _time = Substitute.For<ITimeControlAdapter>();
        _time.TimeControlMode.Returns(2);
        _time.SimplifiedTimeControlMode.Returns(2);
        _time.SpeedUpMultiplier.Returns(4f);
        MapSessionHooks.Settings = _settings;
        MapSessionHooks.TimeControl = _time;
        MapSessionHooks.Acceleration = acceleration;
        MapSessionHooks.PartyCount = () => 2091;
        MapSessionHooks.RawTopN = () => null;
        Assert.IsTrue(TickEventListenerWalker.TryBind(out var failure), failure);
    }

    [TestCleanup]
    public void Cleanup()
    {
        MapSessionHooks.ResetForTests();
        MapFrameProfilerHooks.Profiler = null;
        MapFrameProfilerHooks.Logger = null;
        MapFrameProfilerHooks.TickEventSites = 0;
    }

    private static void Step(object campaign, long now, MapSkip skip = MapSkip.None) =>
        MapSessionHooks.Step(campaign, now, 0, skip);

    // TAOM's application tick through the real prefix and postfix helpers: the only feed of the continuity count.
    private void CloseFrame(object campaign, long now)
    {
        MapFrameProfilerHooks.EndAppTick(MapFrameProfilerHooks.BeginPhase());
        Step(campaign, now);
    }

    private string[] InfoLines() => _logger.ReceivedCalls()
        .Where(c => c.GetMethodInfo().Name == nameof(IModLogger.LogInfo))
        .Select(c => (string)c.GetArguments()[0]!)
        .ToArray();

    // Every logger call in order, as "LogInfo: <line>", "LogWarning: <line>" or "LogError: <line>".
    private string[] Calls() => _logger.ReceivedCalls()
        .Select(c => c.GetMethodInfo().Name + ": " + (string)c.GetArguments()[0]!)
        .ToArray();

    private static MbEvent<float> TwoListeners(Action onA, Action onB)
    {
        var evt = new MbEvent<float>();
        evt.AddNonSerializedListener(new OwnerA(), _ => onA());
        evt.AddNonSerializedListener(new OwnerB(), _ => onB());
        return evt;
    }

    [TestMethod]
    public void TimedTickEvent_NotMeasuring_InvokesEveryListenerAndRecordsNothing()
    {
        var calls = 0;

        MapFrameProfilerHooks.TimedTickEvent(TwoListeners(() => calls++, () => calls++), 0.1f);

        Assert.IsFalse(Profiler.Measuring);
        Assert.AreEqual(2, calls);
        Assert.AreEqual(0, Profiler.Entries.FrameTop(8, 1000).Count);
    }

    [TestMethod]
    public void TimedTickEvent_Measuring_TimesTheDispatchAndEachOwner()
    {
        Step(_campaignA, 0);

        MapFrameProfilerHooks.TimedTickEvent(TwoListeners(() => { }, () => { }), 0.1f);
        CloseFrame(_campaignA, 10);
        var w = Profiler.TakeWindow(10, 8, 0, 0, 0);

        CollectionAssert.AreEquivalent(new[] { nameof(OwnerA), nameof(OwnerB) }, w.Top.Select(t => t.Name).ToArray());
        Assert.IsTrue(w.Top.All(t => t.Calls == 1));
        Assert.IsTrue(w.TickEventMs >= w.Top.Sum(t => t.Ms), "The dispatch bracket encloses every listener call.");
    }

    [TestMethod]
    public void TimedTickEvent_WalkerUnbound_InvokesThroughAndStillTimesTheDispatch()
    {
        TickEventListenerWalker.Unbind();
        Step(_campaignA, 0);
        var calls = 0;

        MapFrameProfilerHooks.TimedTickEvent(TwoListeners(() => calls++, () => { calls++; Thread.Sleep(2); }), 0.1f);
        CloseFrame(_campaignA, 10);
        var w = Profiler.TakeWindow(10, 8, 0, 0, 0);

        Assert.AreEqual(2, calls);
        Assert.AreEqual(0, w.Top.Count);
        Assert.IsTrue(w.TickEventMs > 0, "The whole dispatch is still timed.");
    }

    [TestMethod]
    public void EndPhase_StartZero_RecordsNothing()
    {
        Step(_campaignA, 0);

        MapFrameProfilerHooks.EndPhase(MapPhase.MapState, 0);
        CloseFrame(_campaignA, 10);

        var w = Profiler.TakeWindow(10, 8, 0, 0, 0);
        Assert.AreEqual(1, w.Frames);
        Assert.AreEqual(0, w.MapStateMs);
    }

    [TestMethod]
    public void EndPhase_Measuring_AddsThePhaseToTheClosedFrame()
    {
        Step(_campaignA, 0);

        var start = MapFrameProfilerHooks.BeginPhase();
        Thread.Sleep(2);
        MapFrameProfilerHooks.EndPhase(MapPhase.MapState, start);
        CloseFrame(_campaignA, 10);

        Assert.AreNotEqual(0L, start);
        Assert.IsTrue(Profiler.TakeWindow(10, 8, 0, 0, 0).MapStateMs > 0);
    }

    [TestMethod]
    public void IsSettled_NotMeasuringSameCampaign_TrueAndANewCampaignIsNeverSkipped()
    {
        _settings.MapProfilerEnabled.Returns(false);
        Assert.IsFalse(MapSessionHooks.IsSettled(Profiler, _campaignA), "No session is open yet.");

        Step(_campaignA, 0);

        Assert.IsTrue(MapSessionHooks.IsSettled(Profiler, _campaignA));
        Assert.IsFalse(MapSessionHooks.IsSettled(Profiler, _campaignB), "A new campaign must reach Step to open its session.");
    }

    [TestMethod]
    public void IsSettled_Measuring_False()
    {
        Step(_campaignA, 0);

        Assert.IsFalse(MapSessionHooks.IsSettled(Profiler, _campaignA));
    }

    [TestMethod]
    public void RecordView_Measuring_RecordsAnEntryForTheViewTypeAsTaomOwned()
    {
        Step(_campaignA, 0);

        var stamp = MapFrameProfilerHooks.BeginProbe();
        MapFrameProfilerHooks.RecordView(new ProbeView(), stamp);
        CloseFrame(_campaignA, 10);
        var w = Profiler.TakeWindow(10, 8, 0, 0, 0);

        Assert.AreEqual(1, w.Top.Count);
        Assert.AreEqual(nameof(ProbeView), w.Top[0].Name);
        Assert.AreEqual(1, w.Top[0].Calls);
        Assert.AreEqual(w.Top[0].Ms + w.AppTickMs, w.TaomMs, 1e-9);
    }

    [TestMethod]
    public void Step_FirstBoundaryOfACampaign_OpensASessionAndLogsTheHeader()
    {
        Step(_campaignA, 0);

        _logger.Received(1).LogInfo(MapProfileLines.BuildSessionStartLine(1, 8, 4, 8));
        Assert.IsTrue(Profiler.Measuring);
        Assert.AreEqual(1, InfoLines().Length);
    }

    [TestMethod]
    public void Step_ToggleOffAtSessionStart_LogsNotMeasuringAndRecordsNothing()
    {
        _settings.MapProfilerEnabled.Returns(false);

        Step(_campaignA, 0);
        CloseFrame(_campaignA, 10);
        CloseFrame(_campaignA, 2000);

        _logger.Received(1).LogInfo(MapProfileLines.BuildSessionOffLine(1));
        Assert.IsFalse(Profiler.Measuring);
        Assert.AreEqual(1, InfoLines().Length, "No window line from a session that does not measure.");
    }

    [TestMethod]
    public void Step_AnotherCampaign_WritesThePreviousSummaryThenOpensTheNext()
    {
        Step(_campaignA, 0);
        CloseFrame(_campaignA, 10);
        CloseFrame(_campaignA, 20);

        Step(_campaignB, 30);

        var lines = InfoLines();
        Assert.AreEqual(3, lines.Length, string.Join("\n", lines));
        Assert.AreEqual(MapProfileLines.BuildSessionStartLine(1, 8, 4, 8), lines[0]);
        StringAssert.StartsWith(lines[1], "[MapProfileSummary] reason=newCampaign session=1 frames=2 ");
        Assert.AreEqual(MapProfileLines.BuildSessionStartLine(2, 8, 4, 8), lines[2]);
        Assert.IsTrue(Profiler.Measuring);
        Assert.AreEqual(2, Profiler.Session);
    }

    [TestMethod]
    public void Step_WindowDue_LogsOneMapProfileLine()
    {
        Step(_campaignA, 0);
        CloseFrame(_campaignA, 600);
        CloseFrame(_campaignA, 1200);

        var windows = InfoLines().Where(l => l.StartsWith("[MapProfile] ", StringComparison.Ordinal)).ToArray();
        Assert.AreEqual(1, windows.Length);
        StringAssert.StartsWith(windows[0], "[MapProfile] t=+");
        StringAssert.Contains(windows[0], " parties=2091 ");
        StringAssert.Contains(windows[0], " frames=2 ");
    }

    // FOR-MIKE 16r (Codex P2): a Stoppable mode while the main party waits gives TickMapTime no campaign time, and
    // the engine's own simplification (Campaign.GetSimplifiedTimeControlMode) reads it as Stop. The class comes
    // from that, never from the raw mode (4 below), so waiting frames do not pose as FF frames.
    [TestMethod]
    public void Step_StoppableFastForwardWhileTheMainPartyWaits_ClassesTheFramesAsStop()
    {
        _time.TimeControlMode.Returns(4);
        _time.SimplifiedTimeControlMode.Returns(0);
        Step(_campaignA, 0);
        CloseFrame(_campaignA, 600);
        CloseFrame(_campaignA, 1200);

        MapSessionHooks.EndSession("gameEnd");

        var lines = InfoLines();
        StringAssert.Contains(lines.Single(l => l.StartsWith("[MapProfile] ", StringComparison.Ordinal)), " speed=Stop ");
        StringAssert.Contains(lines.Single(l => l.StartsWith("[MapProfileSummary] ", StringComparison.Ordinal)), " byspeed=Stop:2/1200.00 ");
    }

    [TestMethod]
    public void Step_StoppablePlayWhileTheMainPartyWaits_ClassesTheFrameAsStop()
    {
        _time.TimeControlMode.Returns(3);
        _time.SimplifiedTimeControlMode.Returns(0);
        Step(_campaignA, 0);
        CloseFrame(_campaignA, 10);

        MapSessionHooks.EndSession("gameEnd");

        StringAssert.Contains(InfoLines().Single(l => l.StartsWith("[MapProfileSummary] ", StringComparison.Ordinal)), " byspeed=Stop:1/");
    }

    [TestMethod]
    public void Step_StoppableFastForwardWhileTheMainPartyMoves_StillClassesTheFrameAsFF()
    {
        _time.TimeControlMode.Returns(4);
        _time.SimplifiedTimeControlMode.Returns(4);
        Step(_campaignA, 0);
        CloseFrame(_campaignA, 10);

        MapSessionHooks.EndSession("gameEnd");

        StringAssert.Contains(InfoLines().Single(l => l.StartsWith("[MapProfileSummary] ", StringComparison.Ordinal)), " byspeed=FF:1/");
    }

    [TestMethod]
    public void Step_NoTimeControlAdapter_ClassesTheFrameAsUnknown()
    {
        MapSessionHooks.TimeControl = null;
        Step(_campaignA, 0);
        CloseFrame(_campaignA, 10);

        MapSessionHooks.EndSession("gameEnd");

        StringAssert.Contains(InfoLines().Single(l => l.StartsWith("[MapProfileSummary] ", StringComparison.Ordinal)), " byspeed=na:1/");
    }

    [TestMethod]
    public void EndSession_ClosedFrames_LogsTheSummaryOnce()
    {
        Step(_campaignA, 0);
        CloseFrame(_campaignA, 10);

        MapSessionHooks.EndSession("gameEnd");
        MapSessionHooks.EndSession("gameEnd");

        var summaries = InfoLines().Where(l => l.StartsWith("[MapProfileSummary] ", StringComparison.Ordinal)).ToArray();
        Assert.AreEqual(1, summaries.Length);
        StringAssert.StartsWith(summaries[0], "[MapProfileSummary] reason=gameEnd ");
        Assert.IsFalse(Profiler.Measuring);
    }

    [TestMethod]
    public void EndSession_NoClosedFrame_LogsTheNoFramesLine()
    {
        Step(_campaignA, 0);

        MapSessionHooks.EndSession("gameEnd");

        _logger.Received(1).LogInfo(MapProfileLines.BuildNoFramesLine(1, "gameEnd"));
        Assert.AreEqual(0, InfoLines().Count(l => l.StartsWith("[MapProfileSummary] ", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void Step_LoggerThrows_StopsMeasuringAndLogsOneError()
    {
        var boom = new InvalidOperationException("log file gone");
        _logger.When(l => l.LogInfo(Arg.Is<string>(s => s.StartsWith("[MapProfile] ", StringComparison.Ordinal))))
            .Do(_ => throw boom);
        Step(_campaignA, 0);
        CloseFrame(_campaignA, 1200);

        _logger.Received(1).LogError(Arg.Is<string>(s =>
            s.StartsWith("[MapProfiler] frame boundary failed, measuring stopped", StringComparison.Ordinal)));
        _logger.Received(1).LogError(MapProfileLines.BuildFault("frame boundary", boom));
        Assert.IsFalse(Profiler.Measuring);
        _logger.ClearReceivedCalls();

        CloseFrame(_campaignA, 2500);
        CloseFrame(_campaignA, 4000);

        Assert.AreEqual(0, _logger.ReceivedCalls().Count(), "Nothing more for the faulted session.");
    }

    // DECISIONS D6: a fault keeps what was already measured; the frames that closed before it reach a
    // summary with reason=fault instead of being dropped.
    [TestMethod]
    public void Step_FaultAfterClosedFrames_WritesTheSummaryWithReasonFault()
    {
        var boom = new InvalidOperationException("log file gone");
        _logger.When(l => l.LogInfo(Arg.Is<string>(s => s.StartsWith("[MapProfile] ", StringComparison.Ordinal))))
            .Do(_ => throw boom);
        Step(_campaignA, 0);
        CloseFrame(_campaignA, 10);

        CloseFrame(_campaignA, 1200);

        var summaries = InfoLines().Where(l => l.StartsWith("[MapProfileSummary] ", StringComparison.Ordinal)).ToArray();
        Assert.AreEqual(1, summaries.Length, string.Join("\n", InfoLines()));
        StringAssert.StartsWith(summaries[0], "[MapProfileSummary] reason=fault session=1 frames=2 ");
    }

    // A provider read that throws while the session opens stops that campaign once; the next boundary of the
    // same campaign must not open it again (no exception every frame).
    [TestMethod]
    public void Step_SessionOpenThrows_FaultsOnceAndDoesNotRetryTheSameCampaign()
    {
        var boom = new InvalidOperationException("settings gone");
        _settings.TickProfilerTopN.Returns(_ => throw boom);

        Step(_campaignA, 0);
        Step(_campaignA, 10);
        Step(_campaignA, 20);

        _ = _settings.Received(1).TickProfilerTopN;
        _logger.Received(1).LogError(MapProfileLines.BuildFault("frame boundary", boom));
        Assert.IsFalse(Profiler.Measuring);
    }

    [TestMethod]
    public void Step_SessionOpenThrowsForTwoCampaigns_LogsEachFault()
    {
        var boom = new InvalidOperationException("settings gone");
        _settings.TickProfilerTopN.Returns(_ => throw boom);

        Step(_campaignA, 0);
        Step(_campaignB, 10);

        _logger.Received(2).LogError(MapProfileLines.BuildFault("frame boundary", boom));
    }

    private static readonly string[] Lost = { "Campaign.RealTick", "MapScreen.OnFrameTick" };

    // Codex P2: PatchShield strips Patch101's pair from a shared target after a swallowed throw there, and nothing
    // reinstalls it, so a later window would carry zeros that look measured. The installer's patched check is run
    // again where a session starts, at each window start and before a measuring session's summary; a lost hook stops
    // measuring behind one aggregated warning.
    [TestMethod]
    public void Step_HooksLostAtSessionStart_OpensASessionThatDoesNotMeasureAndWarnsOnce()
    {
        MapSessionHooks.LostHooks = () => Lost;

        Step(_campaignA, 0);
        CloseFrame(_campaignA, 10);
        CloseFrame(_campaignA, 2000);

        Assert.IsFalse(Profiler.Measuring);
        Assert.AreEqual(1, Profiler.Session);
        CollectionAssert.AreEqual(new[] { "LogWarning: " + MapProfileLines.BuildHooksLostLine(1, Lost) }, Calls(),
            "One warning: no session-start line, no window line, no summary, nothing for the later frames.");
    }

    [TestMethod]
    public void Step_HooksLostAtWindowStart_WritesTheWindowThenOneWarningThenTheSummaryAndStops()
    {
        IReadOnlyList<string> lost = Array.Empty<string>();
        MapSessionHooks.LostHooks = () => lost;
        Step(_campaignA, 0);
        CloseFrame(_campaignA, 600);
        lost = Lost;

        CloseFrame(_campaignA, 1200);

        var calls = Calls();
        Assert.AreEqual(4, calls.Length, string.Join("\n", calls));
        StringAssert.StartsWith(calls[0], "LogInfo: [MapProfiler] session 1: measuring");
        StringAssert.StartsWith(calls[1], "LogInfo: [MapProfile] t=+", "The window's line is kept: it holds what was measured.");
        Assert.AreEqual("LogWarning: " + MapProfileLines.BuildHooksLostLine(1, Lost), calls[2]);
        StringAssert.StartsWith(calls[3], "LogInfo: [MapProfileSummary] reason=hooksLost session=1 frames=2 ");
        Assert.IsFalse(Profiler.Measuring);
        _logger.ClearReceivedCalls();

        CloseFrame(_campaignA, 2500);
        CloseFrame(_campaignA, 4000);

        Assert.AreEqual(0, _logger.ReceivedCalls().Count(), "Nothing more once the hooks are lost.");
    }

    [TestMethod]
    public void Step_HooksHealthy_AreCheckedAtTheSessionStartAndOncePerWindowOnly()
    {
        var checks = 0;
        MapSessionHooks.LostHooks = () => { checks++; return Array.Empty<string>(); };

        Step(_campaignA, 0);
        CloseFrame(_campaignA, 100);
        CloseFrame(_campaignA, 200);
        CloseFrame(_campaignA, 900);
        Assert.AreEqual(1, checks, "The session start, never per frame.");

        CloseFrame(_campaignA, 1200);
        Assert.AreEqual(2, checks, "Then once when the window is due.");
        CloseFrame(_campaignA, 1300);
        Assert.AreEqual(2, checks);
        CloseFrame(_campaignA, 2300);
        Assert.AreEqual(3, checks);
        Assert.IsTrue(Profiler.Measuring);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void Step_ToggleOffAtSessionStart_DoesNotCheckTheHooks()
    {
        _settings.MapProfilerEnabled.Returns(false);
        var checks = 0;
        MapSessionHooks.LostHooks = () => { checks++; return Lost; };

        Step(_campaignA, 0);

        Assert.AreEqual(0, checks);
        _logger.Received(1).LogInfo(MapProfileLines.BuildSessionOffLine(1));
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void Step_HooksLostForTwoCampaigns_WarnsEachSession()
    {
        MapSessionHooks.LostHooks = () => Lost;

        Step(_campaignA, 0);
        Step(_campaignB, 10);

        _logger.Received(1).LogWarning(MapProfileLines.BuildHooksLostLine(1, Lost));
        _logger.Received(1).LogWarning(MapProfileLines.BuildHooksLostLine(2, Lost));
        Assert.AreEqual(2, Profiler.Session);
        Assert.IsFalse(Profiler.Measuring);
    }

    [TestMethod]
    public void Step_HookCheckThrows_FaultsOnceAndStopsMeasuring()
    {
        var boom = new InvalidOperationException("patch info gone");
        MapSessionHooks.LostHooks = () => throw boom;

        Step(_campaignA, 0);
        Step(_campaignA, 10);

        _logger.Received(1).LogError(MapProfileLines.BuildFault("frame boundary", boom));
        Assert.AreEqual(1, Calls().Length, "Only the error: nothing is retried for the faulted campaign.");
        Assert.IsFalse(Profiler.Measuring);
    }

    // Convergence review of the Codex follow-up: the window and session starts are not the only places a strip can
    // hide. A strip on MapState.OnTick takes the boundary that runs the window check, so the lines stop and nothing
    // clears Measuring; a RealTick or MapScreen strip after the last window start leaves up to one window of frames
    // with that phase at 0. The summary the session ends with would carry either unflagged, so a measuring session's
    // end looks at the hooks once more, and a lost hook turns its summary into reason=hooksLost.
    [TestMethod]
    public void EndSession_HooksLostSinceTheLastLook_WarnsForThatSessionAndWritesTheSummaryAsHooksLost()
    {
        IReadOnlyList<string> lost = Array.Empty<string>();
        MapSessionHooks.LostHooks = () => lost;
        Step(_campaignA, 0);
        CloseFrame(_campaignA, 100);
        lost = new[] { "MapState.OnTick" };   // stripped: no boundary follows, so no window start finds it
        _logger.ClearReceivedCalls();

        MapSessionHooks.EndSession("gameEnd");

        var calls = Calls();
        Assert.AreEqual(2, calls.Length, string.Join("\n", calls));
        Assert.AreEqual("LogWarning: " + MapProfileLines.BuildHooksLostLine(1, lost), calls[0]);
        StringAssert.StartsWith(calls[1], "LogInfo: [MapProfileSummary] reason=hooksLost session=1 frames=1 ");
        Assert.IsFalse(Profiler.Measuring);
    }

    [TestMethod]
    public void Step_NewCampaignAfterAStrip_ClosesTheOldSessionWithItsOwnWarningAndAHooksLostSummary()
    {
        IReadOnlyList<string> lost = Array.Empty<string>();
        MapSessionHooks.LostHooks = () => lost;
        Step(_campaignA, 0);
        CloseFrame(_campaignA, 100);
        lost = Lost;
        _logger.ClearReceivedCalls();

        Step(_campaignB, 200);

        var calls = Calls();
        Assert.AreEqual(3, calls.Length, string.Join("\n", calls));
        Assert.AreEqual("LogWarning: " + MapProfileLines.BuildHooksLostLine(1, Lost), calls[0],
            "The warning names the session whose summary carried the zeros, not the new one.");
        StringAssert.StartsWith(calls[1], "LogInfo: [MapProfileSummary] reason=hooksLost session=1 frames=1 ");
        Assert.AreEqual("LogWarning: " + MapProfileLines.BuildHooksLostLine(2, Lost), calls[2],
            "The new session's own look at the start follows, and it opens without measuring.");
        Assert.AreEqual(2, Profiler.Session);
        Assert.IsFalse(Profiler.Measuring);
    }

    [TestMethod]
    public void EndSession_HooksHealthy_LooksOnceMoreAndKeepsTheReason()
    {
        var checks = 0;
        MapSessionHooks.LostHooks = () => { checks++; return Array.Empty<string>(); };
        Step(_campaignA, 0);
        CloseFrame(_campaignA, 100);
        Assert.AreEqual(1, checks, "The session start.");

        MapSessionHooks.EndSession("gameEnd");

        Assert.AreEqual(2, checks, "One look at the session end.");
        StringAssert.StartsWith(InfoLines().Single(l => l.StartsWith("[MapProfileSummary] ", StringComparison.Ordinal)),
            "[MapProfileSummary] reason=gameEnd session=1 ");
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void Step_NewCampaignWithHealthyHooks_LooksAtTheClosingSessionAndAtTheOpeningOne()
    {
        var checks = 0;
        MapSessionHooks.LostHooks = () => { checks++; return Array.Empty<string>(); };
        Step(_campaignA, 0);
        CloseFrame(_campaignA, 100);
        Assert.AreEqual(1, checks);

        Step(_campaignB, 200);

        Assert.AreEqual(3, checks, "One look closing session 1 and one opening session 2.");
        StringAssert.StartsWith(InfoLines().Single(l => l.StartsWith("[MapProfileSummary] ", StringComparison.Ordinal)),
            "[MapProfileSummary] reason=newCampaign session=1 ");
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    // Nothing to flag when nothing measures: the toggle is off for this session, so no look at its start and none at
    // its end.
    [TestMethod]
    public void EndSession_NotMeasuring_DoesNotLookAtTheHooks()
    {
        _settings.MapProfilerEnabled.Returns(false);
        var checks = 0;
        MapSessionHooks.LostHooks = () => { checks++; return Lost; };
        Step(_campaignA, 0);

        MapSessionHooks.EndSession("gameEnd");

        Assert.AreEqual(0, checks);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    // One warning per session: once a window start found the hooks lost and stopped measuring, the session end has
    // nothing left to look at.
    [TestMethod]
    public void EndSession_AfterAWindowStartFoundTheHooksLost_AddsNoSecondWarning()
    {
        IReadOnlyList<string> lost = Array.Empty<string>();
        var checks = 0;
        MapSessionHooks.LostHooks = () => { checks++; return lost; };
        Step(_campaignA, 0);
        CloseFrame(_campaignA, 600);
        lost = Lost;
        CloseFrame(_campaignA, 1200);
        Assert.IsFalse(Profiler.Measuring);
        Assert.AreEqual(2, checks, "The session start and the window start that found the loss.");
        _logger.ClearReceivedCalls();

        MapSessionHooks.EndSession("gameEnd");

        Assert.AreEqual(2, checks, "No look at the end of a session that already stopped.");
        Assert.AreEqual(0, _logger.ReceivedCalls().Count());
    }

    [TestMethod]
    public void EndSession_HookCheckThrows_FaultsOnceAndKeepsTheClosedFramesAsAFaultSummary()
    {
        var boom = new InvalidOperationException("patch info gone");
        var throwNow = false;
        MapSessionHooks.LostHooks = () => { if (throwNow) throw boom; return Array.Empty<string>(); };
        Step(_campaignA, 0);
        CloseFrame(_campaignA, 100);
        throwNow = true;
        _logger.ClearReceivedCalls();

        MapSessionHooks.EndSession("gameEnd");

        var calls = Calls();
        Assert.AreEqual(2, calls.Length, string.Join("\n", calls));
        Assert.AreEqual("LogError: " + MapProfileLines.BuildFault("session end", boom), calls[0]);
        StringAssert.StartsWith(calls[1], "LogInfo: [MapProfileSummary] reason=fault session=1 frames=1 ");
        Assert.IsFalse(Profiler.Measuring);
    }

    [TestMethod]
    public void Step_HookCheckThrowsWhileClosingTheOldSession_FaultsOnceAndKeepsTheOldSessionsFrames()
    {
        var boom = new InvalidOperationException("patch info gone");
        var throwNow = false;
        MapSessionHooks.LostHooks = () => { if (throwNow) throw boom; return Array.Empty<string>(); };
        Step(_campaignA, 0);
        CloseFrame(_campaignA, 100);
        throwNow = true;
        _logger.ClearReceivedCalls();

        Step(_campaignB, 200);

        var calls = Calls();
        Assert.AreEqual(2, calls.Length, string.Join("\n", calls));
        Assert.AreEqual("LogError: " + MapProfileLines.BuildFault("frame boundary", boom), calls[0]);
        StringAssert.StartsWith(calls[1], "LogInfo: [MapProfileSummary] reason=fault session=1 frames=1 ");
        Assert.AreEqual(1, Profiler.Session, "The new campaign is the faulted session: it is not opened or measured.");
        Assert.IsFalse(Profiler.Measuring);
    }

    // taom_debug.log is the maintainer's record (DECISIONS D6): an out-of-range shared top-N falls back
    // to the provider's default with one line saying so.
    [TestMethod]
    public void Step_TopNOutOfRange_LogsTheFallbackLine()
    {
        MapSessionHooks.RawTopN = () => 0;

        Step(_campaignA, 0);
        CloseFrame(_campaignA, 10);

        _logger.Received(1).LogWarning(MapProfileLines.BuildTopNFallbackLine(0, 8));
    }
}
