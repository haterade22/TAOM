using System;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Logging;
using TAOM.Features.MissionPerf;
using TAOM.Features.MissionPerf.Hooks;

namespace TAOM.Tests.Features.MissionPerf;

/// <summary>
/// The static helpers the Patch97 transpiler calls in place of the behaviour virtuals, the frame
/// boundary, the agent-tick bracket and the mission-end summary, driven with a real
/// <see cref="MissionLogic"/> subclass (so the class needs the game assemblies) and real
/// <see cref="Stopwatch"/> timestamps.
/// </summary>
[TestCategory("RequiresGame")]
[TestClass]
public class MissionTickProfilerHooksTests
{
    private IModLogger _logger = null!;
    private int _generation;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        MissionTickProfilerHooks.Profiler = new MissionTickProfiler(Stopwatch.Frequency);
        MissionTickProfilerHooks.Logger = _logger;
        _generation = MissionTickProfilerHooks.BeginMission(Stopwatch.GetTimestamp(), measuring: true, hitchThresholdMs: 100000);
    }

    [TestCleanup]
    public void Cleanup()
    {
        MissionTickProfilerHooks.Profiler = null;
        MissionTickProfilerHooks.Logger = null;
        MissionTickProfilerHooks.WaitTickCompletionCall = null;
        MissionTickProfilerHooks.Installed = false;
        MissionTickProfilerHooks.OnTickSites = 0;
        MissionTickProfilerHooks.OnPreTickSites = 0;
    }

    private static void Restart(double hitchMs) =>
        MissionTickProfilerHooks.BeginMission(Stopwatch.GetTimestamp(), measuring: true, hitchThresholdMs: hitchMs);

    private string[] InfoLines() => _logger.ReceivedCalls()
        .Where(c => c.GetMethodInfo().Name == nameof(IModLogger.LogInfo))
        .Select(c => (string)c.GetArguments()[0]!)
        .ToArray();

    private static string Field(string line, string key) => Regex.Match(line, " " + key + "=([^ ]+)").Groups[1].Value;

    [TestMethod]
    public void TimedMissionTick_Measuring_CallsOnMissionTickOnceAndRecordsOneCall()
    {
        var probe = new ProbeBehavior();
        MissionTickProfilerHooks.OnFrameBoundary();
        MissionTickProfilerHooks.TimedMissionTick(probe, 0.016f);
        MissionTickProfilerHooks.OnFrameBoundary();

        Assert.AreEqual(1, probe.MissionTicks);
        var top = MissionTickProfilerHooks.Profiler!.TakeWindow(8).Top.Single();
        Assert.AreEqual(nameof(ProbeBehavior), top.Name);
        Assert.AreEqual(1, top.Calls);
    }

    [TestMethod]
    public void TimedMissionTick_NotMeasuring_CallsThroughAndRecordsNothing()
    {
        var probe = new ProbeBehavior();
        MissionTickProfilerHooks.EndMission(_generation);
        MissionTickProfilerHooks.TimedMissionTick(probe, 0.016f);

        Assert.AreEqual(1, probe.MissionTicks);
        Assert.AreEqual(0, MissionTickProfilerHooks.Profiler!.Behaviors.MissionTop(8, Stopwatch.Frequency).Count);
        Assert.AreEqual(0, MissionTickProfilerHooks.Profiler.Behaviors.FrameTop(8, Stopwatch.Frequency).Count);
    }

    [TestMethod]
    public void TimedMissionTick_BehaviourThrows_PropagatesTheSameExceptionAndRecordsTheCall()
    {
        var thrown = new InvalidOperationException("behaviour broke");
        var probe = new ProbeBehavior { Throw = thrown };
        MissionTickProfilerHooks.OnFrameBoundary();

        var caught = Assert.ThrowsException<InvalidOperationException>(() => MissionTickProfilerHooks.TimedMissionTick(probe, 0.016f));
        MissionTickProfilerHooks.OnFrameBoundary();

        Assert.AreSame(thrown, caught);
        Assert.AreEqual(1, MissionTickProfilerHooks.Profiler!.TakeWindow(8).Top.Single().Calls);
    }

    [TestMethod]
    public void TimedPreDisplay_CallsOnPreDisplayMissionTick()
    {
        var probe = new ProbeBehavior();
        MissionTickProfilerHooks.TimedPreDisplay(probe, 0.016f);
        Assert.AreEqual(1, probe.PreDisplayTicks);
        Assert.AreEqual(0, probe.MissionTicks);
    }

    [TestMethod]
    public void TimedPreMissionTick_CallsOnPreMissionTick()
    {
        var probe = new ProbeBehavior();
        MissionTickProfilerHooks.TimedPreMissionTick(probe, 0.016f);
        Assert.AreEqual(1, probe.PreMissionTicks);
        Assert.AreEqual(0, probe.MissionTicks);
    }

    [TestMethod]
    public void OnFrameBoundary_FrameAboveThreshold_LogsOneHitchLine()
    {
        Restart(hitchMs: 1);
        MissionTickProfilerHooks.OnFrameBoundary();
        Thread.Sleep(5);
        MissionTickProfilerHooks.OnFrameBoundary();

        var lines = InfoLines();
        Assert.AreEqual(1, lines.Length);
        StringAssert.StartsWith(lines[0], "[Hitch] t=+");
    }

    [TestMethod]
    public void OnFrameBoundary_NotMeasuring_LogsNothing()
    {
        MissionTickProfilerHooks.BeginMission(Stopwatch.GetTimestamp(), measuring: false, hitchThresholdMs: 1);
        MissionTickProfilerHooks.OnFrameBoundary();
        Thread.Sleep(5);
        MissionTickProfilerHooks.OnFrameBoundary();

        Assert.AreEqual(0, _logger.ReceivedCalls().Count());
    }

    [TestMethod]
    public void AgentTick_EnterExitOnAnotherThread_IsCountedAsOffMain()
    {
        Restart(hitchMs: 1);
        MissionTickProfilerHooks.OnFrameBoundary();
        var worker = new Thread(() =>
        {
            MissionTickProfilerHooks.OnAgentTickEnter();
            Thread.Sleep(20);
            MissionTickProfilerHooks.OnAgentTickExit();
        });
        worker.Start();
        worker.Join();
        MissionTickProfilerHooks.OnFrameBoundary();

        var hitch = InfoLines().Single();
        Assert.IsTrue(double.Parse(Field(hitch, "agentTickMs"), System.Globalization.CultureInfo.InvariantCulture) >= 15d, hitch);
        Assert.AreEqual(Field(hitch, "frameMs"), Field(hitch, "otherMs"), hitch);
    }

    [TestMethod]
    public void AgentTick_ExitWithoutEnter_RecordsNothing()
    {
        Restart(hitchMs: 1);
        MissionTickProfilerHooks.OnFrameBoundary();
        Thread.Sleep(5);
        MissionTickProfilerHooks.OnAgentTickExit();
        MissionTickProfilerHooks.OnFrameBoundary();

        Assert.AreEqual("0.00", Field(InfoLines().Single(), "agentTickMs"));
    }

    [TestMethod]
    public void WriteSummary_CurrentMission_LogsOneSummary_AndAStaleEndOnlyLogsWhy()
    {
        var probe = new ProbeBehavior();
        MissionTickProfilerHooks.OnFrameBoundary();
        MissionTickProfilerHooks.TimedMissionTick(probe, 0.016f);
        MissionTickProfilerHooks.OnFrameBoundary();

        MissionTickProfilerHooks.WriteSummary(_generation, 8);
        var newer = MissionTickProfilerHooks.BeginMission(Stopwatch.GetTimestamp(), measuring: true, hitchThresholdMs: 100000);
        MissionTickProfilerHooks.WriteSummary(_generation, 8);
        MissionTickProfilerHooks.EndMission(_generation);

        var lines = InfoLines();
        Assert.AreEqual(2, lines.Length, string.Join(" | ", lines));
        StringAssert.StartsWith(lines[0], "[TickSummary] frames=1 wallMs=");
        StringAssert.Contains(lines[0], " top=" + nameof(ProbeBehavior) + ":");
        Assert.AreEqual(TickProfileLines.BuildStaleEndLine(_generation, newer), lines[1]);
        Assert.IsTrue(MissionTickProfilerHooks.Profiler!.Measuring, "The older mission's end must not stop the newer one.");
    }

    [TestMethod]
    public void OnFrameBoundary_PastTheHitchLineCap_WritesTheCapLineOnceAndNoMoreHitchLines()
    {
        Restart(hitchMs: 0);
        MissionTickProfilerHooks.OnFrameBoundary();
        for (var i = 0; i < MissionTickProfiler.MaxHitchLinesPerMission + 3; i++)
            MissionTickProfilerHooks.OnFrameBoundary();

        var lines = InfoLines();
        Assert.AreEqual(MissionTickProfiler.MaxHitchLinesPerMission, lines.Count(l => l.StartsWith("[Hitch] ")));
        Assert.AreEqual(1, lines.Count(l => l.StartsWith("[TickProfiler] hitch line cap reached at t=+")));
        Assert.AreEqual(MissionTickProfiler.MaxHitchLinesPerMission + 3, MissionTickProfilerHooks.Profiler!.Summarize(8).Hitches);
    }

    [TestMethod]
    public void TimedWaitTickCompletion_Measuring_CallsTheWaitAndRecordsItsTime()
    {
        var calls = 0;
        MissionTickProfilerHooks.WaitTickCompletionCall = _ => { calls++; Thread.Sleep(10); };
        MissionTickProfilerHooks.OnFrameBoundary();
        MissionTickProfilerHooks.TimedWaitTickCompletion(null!);
        MissionTickProfilerHooks.OnFrameBoundary();

        Assert.AreEqual(1, calls);
        Assert.IsTrue(MissionTickProfilerHooks.Profiler!.TakeWindow(8).WaitTickMs >= 8d);
    }

    [TestMethod]
    public void TimedWaitTickCompletion_NotMeasuring_CallsThrough()
    {
        var calls = 0;
        MissionTickProfilerHooks.WaitTickCompletionCall = _ => calls++;
        MissionTickProfilerHooks.EndMission(_generation);
        MissionTickProfilerHooks.TimedWaitTickCompletion(null!);

        Assert.AreEqual(1, calls);
    }

    [TestMethod]
    public void OnFrameBoundary_LoggerThrows_StopsMeasuringAndLogsOneFault()
    {
        Restart(hitchMs: 0);
        _logger.When(x => x.LogInfo(Arg.Any<string>())).Do(_ => throw new System.IO.IOException("disk"));
        MissionTickProfilerHooks.OnFrameBoundary();
        MissionTickProfilerHooks.OnFrameBoundary();
        MissionTickProfilerHooks.OnFrameBoundary();

        Assert.IsFalse(MissionTickProfilerHooks.Profiler!.Measuring);
        _logger.Received(1).LogError(Arg.Is<string>(s => s.StartsWith("[TickProfiler] frame boundary failed, measuring stopped: IOException: disk")));
    }

    [TestMethod]
    public void AgentTick_NoProfiler_DoesNothing()
    {
        MissionTickProfilerHooks.Profiler = null;
        MissionTickProfilerHooks.OnAgentTickEnter();
        MissionTickProfilerHooks.OnAgentTickExit();
        MissionTickProfilerHooks.OnFrameBoundary();

        Assert.AreEqual(0, _logger.ReceivedCalls().Count());
        Assert.AreEqual(0, MissionTickProfilerHooks.BeginMission(Stopwatch.GetTimestamp(), measuring: true, hitchThresholdMs: 1));
    }

    [TestMethod]
    public void WriteSummary_NoFrameClosed_LogsWhyThereIsNoSummary()
    {
        MissionTickProfilerHooks.WriteSummary(_generation, 8);

        Assert.AreEqual(TickProfileLines.BuildNoFramesLine(_generation), InfoLines().Single());
    }

    private sealed class ProbeBehavior : MissionLogic
    {
        internal Exception? Throw;
        internal int MissionTicks;
        internal int PreDisplayTicks;
        internal int PreMissionTicks;

        public override void OnMissionTick(float dt)
        {
            MissionTicks++;
            if (Throw != null)
                throw Throw;
        }

        public override void OnPreDisplayMissionTick(float dt) => PreDisplayTicks++;

        public override void OnPreMissionTick(float dt) => PreMissionTicks++;
    }
}
