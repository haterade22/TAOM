// Adapted from yotthani's VanillaTuning (MIT, (c) 2026 yotthani), frame-buffer-guard and frame-buffer-watch.
using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features.CoopInterop;
using TAOM.Features.SkeletonBuffer;
using TAOM.Tests.Features.LoadTimeStamps;

namespace TAOM.Tests.Features.SkeletonBuffer;

/// <summary>
/// The per-mission watch: what it reads and when, the peak line at the end of a mission (once), the guard's overflow
/// delta, and the one on-screen warning that only an unguarded battle on a client may show.
/// </summary>
[TestClass]
public class SkeletonBufferWatchServiceTests
{
    private const long Global = 0x7FF600D9D160;
    private const long Pointer = 0x1D000000000;
    private const long Counter = 0x7FF5FFF01000;
    private const long Counter2 = 0x7FF5FFF01040;
    private const long FillA = Pointer + 0x9D0;
    private const long FillB = Pointer + 0x9D0 + 0x128;
    private const long Pool2A = Pointer + 0xC28;
    private const long Pool2B = Pointer + 0xC28 + 0x128;

    private ISkeletonBufferGuardService _guard = null!;
    private ISkeletonBufferMemoryAdapter _memory = null!;
    private ISkeletonBufferEngineAdapter _engine = null!;
    private ISkeletonBufferSettingsProvider _settings = null!;
    private IDedicatedServerProvider _server = null!;
    private RecordingLogger _logger = null!;
    private SkeletonBufferWatchService _sut = null!;

    [TestInitialize]
    public void SetUp()
    {
        _guard = Substitute.For<ISkeletonBufferGuardService>();
        _guard.Target.Returns(new SkeletonBufferTarget(Global, pool1Guarded: true, pool1CounterAddress: Counter));
        _memory = Substitute.For<ISkeletonBufferMemoryAdapter>();
        _memory.ReadInt64(Global).Returns(Pointer);
        _memory.ReadInt32(FillA).Returns(0);
        _memory.ReadInt32(FillB).Returns(0);
        _memory.ReadInt32(Counter).Returns(3);
        _engine = Substitute.For<ISkeletonBufferEngineAdapter>();
        _settings = Substitute.For<ISkeletonBufferSettingsProvider>();
        _settings.WatchEnabled.Returns(true);
        _server = Substitute.For<IDedicatedServerProvider>();
        _logger = new RecordingLogger();
        _sut = new SkeletonBufferWatchService(_guard, _memory, _engine, _settings, _server, _logger);
    }

    private void Unguarded() =>
        _guard.Target.Returns(new SkeletonBufferTarget(Global, pool1Guarded: false, pool1CounterAddress: 0));

    private void WithPool2(bool guarded = true, long counter = Counter, bool pool2Guarded = false, long pool2Counter = 0)
    {
        _guard.Target.Returns(new SkeletonBufferTarget(Global, guarded, counter, watchPool2: true,
            pool2Guarded: pool2Guarded, pool2CounterAddress: pool2Counter));
        _memory.ReadInt32(Pool2A).Returns(0);
        _memory.ReadInt32(Pool2B).Returns(0);
    }

    private void WithGuardedPool2(int pool2Fill = 100)
    {
        WithPool2(pool2Guarded: true, pool2Counter: Counter2);
        _memory.ReadInt32(FillA).Returns(100);
        _memory.ReadInt32(Pool2A).Returns(pool2Fill);
        _memory.ReadInt32(Counter2).Returns(10);
    }

    private static int Agents() => 700;

    [TestMethod]
    public void Tick_ReadsThePointerThenBothFillCounters_AndKeepsTheLarger()
    {
        _memory.ReadInt32(FillA).Returns(100);
        _memory.ReadInt32(FillB).Returns(40000);
        _sut.Begin();

        _sut.Tick(Agents, 5.0);
        _sut.End();

        StringAssert.Contains(_logger.Lines.Last(), "mission peak: 40000 of 65536 entries");
    }

