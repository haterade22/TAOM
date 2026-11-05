using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.MissionPerf;

namespace TAOM.Tests.Features.MissionPerf;

/// <summary>
/// Which Patch98 window lines are written, in which order, at which level: right after
/// <c>[TickProfile]</c>, <c>[SpawnProfile]</c> only for a window with spawns, then <c>[ScriptProfile]</c>, then
/// <c>[AnimLoad]</c> only while the sampler is on. All INFO (the mission-end line: HitchProbeMissionFlowTests).
/// A real profiler at ticksPerSecond 1000 is driven through two frames.
/// </summary>
[TestClass]
public class ProbeWindowWriterTests
{
    private IModLogger _logger = null!;
    private MissionTickProfiler _profiler = null!;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _profiler = new MissionTickProfiler(1000);
        _profiler.BeginMission(0, Environment.CurrentManagedThreadId, measuring: true, hitchThresholdMs: 100000, behaviorTiming: false);
    }

    private void TwoFrames(bool spawns)
    {
        _profiler.CloseFrame(0, 0, 0, 0, 0);
        if (spawns)
        {
            _profiler.CountSpawn();
            _profiler.AddSpawnTime(3);
        }
        _profiler.AddScriptTick(2);
        _profiler.MarkAnimLoading(true);
        _profiler.CloseFrame(16, 0, 0, 0, 0);
        _profiler.CloseFrame(32, 0, 0, 0, 0);
    }

    private string[] InfoLines() => _logger.ReceivedCalls()
        .Where(c => c.GetMethodInfo().Name == nameof(IModLogger.LogInfo))
        .Select(c => (string)c.GetArguments()[0]!)
        .ToArray();

    [TestMethod]
    public void WriteExtras_ProbeMode_WritesScriptProfileThenAnimLoad()
    {
        _profiler.AnimSampling = true;
        TwoFrames(spawns: false);
        var w = _profiler.TakeExtrasWindow(8);

        ProbeWindowWriter.WriteExtras(_logger, 5.2, w);

        Received.InOrder(() =>
        {
            _logger.LogInfo(HitchProbeLines.BuildScriptProfile(5.2, w));
            _logger.LogInfo(HitchProbeLines.BuildAnimLoad(5.2, w));
        });
        var lines = InfoLines();
        Assert.AreEqual(2, lines.Length);
        StringAssert.StartsWith(lines[0], "[ScriptProfile] ");
        StringAssert.StartsWith(lines[1], "[AnimLoad] t=+5s loadingFrames=1 frames=2");
        Assert.AreEqual(2, _logger.ReceivedCalls().Count(), "Nothing but the two INFO lines.");
    }

    [TestMethod]
    public void WriteExtras_WithSpawns_WritesSpawnProfileFirst()
    {
        _profiler.AnimSampling = true;
        TwoFrames(spawns: true);
        var w = _profiler.TakeExtrasWindow(8);

        ProbeWindowWriter.WriteExtras(_logger, 5.2, w);

        Received.InOrder(() =>
        {
            _logger.LogInfo(HitchProbeLines.BuildSpawnProfile(5.2, w));
            _logger.LogInfo(HitchProbeLines.BuildScriptProfile(5.2, w));
            _logger.LogInfo(HitchProbeLines.BuildAnimLoad(5.2, w));
        });
        var lines = InfoLines();
        Assert.AreEqual(3, lines.Length);
        StringAssert.StartsWith(lines[0], "[SpawnProfile] t=+5s spawns=1 spawnMs=3.00 top=none");
    }

    [TestMethod]
    public void WriteExtras_SamplerOff_WritesNoAnimLoad()
    {
        TwoFrames(spawns: false);
        var w = _profiler.TakeExtrasWindow(8);

        ProbeWindowWriter.WriteExtras(_logger, 5.2, w);

        Received.InOrder(() => _logger.LogInfo(HitchProbeLines.BuildScriptProfile(5.2, w)));
        var lines = InfoLines();
        Assert.AreEqual(1, lines.Length);
        StringAssert.StartsWith(lines[0], "[ScriptProfile] ");
    }
}
