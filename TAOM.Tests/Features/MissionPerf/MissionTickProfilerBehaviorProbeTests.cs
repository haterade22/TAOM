using System.Diagnostics;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.BattleLoadDiagnostics;
using TAOM.Features.MissionPerf;
using TAOM.Features.MissionPerf.Hooks;

namespace TAOM.Tests.Features.MissionPerf;

/// <summary>
/// The default-on mission flow (plan 041, review 041): with Patch98 installed and "Enable Hitch Probe" on, a
/// mission measures in probe mode without behaviour timing, and the tick profiler's not-timing lines say the
/// probe still measures instead of "nothing is measured". Constructs a <c>MissionLogic</c>, so it needs the
/// game assemblies.
/// </summary>
[TestCategory("RequiresGame")]
[TestClass]
public class MissionTickProfilerBehaviorProbeTests
{
    private IModLogger _logger = null!;
    private IBattleLoadDiagnosticsSettingsProvider _settings = null!;

    [TestInitialize]
    public void Setup()
    {
        Reset();
        _logger = Substitute.For<IModLogger>();
        _settings = Substitute.For<IBattleLoadDiagnosticsSettingsProvider>();
        _settings.TickProfilerTopN.Returns(8);
        _settings.HitchThresholdMs.Returns(250d);
        MissionTickProfilerHooks.Profiler = new MissionTickProfiler(Stopwatch.Frequency);
        MissionTickProfilerHooks.Logger = _logger;
        HitchProbeInstaller.ProbeInstalled = true;
    }

    [TestCleanup]
    public void Cleanup() => Reset();

    private static void Reset()
    {
        HitchProbeInstaller.ResetForTests();
        HitchProbeHooks.ResetForTests();
        MissionTickProfilerHooks.ResetForTests();
        MissionAttributionInstaller.ResetForTests();
    }

    private MissionTickProfilerBehavior Create() =>
        new MissionTickProfilerBehavior(_settings, Substitute.For<IGraphicsOptionsAdapter>(), _logger);

    private string[] Lines(string level) => _logger.ReceivedCalls()
        .Where(c => c.GetMethodInfo().Name == level)
        .Select(c => (string)c.GetArguments()[0]!)
        .ToArray();

    [TestMethod]
    public void OnCreated_ProbeOnProfilerOff_MeasuresInProbeModeWithoutBehaviorTiming()
    {
        HitchProbeInstaller.ProfilerOffAtGameStart = true;
        _settings.HitchProbeEnabled.Returns(true);

        Create().OnCreated();

        Assert.IsTrue(MissionTickProfilerHooks.Profiler!.Measuring);
        Assert.IsFalse(MissionTickProfilerHooks.Profiler.BehaviorTiming);
        Assert.AreEqual(0, Lines(nameof(IModLogger.LogWarning)).Length);
    }

    [TestMethod]
    public void OnCreated_ProfilerSwitchedOnAfterGameStart_ProbeMeasuring_SaysTheProbeStillMeasures()
    {
        HitchProbeInstaller.ProfilerOffAtGameStart = true;
        _settings.HitchProbeEnabled.Returns(true);
        _settings.TickProfilerEnabled.Returns(true);

        Create().OnCreated();

        Assert.IsTrue(MissionTickProfilerHooks.Profiler!.Measuring);
        Assert.AreEqual(HitchProbeLines.ProfilerNotTimingLine(restartNeeded: true, probeMeasuring: true),
            Lines(nameof(IModLogger.LogWarning)).Single());
    }

    [TestMethod]
    public void OnCreated_ProfilerInstalledThenSwitchedOff_ProbeMeasuring_SaysTheProbeStillMeasures()
    {
        MissionTickProfilerHooks.Installed = true;
        _settings.HitchProbeEnabled.Returns(true);

        Create().OnCreated();

        Assert.IsTrue(MissionTickProfilerHooks.Profiler!.Measuring);
        CollectionAssert.Contains(Lines(nameof(IModLogger.LogInfo)),
            HitchProbeLines.BuildProfilerOffLine(MissionNumber(), probeMeasuring: true));
    }

    private int MissionNumber() => int.Parse(Lines(nameof(IModLogger.LogInfo))
        .Single(l => l.StartsWith("[TickProfiler] mission ")).Split(' ')[2].TrimEnd(':'), System.Globalization.CultureInfo.InvariantCulture);
}