    [TestMethod]
    public void Tick_TracksThePeakAcrossTicks_WithTheAgentCountAtThePeak()
    {
        _sut.Begin();
        _memory.ReadInt32(FillA).Returns(1000);
        _sut.Tick(() => 100, 1.0);
        _memory.ReadInt32(FillA).Returns(30000);
        _sut.Tick(() => 900, 2.0);
        _memory.ReadInt32(FillA).Returns(500);
        _sut.Tick(() => 50, 3.0);

        _sut.End();

        StringAssert.Contains(_logger.Lines.Last(), "30000 of 65536 entries (45.8 %), about 1071 skeletons, 900 agents, 2.0 s into the mission");
    }

    [TestMethod]
    public void Tick_PointerUnreadable_TakesNoSample()
    {
        _memory.ReadInt64(Global).Returns((long?)null);
        _sut.Begin();

        _sut.Tick(Agents, 1.0);
        _sut.End();

        Assert.AreEqual("INFO " + SkeletonBufferLines.NoReadings, _logger.Lines.Last());
        _memory.DidNotReceive().ReadInt32(FillA);
    }

    [TestMethod]
    public void Tick_PointerIsNull_TakesNoSample()
    {
        _memory.ReadInt64(Global).Returns(0L);
        _sut.Begin();

        _sut.Tick(Agents, 1.0);

        _memory.DidNotReceive().ReadInt32(FillA);
        _memory.DidNotReceive().ReadInt32(FillB);
    }

    [TestMethod]
    public void Tick_OneFillUnreadable_UsesTheOther()
    {
        _memory.ReadInt32(FillA).Returns((int?)null);
        _memory.ReadInt32(FillB).Returns(700);
        _sut.Begin();

        _sut.Tick(Agents, 1.0);
        _sut.End();

        StringAssert.Contains(_logger.Lines.Last(), "peak: 700 of");
    }

    [TestMethod]
    public void Tick_BothFillsUnreadable_TakesNoSample()
    {
        _memory.ReadInt32(FillA).Returns((int?)null);
        _memory.ReadInt32(FillB).Returns((int?)null);
        _sut.Begin();

        _sut.Tick(Agents, 1.0);
        _sut.End();

        Assert.AreEqual("INFO " + SkeletonBufferLines.NoReadings, _logger.Lines.Last());
    }

    [TestMethod]
    public void Tick_WatchSettingOff_ReadsNothing_AndSaysSoOnce()
    {
        _settings.WatchEnabled.Returns(false);

        _sut.Begin();
        _sut.Tick(Agents, 1.0);
        _sut.End();

        _memory.DidNotReceiveWithAnyArgs().ReadInt64(default);
        _memory.DidNotReceiveWithAnyArgs().ReadInt32(default);
        Assert.AreEqual(1, _logger.Lines.Count);
        StringAssert.Contains(_logger.Lines[0], "watch off for this mission");
    }

    [TestMethod]
    public void Tick_NoTarget_ReadsNothingAndLogsNothing()
    {
        _guard.Target.Returns((SkeletonBufferTarget?)null);

        _sut.Begin();
        _sut.Tick(Agents, 1.0);
        _sut.End();

        _memory.DidNotReceiveWithAnyArgs().ReadInt64(default);
        Assert.AreEqual(0, _logger.Lines.Count, "the install already said why");
    }

    [TestMethod]
    public void Tick_BeforeBegin_DoesNothing()
    {
        _sut.Tick(Agents, 1.0);

        _memory.DidNotReceiveWithAnyArgs().ReadInt64(default);
    }

    [TestMethod]
    public void Tick_AfterEnd_DoesNothing()
    {
        _sut.Begin();
        _sut.End();
        _memory.ClearReceivedCalls();

        _sut.Tick(Agents, 1.0);

        _memory.DidNotReceiveWithAnyArgs().ReadInt64(default);
    }

    [TestMethod]
    public void End_CalledTwice_WritesOneLine()
    {
        _memory.ReadInt32(FillA).Returns(100);
        _sut.Begin();
        _sut.Tick(Agents, 1.0);

        _sut.End();
        _sut.End();

        Assert.AreEqual(1, _logger.Lines.Count);
    }

