using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.BattleLoadDiagnostics;
using TAOM.Features.LoadTimeStamps;
using TAOM.Features.LoadTimeStamps.Domain;

namespace TAOM.Tests.Features.LoadTimeStamps;

[TestClass]
public class LifecycleTimingServiceTests
{
    private IBattleLoadDiagnosticsSettingsProvider _settings = null!;
    private FakeCampaignListenerAdapter _adapter = null!;
    private FakeStampClock _clock = null!;
    private RecordingLogger _logger = null!;
    private LifecycleTimingService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _settings = Substitute.For<IBattleLoadDiagnosticsSettingsProvider>();
        _settings.LoadTimeStampsEnabled.Returns(true);
        _adapter = new FakeCampaignListenerAdapter();
        _clock = new FakeStampClock();
        _logger = new RecordingLogger();
        _sut = Build(_logger);
    }

    // The gate logs its "detail on" line to a logger of its own, so the oracles see only [Lifecycle] lines.
    private LifecycleTimingService Build(IModLogger logger) =>
        new(_adapter, new LoadStampDetailGate(_settings, new RecordingLogger()), _clock, logger);

    [TestMethod]
    public void Dispatch_DetailOff_WritesExactlyTheDispatchLine_AndSwapsNoListener()
    {
        _settings.LoadTimeStampsEnabled.Returns(false);
        _adapter.Add(LifecycleEvent.OnNewGameCreated, "A.OnNewGameCreated", "SandBox", false);

        var scope = _sut.Begin(LifecycleDispatch.OnNewGameCreated);
        _clock.Advance(25);
        _sut.End(scope, null);

        Assert.IsNotNull(scope);
        CollectionAssert.AreEqual(new[]
        {
            "INFO [Lifecycle] dispatch=OnNewGameCreated ms=25.00 listeners_ms=none result=ok",
        }, _logger.Lines);
        Assert.AreEqual(0, _adapter.Wrapped.Count, "no listener is swapped with the toggle off");
        Assert.AreEqual(0, _adapter.Restored.Count, "nothing was swapped, so nothing is restored");
    }

    [TestMethod]
    public void Dispatch_DetailOffAndTheDispatchThrew_LogsTheExceptionTypeAsResult()
    {
        _settings.LoadTimeStampsEnabled.Returns(false);

        var scope = _sut.Begin(LifecycleDispatch.OnSessionStart);
        _clock.Advance(40);
        _sut.End(scope, new NullReferenceException());

        CollectionAssert.AreEqual(new[]
        {
            "INFO [Lifecycle] dispatch=OnSessionStart ms=40.00 listeners_ms=none result=NullReferenceException",
        }, _logger.Lines);
    }

    [TestMethod]
    public void Dispatch_DetailOffWithAMissingBinding_WritesNoTimingOffWarning()
    {
        // The adapter is not asked with the toggle off: a missing binding is already in the
        // always-written [Lifecycle] ready line, and C4 waits for a dispatch that wants the detail.
        _settings.LoadTimeStampsEnabled.Returns(false);
        _adapter.BindingProblem = "MbEvent<CampaignGameStarter>._nonSerializedListenerList not found";

        var scope = _sut.Begin(LifecycleDispatch.OnSessionStart);
        _clock.Advance(40);
        _sut.End(scope, null);

        CollectionAssert.AreEqual(new[]
        {
            "INFO [Lifecycle] dispatch=OnSessionStart ms=40.00 listeners_ms=none result=ok",
        }, _logger.Lines);
    }

    [TestMethod]
    public void Dispatch_DetailOffAcrossANewGame_WritesOneLineEach()
    {
        _settings.LoadTimeStampsEnabled.Returns(false);

        foreach (var dispatch in new[] { LifecycleDispatch.OnNewGameCreated, LifecycleDispatch.OnSessionStart, LifecycleDispatch.OnAfterSessionStart })
        {
            var scope = _sut.Begin(dispatch);
            _clock.Advance(10);
            _sut.End(scope, null);
        }

        CollectionAssert.AreEqual(new[]
        {
            "INFO [Lifecycle] dispatch=OnNewGameCreated ms=10.00 listeners_ms=none result=ok",
            "INFO [Lifecycle] dispatch=OnSessionStart ms=10.00 listeners_ms=none result=ok",
            "INFO [Lifecycle] dispatch=OnAfterSessionStart ms=10.00 listeners_ms=none result=ok",
        }, _logger.Lines);
    }

    [TestMethod]
    public void Dispatch_ToggleFlippedBetweenDispatches_OnlyTheOnDispatchSwapsAndTimesHandlers()
    {
        _adapter.Add(LifecycleEvent.OnSessionLaunched, "A.OnSessionLaunched", "SandBox", false);

        _settings.LoadTimeStampsEnabled.Returns(false);
        var off = _sut.Begin(LifecycleDispatch.OnSessionStart);
        _sut.End(off, null);
        _settings.LoadTimeStampsEnabled.Returns(true);
        var on = _sut.Begin(LifecycleDispatch.OnSessionStart);
        _adapter.Fire(LifecycleEvent.OnSessionLaunched, 0, 12, -1);
        _sut.End(on, null);

        CollectionAssert.AreEqual(new[] { LifecycleEvent.OnSessionLaunched }, _adapter.Wrapped);
        CollectionAssert.AreEqual(_adapter.Wrapped, _adapter.Restored);
        CollectionAssert.AreEqual(new[]
        {
            "INFO [Lifecycle] dispatch=OnSessionStart ms=0.00 listeners_ms=none result=ok",
            "INFO [Lifecycle] event=OnSessionLaunched handler=A.OnSessionLaunched asm=SandBox calls=1 ms=12.00 max_ms=12.00 max_index=none",
            "INFO [Lifecycle] event=OnSessionLaunched scope=total listeners=1 taom_listeners=0 ms=12.00 taom_ms=0.00 other_ms=12.00 over_threshold=1 max_ms=12.00 max_handler=A.OnSessionLaunched",
            "INFO [Lifecycle] dispatch=OnSessionStart ms=0.00 listeners_ms=12.00 result=ok",
        }, _logger.Lines);
    }

    [DataTestMethod]
    [DataRow(LifecycleDispatch.OnNewGameCreated, "OnNewGameCreated,OnNewGameCreatedPartialFollowUp,OnNewGameCreatedPartialFollowUpEnd")]
    [DataRow(LifecycleDispatch.OnGameEarlyLoaded, "OnGameEarlyLoaded")]
    [DataRow(LifecycleDispatch.OnGameLoaded, "OnGameLoaded")]
    [DataRow(LifecycleDispatch.OnSessionStart, "OnSessionLaunched")]
    [DataRow(LifecycleDispatch.OnAfterSessionStart, "OnAfterSessionLaunched")]
    public void Begin_EachDispatch_WrapsItsEventsInOrder(LifecycleDispatch dispatch, string expected)
    {
        var scope = _sut.Begin(dispatch);

        Assert.IsNotNull(scope);
        Assert.AreEqual(expected, string.Join(",", _adapter.Wrapped));
        Assert.AreEqual(expected, string.Join(",", LifecycleTimingService.EventsOf(dispatch)));
    }

    [TestMethod]
    public void End_AHandlerAtTheThreshold_GetsALine_AndOneJustUnderDoesNot()
    {
        _adapter.Add(LifecycleEvent.OnSessionLaunched, "A.OnSessionLaunched", "SandBox", false);
        _adapter.Add(LifecycleEvent.OnSessionLaunched, "B.OnSessionLaunched", "TAOM", true);
        var scope = _sut.Begin(LifecycleDispatch.OnSessionStart);
        _adapter.Fire(LifecycleEvent.OnSessionLaunched, 0, 10, -1);
        _adapter.Fire(LifecycleEvent.OnSessionLaunched, 1, 9, -1);

        _sut.End(scope, null);

        var handlerLines = _logger.Lines.Where(l => l.Contains(" handler=")).ToList();
        CollectionAssert.AreEqual(new[]
        {
            "INFO [Lifecycle] event=OnSessionLaunched handler=A.OnSessionLaunched asm=SandBox calls=1 ms=10.00 max_ms=10.00 max_index=none",
        }, handlerLines);
    }

    [TestMethod]
    public void End_EventTotal_SplitsTaomAndOtherTime_AndCountsOverThreshold()
    {
        _adapter.Add(LifecycleEvent.OnGameLoaded, "Vanilla.OnGameLoaded", "TaleWorlds.CampaignSystem", false);
        _adapter.Add(LifecycleEvent.OnGameLoaded, "Ours.OnGameLoaded", "TAOM", true);
        _adapter.Add(LifecycleEvent.OnGameLoaded, "Ours2.OnGameLoaded", "TAOM", true);
        _adapter.Add(LifecycleEvent.OnGameLoaded, "Quiet.OnGameLoaded", "SandBox", false);
        var scope = _sut.Begin(LifecycleDispatch.OnGameLoaded);
        _adapter.Fire(LifecycleEvent.OnGameLoaded, 0, 40, -1);
        _adapter.Fire(LifecycleEvent.OnGameLoaded, 1, 15, -1);
        _adapter.Fire(LifecycleEvent.OnGameLoaded, 2, 3, -1);
        _adapter.Fire(LifecycleEvent.OnGameLoaded, 3, 2, -1);

        _sut.End(scope, null);

        CollectionAssert.Contains(_logger.Lines,
            "INFO [Lifecycle] event=OnGameLoaded scope=total listeners=4 taom_listeners=2 ms=60.00 taom_ms=18.00 other_ms=42.00 over_threshold=2 max_ms=40.00 max_handler=Vanilla.OnGameLoaded");
    }

    [TestMethod]
    public void End_PartialFollowUp_AggregatesCallsAndKeepsTheArgumentOfTheSlowestCall()
    {
        _adapter.Add(LifecycleEvent.OnNewGameCreatedPartialFollowUp, "Market.OnNewGameCreatedPartialFollowUp", "TAOM", true);
        var scope = _sut.Begin(LifecycleDispatch.OnNewGameCreated);
        _adapter.Fire(LifecycleEvent.OnNewGameCreatedPartialFollowUp, 0, 2, 0);
        _adapter.Fire(LifecycleEvent.OnNewGameCreatedPartialFollowUp, 0, 30, 1);
        _adapter.Fire(LifecycleEvent.OnNewGameCreatedPartialFollowUp, 0, 30, 2);
        _adapter.Fire(LifecycleEvent.OnNewGameCreatedPartialFollowUp, 0, 1, 3);

        _sut.End(scope, null);

        CollectionAssert.Contains(_logger.Lines,
            "INFO [Lifecycle] event=OnNewGameCreatedPartialFollowUp handler=Market.OnNewGameCreatedPartialFollowUp asm=TAOM calls=4 ms=63.00 max_ms=30.00 max_index=1");
    }

    [TestMethod]
    public void End_LinesComeHandlersThenEventTotalPerEvent_ThenTheDispatchLine()
    {
        _adapter.Add(LifecycleEvent.OnNewGameCreated, "A.OnNewGameCreated", "SandBox", false);
        _adapter.Add(LifecycleEvent.OnNewGameCreated, "B.OnNewGameCreated", "TAOM", true);
        _adapter.Add(LifecycleEvent.OnNewGameCreatedPartialFollowUp, "C.OnNewGameCreatedPartialFollowUp", "TAOM", true);
        var scope = _sut.Begin(LifecycleDispatch.OnNewGameCreated);
        _adapter.Fire(LifecycleEvent.OnNewGameCreated, 0, 12, -1);
        _adapter.Fire(LifecycleEvent.OnNewGameCreated, 1, 20, -1);
        _adapter.Fire(LifecycleEvent.OnNewGameCreatedPartialFollowUp, 0, 11, 5);
        _clock.Advance(50);

        _sut.End(scope, null);

        CollectionAssert.AreEqual(new[]
        {
            "INFO [Lifecycle] event=OnNewGameCreated handler=A.OnNewGameCreated asm=SandBox calls=1 ms=12.00 max_ms=12.00 max_index=none",
            "INFO [Lifecycle] event=OnNewGameCreated handler=B.OnNewGameCreated asm=TAOM calls=1 ms=20.00 max_ms=20.00 max_index=none",
            "INFO [Lifecycle] event=OnNewGameCreated scope=total listeners=2 taom_listeners=1 ms=32.00 taom_ms=20.00 other_ms=12.00 over_threshold=2 max_ms=20.00 max_handler=B.OnNewGameCreated",
            "INFO [Lifecycle] event=OnNewGameCreatedPartialFollowUp handler=C.OnNewGameCreatedPartialFollowUp asm=TAOM calls=1 ms=11.00 max_ms=11.00 max_index=5",
            "INFO [Lifecycle] event=OnNewGameCreatedPartialFollowUp scope=total listeners=1 taom_listeners=1 ms=11.00 taom_ms=11.00 other_ms=0.00 over_threshold=1 max_ms=11.00 max_handler=C.OnNewGameCreatedPartialFollowUp",
            "INFO [Lifecycle] event=OnNewGameCreatedPartialFollowUpEnd scope=total listeners=0 taom_listeners=0 ms=0.00 taom_ms=0.00 other_ms=0.00 over_threshold=0 max_ms=0.00 max_handler=none",
            "INFO [Lifecycle] dispatch=OnNewGameCreated ms=50.00 listeners_ms=43.00 result=ok",
        }, _logger.Lines);
        CollectionAssert.AreEqual(_adapter.Wrapped, _adapter.Restored);
    }

    [TestMethod]
    public void End_RestoresEveryWrappedEvent_EvenWhenTheLoggerThrows()
    {
        var throwing = Substitute.For<IModLogger>();
        throwing.When(l => l.LogInfo(Arg.Any<string>())).Do(_ => throw new InvalidOperationException("disk full"));
        throwing.When(l => l.LogWarning(Arg.Any<string>())).Do(_ => throw new InvalidOperationException("disk full"));
        var sut = Build(throwing);
        _adapter.Add(LifecycleEvent.OnNewGameCreated, "A.OnNewGameCreated", "SandBox", false);
        var scope = sut.Begin(LifecycleDispatch.OnNewGameCreated);
        _adapter.Fire(LifecycleEvent.OnNewGameCreated, 0, 12, -1);

        sut.End(scope, null);

        CollectionAssert.AreEqual(new[]
        {
            LifecycleEvent.OnNewGameCreated, LifecycleEvent.OnNewGameCreatedPartialFollowUp, LifecycleEvent.OnNewGameCreatedPartialFollowUpEnd,
        }, _adapter.Restored);
    }

    [TestMethod]
    public void End_DispatchThrew_LogsTheExceptionTypeAsResult()
    {
        var scope = _sut.Begin(LifecycleDispatch.OnSessionStart);
        _clock.Advance(40);

        _sut.End(scope, new NullReferenceException());

        Assert.AreEqual("INFO [Lifecycle] dispatch=OnSessionStart ms=40.00 listeners_ms=0.00 result=NullReferenceException", _logger.Lines.Last());
        CollectionAssert.AreEqual(new[] { LifecycleEvent.OnSessionLaunched }, _adapter.Restored);
    }

    [TestMethod]
    public void Begin_BindingProblem_WarnsOnceAcrossDispatches_AndEndLogsTheDispatchWithListenersNone()
    {
        _adapter.BindingProblem = "MbEvent<CampaignGameStarter>._nonSerializedListenerList not found";

        var first = _sut.Begin(LifecycleDispatch.OnSessionStart);
        _clock.Advance(40);
        _sut.End(first, null);
        var second = _sut.Begin(LifecycleDispatch.OnAfterSessionStart);
        _clock.Advance(5);
        _sut.End(second, null);

        Assert.AreEqual(0, _adapter.Wrapped.Count);
        CollectionAssert.AreEqual(new[]
        {
            "WARN [Lifecycle] per-handler timing off for this session: MbEvent<CampaignGameStarter>._nonSerializedListenerList not found; dispatch totals are still written",
            "INFO [Lifecycle] dispatch=OnSessionStart ms=40.00 listeners_ms=none result=ok",
            "INFO [Lifecycle] dispatch=OnAfterSessionStart ms=5.00 listeners_ms=none result=ok",
        }, _logger.Lines);
    }

    [TestMethod]
    public void Begin_WrapThrows_RestoresWhatItWrapped_WarnsOnce_AndLaterDispatchesWrapNothing()
    {
        _adapter.ThrowOnWrap = LifecycleEvent.OnNewGameCreatedPartialFollowUp;

        var scope = _sut.Begin(LifecycleDispatch.OnNewGameCreated);
        _clock.Advance(7);
        _sut.End(scope, null);
        var later = _sut.Begin(LifecycleDispatch.OnSessionStart);
        _sut.End(later, null);

        CollectionAssert.AreEqual(new[] { LifecycleEvent.OnNewGameCreated }, _adapter.Wrapped);
        CollectionAssert.AreEqual(new[] { LifecycleEvent.OnNewGameCreated, LifecycleEvent.OnNewGameCreatedPartialFollowUp }, _adapter.Restored);
        CollectionAssert.AreEqual(new[]
        {
            "WARN [Lifecycle] per-handler timing off for this session: InvalidOperationException: record type moved; dispatch totals are still written",
            "INFO [Lifecycle] dispatch=OnNewGameCreated ms=7.00 listeners_ms=none result=ok",
            "INFO [Lifecycle] dispatch=OnSessionStart ms=0.00 listeners_ms=none result=ok",
        }, _logger.Lines);
    }

    [TestMethod]
    public void End_DispatchMs_ExcludesTheStampsOwnSwapAndLines()
    {
        _adapter.Add(LifecycleEvent.OnSessionLaunched, "A.OnSessionLaunched", "SandBox", false);
        _adapter.OnWrap = () => _clock.Advance(5);
        _logger.OnLog = _ => _clock.Advance(100);
        var scope = _sut.Begin(LifecycleDispatch.OnSessionStart);
        _clock.Advance(30);
        _adapter.Fire(LifecycleEvent.OnSessionLaunched, 0, 12, -1);

        _sut.End(scope, null);

        Assert.AreEqual("INFO [Lifecycle] dispatch=OnSessionStart ms=30.00 listeners_ms=12.00 result=ok", _logger.Lines.Last());
    }

    [TestMethod]
    public void End_LoggerFault_WarnsAStampFault_NotTimingOff_AndLaterDispatchesStillWrap()
    {
        _logger.OnLog = line => { if (line.StartsWith("INFO ")) throw new InvalidOperationException("disk full"); };
        _adapter.Add(LifecycleEvent.OnSessionLaunched, "A.OnSessionLaunched", "SandBox", false);
        var scope = _sut.Begin(LifecycleDispatch.OnSessionStart);
        _adapter.Fire(LifecycleEvent.OnSessionLaunched, 0, 12, -1);

        _sut.End(scope, null);
        _logger.OnLog = null;
        var later = _sut.Begin(LifecycleDispatch.OnAfterSessionStart);
        _sut.End(later, null);

        CollectionAssert.AreEqual(new[] { LifecycleEvent.OnSessionLaunched, LifecycleEvent.OnAfterSessionLaunched }, _adapter.Wrapped);
        CollectionAssert.AreEqual(new[]
        {
            "WARN [Lifecycle] stamp fault, some [Lifecycle] lines may be missing this session: InvalidOperationException: disk full",
            "INFO [Lifecycle] event=OnAfterSessionLaunched scope=total listeners=0 taom_listeners=0 ms=0.00 taom_ms=0.00 other_ms=0.00 over_threshold=0 max_ms=0.00 max_handler=none",
            "INFO [Lifecycle] dispatch=OnAfterSessionStart ms=0.00 listeners_ms=0.00 result=ok",
        }, _logger.Lines);
    }

    [TestMethod]
    public void End_RestoreFault_WarnsARestoreFaultOnce_AndStillWritesTheDispatchLine()
    {
        _adapter.ThrowOnRestore = true;

        var first = _sut.Begin(LifecycleDispatch.OnSessionStart);
        _sut.End(first, null);
        var second = _sut.Begin(LifecycleDispatch.OnAfterSessionStart);
        _sut.End(second, null);

        Assert.AreEqual(1, _logger.Lines.Count(l => l.StartsWith("WARN ")));
        CollectionAssert.Contains(_logger.Lines,
            "WARN [Lifecycle] restore fault, a campaign handler may keep its timing wrapper this session (it still runs once per call): InvalidOperationException: setter moved");
        CollectionAssert.Contains(_logger.Lines, "INFO [Lifecycle] dispatch=OnSessionStart ms=0.00 listeners_ms=0.00 result=ok");
        CollectionAssert.Contains(_logger.Lines, "INFO [Lifecycle] dispatch=OnAfterSessionStart ms=0.00 listeners_ms=0.00 result=ok");
    }

    [TestMethod]
    public void End_RestoreFault_NeverHidesALaterStampFault()
    {
        _adapter.ThrowOnRestore = true;
        var first = _sut.Begin(LifecycleDispatch.OnSessionStart);
        _sut.End(first, null);
        _adapter.ThrowOnRestore = false;
        // Reads: 1 and 2 in the first Begin, 3 in its End, 4 and 5 in the second Begin.
        _clock.ThrowOnRead = 5;

        var second = _sut.Begin(LifecycleDispatch.OnAfterSessionStart);
        _sut.End(second, null);

        CollectionAssert.AreEqual(new[]
        {
            "WARN [Lifecycle] restore fault, a campaign handler may keep its timing wrapper this session (it still runs once per call): InvalidOperationException: setter moved",
            "WARN [Lifecycle] stamp fault, some [Lifecycle] lines may be missing this session: InvalidOperationException: clock gone",
        }, _logger.Lines.Where(l => l.StartsWith("WARN ")).ToList());
    }

    [TestMethod]
    public void Begin_ClockFaultAfterTheSwap_WarnsAStampFault_KeepsTheSwap_AndTimingStaysOn()
    {
        _adapter.Add(LifecycleEvent.OnSessionLaunched, "A.OnSessionLaunched", "SandBox", false);
        // Read 1 is the scope's first tick; read 2 is the start tick taken after the swap.
        _clock.ThrowOnRead = 2;

        var scope = _sut.Begin(LifecycleDispatch.OnSessionStart);
        _adapter.Fire(LifecycleEvent.OnSessionLaunched, 0, 12, -1);
        _clock.Advance(30);
        _sut.End(scope, null);
        var later = _sut.Begin(LifecycleDispatch.OnAfterSessionStart);
        _sut.End(later, null);

        CollectionAssert.AreEqual(new[] { LifecycleEvent.OnSessionLaunched, LifecycleEvent.OnAfterSessionLaunched }, _adapter.Wrapped);
        CollectionAssert.AreEqual(_adapter.Wrapped, _adapter.Restored);
        CollectionAssert.AreEqual(new[]
        {
            "WARN [Lifecycle] stamp fault, some [Lifecycle] lines may be missing this session: InvalidOperationException: clock gone",
            "INFO [Lifecycle] event=OnSessionLaunched handler=A.OnSessionLaunched asm=SandBox calls=1 ms=12.00 max_ms=12.00 max_index=none",
            "INFO [Lifecycle] event=OnSessionLaunched scope=total listeners=1 taom_listeners=0 ms=12.00 taom_ms=0.00 other_ms=12.00 over_threshold=1 max_ms=12.00 max_handler=A.OnSessionLaunched",
            "INFO [Lifecycle] dispatch=OnSessionStart ms=30.00 listeners_ms=12.00 result=ok",
            "INFO [Lifecycle] event=OnAfterSessionLaunched scope=total listeners=0 taom_listeners=0 ms=0.00 taom_ms=0.00 other_ms=0.00 over_threshold=0 max_ms=0.00 max_handler=none",
            "INFO [Lifecycle] dispatch=OnAfterSessionStart ms=0.00 listeners_ms=0.00 result=ok",
        }, _logger.Lines);
    }

    [TestMethod]
    public void Begin_BindingProblem_DispatchMs_LeavesOutTheTimingOffWarning()
    {
        _adapter.BindingProblem = "MbEvent<CampaignGameStarter>._nonSerializedListenerList not found";
        _logger.OnLog = _ => _clock.Advance(100);

        var scope = _sut.Begin(LifecycleDispatch.OnSessionStart);
        _clock.Advance(40);
        _sut.End(scope, null);

        Assert.AreEqual("INFO [Lifecycle] dispatch=OnSessionStart ms=40.00 listeners_ms=none result=ok", _logger.Lines.Last());
    }

    [TestMethod]
    public void Begin_WrapFailure_DispatchMs_LeavesOutThePartialSwapAndTheWarning()
    {
        _adapter.ThrowOnWrap = LifecycleEvent.OnNewGameCreatedPartialFollowUp;
        _adapter.OnWrap = () => _clock.Advance(5);
        _logger.OnLog = _ => _clock.Advance(100);

        var scope = _sut.Begin(LifecycleDispatch.OnNewGameCreated);
        _clock.Advance(7);
        _sut.End(scope, null);

        Assert.AreEqual("INFO [Lifecycle] dispatch=OnNewGameCreated ms=7.00 listeners_ms=none result=ok", _logger.Lines.Last());
    }

    [TestMethod]
    public void Begin_WrapFailsAfterAStampFault_StillWarnsThatTimingIsOff()
    {
        // The third clock read is the first dispatch's end tick: End's catch writes C5 (a stamp fault).
        _clock.ThrowOnRead = 3;
        var first = _sut.Begin(LifecycleDispatch.OnSessionStart);
        _sut.End(first, null);
        _clock.ThrowOnRead = null;
        _adapter.ThrowOnWrap = LifecycleEvent.OnGameLoaded;

        var second = _sut.Begin(LifecycleDispatch.OnGameLoaded);
        _sut.End(second, null);

        CollectionAssert.AreEqual(new[]
        {
            "WARN [Lifecycle] stamp fault, some [Lifecycle] lines may be missing this session: InvalidOperationException: clock gone",
            "WARN [Lifecycle] per-handler timing off for this session: InvalidOperationException: record type moved; dispatch totals are still written",
        }, _logger.Lines.Where(l => l.StartsWith("WARN ", StringComparison.Ordinal)).ToList(),
            "a stamp fault keeps its own latch, so it never hides the later timing-off line");
    }

    [TestMethod]
    public void Begin_WrapFailsAfterARestoreFault_StillWarnsThatTimingIsOff()
    {
        _adapter.ThrowOnRestore = true;
        var first = _sut.Begin(LifecycleDispatch.OnSessionStart);
        _sut.End(first, null);
        _adapter.ThrowOnRestore = false;
        _adapter.ThrowOnWrap = LifecycleEvent.OnGameLoaded;

        var second = _sut.Begin(LifecycleDispatch.OnGameLoaded);
        _sut.End(second, null);

        CollectionAssert.Contains(_logger.Lines,
            "WARN [Lifecycle] per-handler timing off for this session: InvalidOperationException: record type moved; dispatch totals are still written");
        CollectionAssert.Contains(_logger.Lines, "INFO [Lifecycle] dispatch=OnGameLoaded ms=0.00 listeners_ms=none result=ok");
    }

    [TestMethod]
    public void End_NullScope_DoesNothing()
    {
        _sut.End(null, new InvalidOperationException("x"));

        Assert.AreEqual(0, _logger.Lines.Count);
        Assert.AreEqual(0, _adapter.Restored.Count);
    }
}
