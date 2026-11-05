using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.BattleLoadDiagnostics;

namespace TAOM.Tests.Features.BattleLoadDiagnostics;

/// <summary>
/// A player's game froze mid-battle with the heap flat and no exception (#634). The engine's next
/// frame spins in <c>Mission.WaitTickCompletion</c> until the asynchronous agent tick finishes, so a
/// stuck agent tick freezes the game with nothing in the log. The watchdog reads two in-flight probes
/// (the agent tick and the main thread's whole mission frame) from a timer thread and photographs the
/// stuck thread.
/// </summary>
[TestClass]
public class MissionTickStallWatchdogTests
{
    private static readonly long T0 = new DateTime(2026, 9, 22, 12, 0, 0, DateTimeKind.Utc).Ticks;
    private static long At(double seconds) => T0 + TimeSpan.FromSeconds(seconds).Ticks;

    private IModLogger _logger = null!;
    private IBattleLoadDiagnosticsSettingsProvider _settings = null!;
    private IBattleLoadDiagnosticsService _service = null!;
    private MissionTickStallProbe _asyncTick = null!;
    private MissionTickStallProbe _missionFrame = null!;
    private MissionTickStallWatchdog _sut = null!;
    private List<string> _errors = null!;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _errors = new List<string>();
        _logger.When(l => l.LogError(Arg.Any<string>())).Do(c => _errors.Add(c.Arg<string>()));
        _settings = Substitute.For<IBattleLoadDiagnosticsSettingsProvider>();
        _settings.MissionTickStallSamplerEnabled.Returns(true);
        _service = Substitute.For<IBattleLoadDiagnosticsService>();
        _asyncTick = new MissionTickStallProbe("async agent tick");
        _missionFrame = new MissionTickStallProbe("mission frame");
        _sut = new MissionTickStallWatchdog(_logger, _settings, _service, _missionFrame, _asyncTick)
        {
            DescribeStack = thread => $"    at Fake.Frame(thread {thread.ManagedThreadId})",
        };
    }

    [TestMethod]
    public void NothingInFlight_LogsNothing()
    {
        _sut.Poll(At(100));

        Assert.AreEqual(0, _errors.Count);
    }

    [TestMethod]
    public void ATickShorterThanTheFirstThreshold_LogsNothing()
    {
        _asyncTick.EnterAt(T0);

        _sut.Poll(At(9.9));

        Assert.AreEqual(0, _errors.Count);
    }

    [TestMethod]
    public void AStuckAgentTick_IsPhotographedAtTenTwentyAndFortySeconds_ThenLeftAlone()
    {
        _asyncTick.EnterAt(T0);

        foreach (var s in new[] { 10.0, 11, 12, 20, 21, 40, 41, 80, 300 })
            _sut.Poll(At(s));

        Assert.AreEqual(3, _errors.Count);
        StringAssert.Contains(_errors[0], "[MissionStall]");
        StringAssert.Contains(_errors[0], "async agent tick");
        StringAssert.Contains(_errors[0], "sample#1");
        StringAssert.Contains(_errors[0], $"thread {Thread.CurrentThread.ManagedThreadId}");
        StringAssert.Contains(_errors[0], "Fake.Frame");
        StringAssert.Contains(_errors[2], "sample#3");
    }

    [TestMethod]
    public void ATickThatFinishes_StartsAFreshEpisodeNextTime()
    {
        _asyncTick.EnterAt(T0);
        _sut.Poll(At(10));
        _asyncTick.Exit();
        _sut.Poll(At(11));

        _asyncTick.EnterAt(At(50));
        _sut.Poll(At(60));

        Assert.AreEqual(2, _errors.Count);
        StringAssert.Contains(_errors[1], "sample#1");
    }

    [TestMethod]
    public void AStuckMissionFrame_NamesTheMissionFrame()
    {
        _missionFrame.EnterAt(T0);

        _sut.Poll(At(12));

        Assert.AreEqual(1, _errors.Count);
        StringAssert.Contains(_errors[0], "mission frame");
    }

    // Fast-forward runs the agent tick inline, inside the main frame, on the same thread: one stack
    // answers both, so the thread is suspended once and the outer frame is the one reported.
    [TestMethod]
    public void AnAgentTickNestedInTheFrameOnTheSameThread_IsPhotographedOnceAsTheFrame()
    {
        _missionFrame.EnterAt(T0);
        _asyncTick.EnterAt(At(0.1));

        _sut.Poll(At(12));

        Assert.AreEqual(1, _errors.Count);
        StringAssert.Contains(_errors[0], "mission frame");
    }

    // The frozen shape in #634: the agent tick stuck on its own thread, the next frame waiting for it.
    [TestMethod]
    public void AStuckAgentTickOnItsOwnThread_AndTheWaitingFrame_AreEachPhotographed()
    {
        var worker = new Thread(() => _asyncTick.EnterAt(T0));
        worker.Start();
        worker.Join();
        _missionFrame.EnterAt(At(1));

        _sut.Poll(At(12));

        Assert.AreEqual(2, _errors.Count);
        Assert.AreEqual(1, _errors.Count(e => e.Contains("async agent tick") && e.Contains("WaitTickCompletion")));
        Assert.AreEqual(1, _errors.Count(e => e.Contains("mission frame")));
    }

    // Crash forensics, not phase logging: like the memory sampler it answers to its own switch only,
    // and costs nothing until a frame has already been stuck for 10 s.
    [TestMethod]
    public void MasterDiagnosticsToggleOff_StillSamples()
    {
        _settings.IsEnabled.Returns(false);
        _asyncTick.EnterAt(T0);

        _sut.Poll(At(10));

        Assert.AreEqual(1, _errors.Count);
    }

    [TestMethod]
    public void Disabled_LogsNothing()
    {
        _settings.MissionTickStallSamplerEnabled.Returns(false);
        _asyncTick.EnterAt(T0);

        _sut.Poll(At(60));

        Assert.AreEqual(0, _errors.Count);
    }

    // ExitStallSampler owns the exit window and suspends the main thread itself; two samplers must
    // never suspend the same thread at once.
    [TestMethod]
    public void DuringTheExitWindow_LeavesTheMainThreadToTheExitSampler()
    {
        _service.IsExitWindowActive.Returns(true);
        _missionFrame.EnterAt(T0);

        _sut.Poll(At(60));

        Assert.AreEqual(0, _errors.Count);
    }

    [TestMethod]
    public void ACaptureThatThrows_IsReportedWithoutEscapingTheTimer()
    {
        _sut.DescribeStack = _ => throw new InvalidOperationException("thread gone");
        _asyncTick.EnterAt(T0);

        _sut.Poll(At(10));

        Assert.AreEqual(1, _errors.Count, "the stall itself is still reported");
        StringAssert.Contains(_errors[0], "thread gone");
    }

    [TestMethod]
    public void Start_CalledTwice_ReusesTheSameTimer()
    {
        var field = typeof(MissionTickStallWatchdog).GetField("_timer", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, "MissionTickStallWatchdog._timer was renamed; this guard no longer sees the timer.");
        try
        {
            _sut.Start();
            var first = field.GetValue(_sut);
            Assert.IsNotNull(first);

            _sut.Start();

            Assert.AreSame(first, field.GetValue(_sut), "every game initialisation calls Start(); a second timer would double every sample");
        }
        finally
        {
            _sut.Dispose();
        }
    }

    [TestMethod]
    public void Probe_EnterRecordsTheCallingThread_ExitClearsIt()
    {
        var probe = new MissionTickStallProbe("x");
        Thread worker = null!;
        var t = new Thread(() => { worker = Thread.CurrentThread; probe.EnterAt(T0); });
        t.Start();
        t.Join();

        Assert.IsTrue(probe.TryRead(out var since, out var thread));
        Assert.AreEqual(T0, since);
        Assert.AreSame(worker, thread);

        probe.Exit();

        Assert.IsFalse(probe.TryRead(out _, out _));
    }
}