    [TestMethod]
    public void Begin_Twice_StartsFromAFreshPeak()
    {
        _memory.ReadInt32(FillA).Returns(50000);
        _sut.Begin();
        _sut.Tick(Agents, 1.0);
        _memory.ReadInt32(FillA).Returns(10);

        _sut.Begin();
        _sut.Tick(Agents, 1.0);
        _sut.End();

        StringAssert.Contains(_logger.Lines.Last(), "peak: 10 of");
    }

    // ---- the second pool ----

    [TestMethod]
    public void Tick_Pool2Watched_ReadsBothOfItsBuffersAndKeepsTheLarger()
    {
        WithPool2();
        _memory.ReadInt32(FillA).Returns(100);
        _memory.ReadInt32(Pool2A).Returns(5000);
        _memory.ReadInt32(Pool2B).Returns(90000);
        _sut.Begin();

        _sut.Tick(Agents, 5.0);
        _sut.End();

        StringAssert.Contains(_logger.Lines.Last(), "pool 2 peak: 90000 of 262144 entries (34.3 %)");
    }

    [TestMethod]
    public void Tick_Pool2NotWatched_NeverReadsItsCounters()
    {
        _memory.ReadInt32(FillA).Returns(100);
        _sut.Begin();

        _sut.Tick(Agents, 5.0);
        _sut.End();

        _memory.DidNotReceive().ReadInt32(Pool2A);
        _memory.DidNotReceive().ReadInt32(Pool2B);
        Assert.IsFalse(_logger.Lines.Last().Contains("pool 2"), "no pool 2 clause when it is not watched");
    }

    [TestMethod]
    public void Tick_TracksPool2sPeakApartFromPool1s()
    {
        WithPool2();
        _sut.Begin();
        _memory.ReadInt32(FillA).Returns(30000);
        _memory.ReadInt32(Pool2A).Returns(1000);
        _sut.Tick(() => 900, 1.0);
        _memory.ReadInt32(FillA).Returns(500);
        _memory.ReadInt32(Pool2A).Returns(70000);
        _sut.Tick(() => 50, 2.0);

        _sut.End();

        var line = _logger.Lines.Last();
        StringAssert.Contains(line, "mission peak: 30000 of 65536 entries (45.8 %), about 1071 skeletons, 900 agents, 1.0 s into the mission");
        StringAssert.Contains(line, "pool 2 peak: 70000 of 262144 entries (26.7 %)");
    }

    [TestMethod]
    public void Tick_Pool2OneBufferUnreadable_UsesTheOther()
    {
        WithPool2();
        _memory.ReadInt32(FillA).Returns(100);
        _memory.ReadInt32(Pool2A).Returns((int?)null);
        _memory.ReadInt32(Pool2B).Returns(777);
        _sut.Begin();

        _sut.Tick(Agents, 1.0);
        _sut.End();

        StringAssert.Contains(_logger.Lines.Last(), "pool 2 peak: 777 of 262144");
    }

    [TestMethod]
    public void Tick_Pool2BothUnreadable_LeavesPool1sLineWithoutAPool2Clause()
    {
        WithPool2();
        _memory.ReadInt32(FillA).Returns(100);
        _memory.ReadInt32(Pool2A).Returns((int?)null);
        _memory.ReadInt32(Pool2B).Returns((int?)null);
        _sut.Begin();

        _sut.Tick(Agents, 1.0);
        _sut.End();

        StringAssert.Contains(_logger.Lines.Last(), "peak: 100 of 65536");
        Assert.IsFalse(_logger.Lines.Last().Contains("pool 2"));
    }

    [TestMethod]
    public void End_Pool2AtNinetyPercent_LogsAWarningNamingIt()
    {
        WithPool2();
        _memory.ReadInt32(FillA).Returns(100);
        _memory.ReadInt32(Pool2B).Returns(235930);   // 90.0 % of 262,144
        _sut.Begin();
        _sut.Tick(Agents, 1.0);

        _sut.End();

        var line = _logger.Lines.Last();
        StringAssert.StartsWith(line, "WARN ");
        StringAssert.Contains(line, "pool 2 peak: 235930 of 262144 entries (90.0 %), over 90 %");
    }

    [TestMethod]
    public void End_Pool2BelowNinetyPercent_LogsAtInfo()
    {
        WithPool2();
        _memory.ReadInt32(Pool2B).Returns(235929);
        _memory.ReadInt32(FillA).Returns(100);
        _sut.Begin();
        _sut.Tick(Agents, 1.0);

        _sut.End();

        StringAssert.StartsWith(_logger.Lines.Last(), "INFO ");
        Assert.IsFalse(_logger.Lines.Last().Contains("over 90 %"));
    }

    [TestMethod]
    public void End_Pool1AtNinetyPercentWithoutAGuard_LogsAWarningNamingIt()
    {
        Unguarded();
        _memory.ReadInt32(FillA).Returns(60000);   // 91.6 %
        _sut.Begin();
        _sut.Tick(Agents, 1.0);

        _sut.End();

        var line = _logger.Lines.Last();
        StringAssert.StartsWith(line, "WARN ");
        StringAssert.Contains(line, "60000 of 65536 entries (91.6 %), over 90 %, about");
    }

    [TestMethod]
    public void End_Pool1AtNinetyPercentWithOurGuard_LogsAtInfoWithTheMarker()
    {
        _memory.ReadInt32(FillA).Returns(60000);
        _sut.Begin();
        _sut.Tick(Agents, 1.0);

        _sut.End();

        var line = _logger.Lines.Last();
        StringAssert.StartsWith(line, "INFO ");
        StringAssert.Contains(line, "(91.6 %), over 90 %");
    }

    [TestMethod]
    public void End_Pool1AtNinetyPercentGuardedByAnotherModule_LogsAtInfo()
    {
        _guard.Target.Returns(new SkeletonBufferTarget(Global, pool1Guarded: true, pool1CounterAddress: 0));
        _memory.ReadInt32(FillA).Returns(60000);
        _sut.Begin();
        _sut.Tick(Agents, 1.0);

        _sut.End();

        StringAssert.StartsWith(_logger.Lines.Last(), "INFO ");
    }

    [TestMethod]
    public void End_Pool1BelowNinetyPercentWithoutAGuard_LogsAtInfoWithoutTheMarker()
    {
        Unguarded();
        _memory.ReadInt32(FillA).Returns(58982);
        _sut.Begin();
        _sut.Tick(Agents, 1.0);

        _sut.End();

        StringAssert.StartsWith(_logger.Lines.Last(), "INFO ");
        Assert.IsFalse(_logger.Lines.Last().Contains("over 90 %"));
    }

    [TestMethod]
    public void End_Pool2AtNinetyPercentWithOurGuard_LogsAtInfoWithTheMarker()
    {
        WithGuardedPool2(pool2Fill: 235930);
        _sut.Begin();
        _sut.Tick(Agents, 1.0);

        _sut.End();

        var line = _logger.Lines.Last();
        StringAssert.StartsWith(line, "INFO ");
        StringAssert.Contains(line, "pool 2 peak: 235930 of 262144 entries (90.0 %), over 90 %");
    }

    [TestMethod]
    public void End_Pool2AtNinetyPercentGuardedByAnotherModule_LogsAtInfo()
    {
        WithPool2(pool2Guarded: true, pool2Counter: 0);
        _memory.ReadInt32(FillA).Returns(100);
        _memory.ReadInt32(Pool2B).Returns(235930);
        _sut.Begin();
        _sut.Tick(Agents, 1.0);

        _sut.End();

        StringAssert.StartsWith(_logger.Lines.Last(), "INFO ");
    }

    [TestMethod]
    public void Tick_Pool2AtNinetyPercentWithoutAnyGuard_ShowsNoOnScreenWarning()
    {
        WithPool2(guarded: false, counter: 0);
        _memory.ReadInt32(FillA).Returns(100);
        _memory.ReadInt32(Pool2A).Returns(260000);
        _sut.Begin();

        _sut.Tick(Agents, 1.0);

        _engine.DidNotReceiveWithAnyArgs().ShowWarning(default);
    }

    [TestMethod]
    public void Tick_Pool1AtNinetyPercentWithoutAGuard_StillWarnsOnScreenWhenPool2IsWatched()
    {
        WithPool2(guarded: false, counter: 0);
        _memory.ReadInt32(FillA).Returns(60000);
        _sut.Begin();

        _sut.Tick(Agents, 1.0);

        _engine.Received(1).ShowWarning(91);
    }

    [TestMethod]
    public void Begin_Twice_StartsFromAFreshPool2Peak()
    {
        WithPool2();
        _memory.ReadInt32(FillA).Returns(1);
        _memory.ReadInt32(Pool2A).Returns(200000);
        _sut.Begin();
        _sut.Tick(Agents, 1.0);
        _memory.ReadInt32(Pool2A).Returns(10);

        _sut.Begin();
        _sut.Tick(Agents, 1.0);
        _sut.End();

        StringAssert.Contains(_logger.Lines.Last(), "pool 2 peak: 10 of");
    }

    // ---- the second pool's guard ----

    [TestMethod]
    public void End_Pool2GuardedWithNoOverflows_SaysZeroNextToItsPeakAtInfoLevel()
    {
        WithGuardedPool2();
        _sut.Begin();
        _sut.Tick(Agents, 1.0);

        _sut.End();

        var line = _logger.Lines.Last();
        StringAssert.StartsWith(line, "INFO ");
        StringAssert.EndsWith(line, "guard overflows this mission: 0; pool 2 peak: 100 of 262144 entries (0.0 %); guard overflows this mission: 0");
    }

    [TestMethod]
    public void End_Pool2Overflowed_ReportsItsCounterDeltaAndIsAWarningEvenWhenPool1HadNone()
    {
        WithGuardedPool2();
        _memory.ReadInt32(Counter2).Returns(10, 14);   // at Begin, then at End
        _sut.Begin();
        _sut.Tick(Agents, 1.0);

        _sut.End();

        var line = _logger.Lines.Last();
        StringAssert.StartsWith(line, "WARN ");
        StringAssert.Contains(line, "into the mission; guard overflows this mission: 0; pool 2 peak: 100 of 262144");
        StringAssert.EndsWith(line, "; guard overflows this mission: 4");
    }

    [TestMethod]
    public void End_BothPoolsOverflowed_EachDeltaIsItsOwn()
    {
        WithGuardedPool2();
        _memory.ReadInt32(Counter).Returns(3, 7);
        _memory.ReadInt32(Counter2).Returns(100, 102);
        _sut.Begin();
        _sut.Tick(Agents, 1.0);

        _sut.End();

        var line = _logger.Lines.Last();
        StringAssert.StartsWith(line, "WARN ");
        StringAssert.Contains(line, "into the mission; guard overflows this mission: 4; pool 2 peak");
        StringAssert.EndsWith(line, "; guard overflows this mission: 2");
    }

    [TestMethod]
    public void End_Pool1OverflowedAndPool2Not_IsAWarningWithTheRightNotes()
    {
        WithGuardedPool2();
        _memory.ReadInt32(Counter).Returns(3, 4);
        _sut.Begin();
        _sut.Tick(Agents, 1.0);

        _sut.End();

        var line = _logger.Lines.Last();
        StringAssert.StartsWith(line, "WARN ");
        StringAssert.Contains(line, "into the mission; guard overflows this mission: 1; pool 2 peak");
        StringAssert.EndsWith(line, "; guard overflows this mission: 0");
    }

    [TestMethod]
    public void End_Pool2CounterUnreadableAtTheEnd_SaysSo()
    {
        WithGuardedPool2();
        _sut.Begin();
        _sut.Tick(Agents, 1.0);
        _memory.ReadInt32(Counter2).Returns((int?)null);

        _sut.End();

        StringAssert.EndsWith(_logger.Lines.Last(), "; guard overflow count unreadable");
        StringAssert.Contains(_logger.Lines.Last(), "guard overflows this mission: 0; pool 2 peak", "pool 1's note is its own");
    }

    [TestMethod]
    public void End_Pool2CounterUnreadableAtBegin_SaysSoEvenWhenItReadsLater()
    {
        WithGuardedPool2();
        _memory.ReadInt32(Counter2).Returns((int?)null, 50);
        _sut.Begin();
        _sut.Tick(Agents, 1.0);

        _sut.End();

        StringAssert.EndsWith(_logger.Lines.Last(), "; guard overflow count unreadable");
    }

    [TestMethod]
    public void End_Pool2WatchedWithoutAGuard_SaysNoGuardInstalledAndReadsNoCounter()
    {
        WithPool2();
        _memory.ReadInt32(FillA).Returns(100);
        _sut.Begin();
        _sut.Tick(Agents, 1.0);

        _sut.End();

        StringAssert.EndsWith(_logger.Lines.Last(), "pool 2 peak: 0 of 262144 entries (0.0 %); no guard installed");
        _memory.DidNotReceive().ReadInt32(Counter2);
    }

    [TestMethod]
    public void End_Pool2GuardedByAnotherModule_SaysSo()
    {
        WithPool2(pool2Guarded: true, pool2Counter: 0);
        _memory.ReadInt32(FillA).Returns(100);
        _sut.Begin();
        _sut.Tick(Agents, 1.0);

        _sut.End();

        StringAssert.EndsWith(_logger.Lines.Last(), "pool 2 peak: 0 of 262144 entries (0.0 %); guarded by another module");
        _memory.DidNotReceive().ReadInt32(Counter2);
    }

    [TestMethod]
    public void Begin_Pool2CounterReadThrows_TheLineSaysTheWatchDidNotStart()
    {
        WithGuardedPool2();
        _memory.ReadInt32(Counter2).Returns(_ => throw new InvalidOperationException("boom"));

        _sut.Begin();
        _sut.Tick(Agents, 1.0);
        _sut.End();

        Assert.AreEqual(1, _logger.Lines.Count);
        StringAssert.Contains(_logger.Lines[0], "the watch could not start for this mission: InvalidOperationException: boom");
    }

    [TestMethod]
    public void Begin_Twice_ReadsBothBaselinesAgain()
    {
        WithGuardedPool2();
        _memory.ReadInt32(Counter2).Returns(10, 12, 12, 20);   // Begin, End, Begin, End... only the second pair counts
        _sut.Begin();
        _sut.Tick(Agents, 1.0);
        _sut.End();
        _sut.Begin();
        _sut.Tick(Agents, 1.0);

        _sut.End();

        StringAssert.EndsWith(_logger.Lines.Last(), "; guard overflows this mission: 8");
    }

    [TestMethod]
    public void Tick_Pool1UnguardedButPool2Guarded_StillWarnsOnScreenForPool1()
    {
        _guard.Target.Returns(new SkeletonBufferTarget(Global, pool1Guarded: false, pool1CounterAddress: 0, watchPool2: true,
            pool2Guarded: true, pool2CounterAddress: Counter2));
        _memory.ReadInt32(Counter2).Returns(0);
        _memory.ReadInt32(Pool2A).Returns(0);
        _memory.ReadInt32(Pool2B).Returns(0);
        _memory.ReadInt32(FillA).Returns(60000);
        _sut.Begin();

        _sut.Tick(Agents, 1.0);

        _engine.Received(1).ShowWarning(91);
    }

    [TestMethod]
    public void Tick_Pool1GuardedButPool2NotGuarded_Pool1ShowsNothing()
    {
        WithPool2(guarded: true, pool2Guarded: false);
        _memory.ReadInt32(FillA).Returns(65000);
        _sut.Begin();

        _sut.Tick(Agents, 1.0);

        _engine.DidNotReceiveWithAnyArgs().ShowWarning(default);
    }

    // ---- the fault lines ----

    [TestMethod]
    public void Tick_AdapterThrows_TheLineSaysOneReadFailedAndTheWatchGoesOn()
    {
        _memory.ReadInt64(Global).Returns(_ => throw new InvalidOperationException("boom"));
        _sut.Begin();

        _sut.Tick(Agents, 1.0);

        StringAssert.Contains(_logger.Lines.Single(), "one watch read failed (InvalidOperationException: boom); the watch goes on");
    }

    [TestMethod]
    public void Tick_AdapterThrowsOnceThenRecovers_TheWatchKeepsReading()
    {
        _memory.ReadInt64(Global).Returns(_ => throw new InvalidOperationException("boom"), _ => (long?)Pointer);
        _memory.ReadInt32(FillA).Returns(4321);
        _sut.Begin();

        _sut.Tick(Agents, 1.0);
        _sut.Tick(Agents, 2.0);
        _sut.End();

        StringAssert.Contains(_logger.Lines.Last(), "peak: 4321 of 65536");
    }

    [TestMethod]
    public void Begin_TheCounterReadThrows_TheLineSaysTheWatchDidNotStartAndNothingIsRead()
    {
        _memory.ReadInt32(Counter).Returns(_ => throw new InvalidOperationException("boom"));

        _sut.Begin();
        _sut.Tick(Agents, 1.0);
        _sut.End();

        Assert.AreEqual(1, _logger.Lines.Count);
        StringAssert.Contains(_logger.Lines[0], "the watch could not start for this mission: InvalidOperationException: boom");
        _memory.DidNotReceiveWithAnyArgs().ReadInt64(default);
    }

    [TestMethod]
    public void End_TheLoggerThrows_IsSwallowed()
    {
        var logger = Substitute.For<TAOM.Core.Logging.IModLogger>();
        logger.When(l => l.LogInfo(Arg.Any<string>())).Do(_ => throw new InvalidOperationException("log closed"));
        var sut = new SkeletonBufferWatchService(_guard, _memory, _engine, _settings, _server, logger);
        _memory.ReadInt32(FillA).Returns(100);
        sut.Begin();
        sut.Tick(Agents, 1.0);

        sut.End();

        logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("the mission-end peak line could not be written")));
    }

    // ---- the guard note ----

    [TestMethod]
    public void End_GuardedWithNoOverflows_SaysZeroAtInfoLevel()
    {
        _memory.ReadInt32(FillA).Returns(100);
        _sut.Begin();
        _sut.Tick(Agents, 1.0);

        _sut.End();

        var line = _logger.Lines.Last();
        StringAssert.StartsWith(line, "INFO ");
        StringAssert.EndsWith(line, "guard overflows this mission: 0");
    }

    [TestMethod]
    public void End_GuardedWithOverflows_ReportsTheCounterDeltaAsAWarning()
    {
        _memory.ReadInt32(FillA).Returns(65500);
        _memory.ReadInt32(Counter).Returns(3, 7);   // at Begin, then at End
        _sut.Begin();
        _sut.Tick(Agents, 1.0);

        _sut.End();

        var line = _logger.Lines.Last();
        StringAssert.StartsWith(line, "WARN ");
        StringAssert.EndsWith(line, "guard overflows this mission: 4");
    }

    [TestMethod]
    public void End_CounterUnreadableAtTheEnd_SaysSo()
    {
        _memory.ReadInt32(FillA).Returns(100);
        _sut.Begin();
        _sut.Tick(Agents, 1.0);
        _memory.ReadInt32(Counter).Returns((int?)null);

        _sut.End();

        StringAssert.EndsWith(_logger.Lines.Last(), "guard overflow count unreadable");
    }

    [TestMethod]
    public void End_NoGuard_SaysNoGuardInstalled()
    {
        Unguarded();
        _memory.ReadInt32(FillA).Returns(100);
        _sut.Begin();
        _sut.Tick(Agents, 1.0);

        _sut.End();

        StringAssert.EndsWith(_logger.Lines.Last(), "no guard installed");
        _memory.DidNotReceive().ReadInt32(Counter);
    }

    [TestMethod]
    public void End_AnotherModulesGuard_SaysGuardedByAnotherModule()
    {
        _guard.Target.Returns(new SkeletonBufferTarget(Global, pool1Guarded: true, pool1CounterAddress: 0));
        _memory.ReadInt32(FillA).Returns(100);
        _sut.Begin();
        _sut.Tick(Agents, 1.0);

        _sut.End();

        StringAssert.EndsWith(_logger.Lines.Last(), "guarded by another module");
    }

    // ---- the on-screen warning ----

    [TestMethod]
    public void Tick_NinetyPercentWithoutAGuard_ShowsTheWarningOnce()
    {
        Unguarded();
        _memory.ReadInt32(FillA).Returns(60000);   // 91.6 %
        _sut.Begin();

        _sut.Tick(Agents, 1.0);
        _sut.Tick(Agents, 2.0);

        _engine.Received(1).ShowWarning(91);
    }

    [TestMethod]
    public void Tick_NinetyPercentWithOurGuard_ShowsNothing()
    {
        _memory.ReadInt32(FillA).Returns(65000);
        _sut.Begin();

        _sut.Tick(Agents, 1.0);

        _engine.DidNotReceiveWithAnyArgs().ShowWarning(default);
    }

    [TestMethod]
    public void Tick_NinetyPercentWithAnotherModulesGuard_ShowsNothing()
    {
        _guard.Target.Returns(new SkeletonBufferTarget(Global, pool1Guarded: true, pool1CounterAddress: 0));
        _memory.ReadInt32(FillA).Returns(65000);
        _sut.Begin();

        _sut.Tick(Agents, 1.0);

        _engine.DidNotReceiveWithAnyArgs().ShowWarning(default);
    }

    [TestMethod]
    public void Tick_NinetyPercentOnADedicatedServer_ShowsNothing()
    {
        Unguarded();
        _server.IsDedicatedServer.Returns(true);
        _memory.ReadInt32(FillA).Returns(65000);
        _sut.Begin();

        _sut.Tick(Agents, 1.0);

        _engine.DidNotReceiveWithAnyArgs().ShowWarning(default);
    }

    [TestMethod]
    public void Tick_BelowNinetyPercentWithoutAGuard_ShowsNothing()
    {
        Unguarded();
        _memory.ReadInt32(FillA).Returns(30000);
        _sut.Begin();

        _sut.Tick(Agents, 1.0);

        _engine.DidNotReceiveWithAnyArgs().ShowWarning(default);
    }

    [TestMethod]
    public void Tick_WarningInANewMission_ShowsAgain()
    {
        Unguarded();
        _memory.ReadInt32(FillA).Returns(65000);
        _sut.Begin();
        _sut.Tick(Agents, 1.0);
        _sut.End();

        _sut.Begin();
        _sut.Tick(Agents, 1.0);

        _engine.Received(2).ShowWarning(99);
    }

    // ---- nothing escapes ----

    [TestMethod]
    public void Tick_AdapterThrows_IsSwallowedAndLoggedOnce()
    {
        _memory.ReadInt64(Global).Returns(_ => throw new InvalidOperationException("boom"));
        _sut.Begin();

        _sut.Tick(Agents, 1.0);
        _sut.Tick(Agents, 2.0);

        Assert.AreEqual(1, _logger.Lines.Count);
        StringAssert.Contains(_logger.Lines[0], "InvalidOperationException");
    }

    [TestMethod]
    public void Begin_AdapterThrows_IsSwallowed()
    {
        _settings.WatchEnabled.Returns(_ => throw new InvalidOperationException("boom"));

        _sut.Begin();
        _sut.Tick(Agents, 1.0);
        _sut.End();

        StringAssert.Contains(_logger.Lines[0], "InvalidOperationException");
    }

    [TestMethod]
    public void Tick_WarningThrows_IsSwallowed()
    {
        Unguarded();
        _memory.ReadInt32(FillA).Returns(65000);
        _engine.When(e => e.ShowWarning(Arg.Any<int>())).Do(_ => throw new InvalidOperationException("ui"));
        _sut.Begin();

        _sut.Tick(Agents, 1.0);
        _sut.End();

        StringAssert.Contains(_logger.Lines.Last(), "mission peak");
    }
}
