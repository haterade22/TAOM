using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.BattleLoadDiagnostics;
using TAOM.Features.MissionPerf;
using TAOM.Features.MissionPerf.Hooks;

namespace TAOM.Tests.Features.MissionPerf;

/// <summary>
/// The tick profiler's per-mission side, driven through the engine's own lifecycle entry points with no
/// mission attached (<c>MissionBehavior.Mission</c> is then null, so the scene and agent reads fall back
/// and name their fault). Every mission states whether it measures and why not; a measured mission ends
/// with a <c>[TickSummary]</c>. The install is a startup fact: a mission measures only while every hook the
/// profiler needs is still in place, which these tests change after the install through a fake Harmony world
/// (the required hooks minus the removed ones). Constructs a <c>MissionLogic</c>, so it needs the game assemblies.
/// </summary>
[TestCategory("RequiresGame")]
[TestClass]
public class MissionTickProfilerBehaviorTests
{
    private IModLogger _logger = null!;
    private IBattleLoadDiagnosticsSettingsProvider _settings = null!;
    private IGraphicsOptionsAdapter _options = null!;
    private readonly HashSet<string> _removed = new HashSet<string>();

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _settings = Substitute.For<IBattleLoadDiagnosticsSettingsProvider>();
        _settings.TickProfilerTopN.Returns(8);
        _settings.HitchThresholdMs.Returns(250d);
        _options = Substitute.For<IGraphicsOptionsAdapter>();
        ResetStatics();
    }

    [TestCleanup]
    public void Cleanup() => ResetStatics();

    private static void ResetStatics()
    {
        MissionTickProfilerHooks.Profiler = null;
        MissionTickProfilerHooks.Logger = null;
        MissionTickProfilerHooks.WaitTickCompletionCall = null;
        MissionTickProfilerHooks.Installed = false;
        MissionTickProfilerHooks.OnTickSites = 0;
        MissionTickProfilerHooks.OnPreTickSites = 0;
        MissionTickProfilerHealth.PatchInfo = Harmony.GetPatchInfo;
    }

    private void Install()
    {
        MissionTickProfilerHooks.Profiler = new MissionTickProfiler(Stopwatch.Frequency);
        MissionTickProfilerHooks.Logger = _logger;
        MissionTickProfilerHooks.WaitTickCompletionCall = _ => { };
        MissionTickProfilerHooks.OnTickSites = 2;
        MissionTickProfilerHooks.OnPreTickSites = 2;
        MissionTickProfilerHooks.Installed = true;
        MissionTickProfilerHealth.PatchInfo = WorldPatches;
    }

    // The patches Harmony would report in a process where the install worked: every required hook on the
    // target, minus the ones a test removed afterwards.
    private Patches? WorldPatches(MethodBase target)
    {
        var present = MissionTickProfilerHealth.RequiredHooks()
            .Where(h => h.Target == target && !_removed.Contains(h.Name)).ToList();
        Patch[] Of(HarmonyPatchType kind) => present.Where(h => h.Kind == kind)
            .Select((h, i) => new Patch(h.PatchMethod!, i, "com.taom.mod", 0, Array.Empty<string>(), Array.Empty<string>(), false))
            .ToArray();
        return new Patches(Of(HarmonyPatchType.Prefix), Of(HarmonyPatchType.Postfix), Of(HarmonyPatchType.Transpiler),
            Of(HarmonyPatchType.Finalizer), Array.Empty<Patch>(), Array.Empty<Patch>());
    }

    private MissionTickProfilerBehavior Create() => new MissionTickProfilerBehavior(_settings, _options, _logger);

    private string[] Lines(string level) => _logger.ReceivedCalls()
        .Where(c => c.GetMethodInfo().Name == level)
        .Select(c => (string)c.GetArguments()[0]!)
        .ToArray();

    [TestMethod]
    public void OnCreated_InstalledAndOn_WritesTheMissionHeaderAndStartsMeasuring()
    {
        Install();
        _settings.TickProfilerEnabled.Returns(true);

        Create().OnCreated();

        Assert.IsTrue(MissionTickProfilerHooks.Profiler!.Measuring);
        var header = Lines(nameof(IModLogger.LogInfo)).Single();
        StringAssert.Matches(header, new Regex(@"^\[TickProfiler\] mission \d+: measuring, top 8 behaviours per line, hitch threshold 250 ms, first 100 hitch frames written in full, sites Mission\.OnTick 2/2 Mission\.OnPreTick 2/2$"));
    }

    [TestMethod]
    public void OnCreated_OnButOffAtGameStart_WarnsThatARestartIsNeeded()
    {
        _settings.TickProfilerEnabled.Returns(true);

        Create().OnCreated();

        Assert.AreEqual(TickProfileLines.RestartNeededLine, Lines(nameof(IModLogger.LogWarning)).Single());
    }

    [TestMethod]
    public void OnCreated_OnButTheInstallFailed_WarnsNotInstalled()
    {
        Install();
        MissionTickProfilerHooks.Installed = false;
        _settings.TickProfilerEnabled.Returns(true);

        Create().OnCreated();

        Assert.AreEqual(TickProfileLines.NotInstalledLine, Lines(nameof(IModLogger.LogWarning)).Single());
        Assert.IsFalse(MissionTickProfilerHooks.Profiler!.Measuring);
    }

    [TestMethod]
    public void OnCreated_InstalledButSwitchedOff_SaysWhyItIsNotMeasuring()
    {
        Install();
        _settings.TickProfilerEnabled.Returns(false);

        Create().OnCreated();

        Assert.IsFalse(MissionTickProfilerHooks.Profiler!.Measuring);
        StringAssert.Matches(Lines(nameof(IModLogger.LogInfo)).Single(),
            new Regex(@"^\[TickProfiler\] mission \d+: not measuring, 'Enable Tick Profiler' is off in MCM; "));
    }

    [TestMethod]
    public void OnCreated_OffAndNeverInstalled_WritesNoStatusLine()
    {
        _settings.TickProfilerEnabled.Returns(false);

        Create().OnCreated();

        Assert.AreEqual(0, _logger.ReceivedCalls().Count());
    }

    [TestMethod]
    public void OnMissionTick_WritesOnePerfContextPerMission_WithFallbacksNamed()
    {
        _settings.TickProfilerEnabled.Returns(false);
        var behavior = Create();
        behavior.OnCreated();

        behavior.OnMissionTick(0.016f);
        behavior.OnMissionTick(0.016f);

        var info = Lines(nameof(IModLogger.LogInfo));
        var context = info.Single(l => l.StartsWith("[PerfContext] "));
        StringAssert.Contains(context, " scene=unknown agents=-1 ");
        StringAssert.Contains(context, " tickProfiler=off ");
        Assert.AreEqual(1, info.Count(l => l.StartsWith("[TickProfiler] context read of scene failed")));
    }

    [TestMethod]
    public void OnEndMission_MeasuredMission_WritesTheSummaryAndStopsMeasuring()
    {
        Install();
        _settings.TickProfilerEnabled.Returns(true);
        var behavior = Create();
        behavior.OnCreated();
        MissionTickProfilerHooks.OnFrameBoundary();
        MissionTickProfilerHooks.OnFrameBoundary();

        behavior.OnEndMissionInternal();

        Assert.AreEqual(1, Lines(nameof(IModLogger.LogInfo)).Count(l => l.StartsWith("[TickSummary] frames=1 ")));
        Assert.IsFalse(MissionTickProfilerHooks.Profiler!.Measuring);
    }

    // The install is a startup fact. After it, another mod's transpiler can take an anchor (Patch97's rewrite
    // then finds none and says so: OnTickSites 0) or PatchShield can strip TAOM's patches on a method it
    // rescued; either way the mission must not measure with a hook that is no longer there.
    [TestMethod]
    public void OnCreated_InstalledThenAZeroSiteRewrite_DoesNotMeasureAndWarnsOnce()
    {
        Install();
        MissionTickProfilerHooks.OnTickSites = 0;
        _settings.TickProfilerEnabled.Returns(true);

        Create().OnCreated();

        Assert.IsFalse(MissionTickProfilerHooks.Profiler!.Measuring);
        var warning = Lines(nameof(IModLogger.LogWarning)).Single();
        StringAssert.Matches(warning, new Regex(@"^\[TickProfiler\] mission \d+: not measuring, required hooks missing: Mission\.OnTick call sites 0/2; "));
        Assert.AreEqual(0, Lines(nameof(IModLogger.LogInfo)).Length, "No measuring header, and no off line.");
    }

    [TestMethod]
    public void RequiredHooks_AreTheFourTheProfilerNeeds()
        => CollectionAssert.AreEqual(
            new[]
            {
                "Mission.OnTick transpiler (Patch97)",
                "Mission.OnPreTick frame-boundary prefix (Patch98)",
                "Mission.TickAgentsAndTeamsImp prefix (Patch91)",
                "Mission.TickAgentsAndTeamsImp finalizer (Patch91)",
            },
            MissionTickProfilerHealth.RequiredHooks().Select(h => h.Name).ToArray());

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    public void OnCreated_InstalledThenARequiredHookIsRemoved_DoesNotMeasureAndNamesOnlyThatHook(int index)
    {
        Install();
        var hooks = MissionTickProfilerHealth.RequiredHooks();
        _removed.Add(hooks[index].Name);
        _settings.TickProfilerEnabled.Returns(true);

        Create().OnCreated();

        Assert.IsFalse(MissionTickProfilerHooks.Profiler!.Measuring);
        var warning = Lines(nameof(IModLogger.LogWarning)).Single();
        StringAssert.Contains(warning, hooks[index].Name);
        for (var other = 0; other < hooks.Count; other++)
        {
            if (other != index)
                Assert.IsFalse(warning.Contains(hooks[other].Name), $"{hooks[other].Name} is in place but the line names it: {warning}");
        }
        Assert.AreEqual(0, Lines(nameof(IModLogger.LogInfo)).Length);
    }

    [TestMethod]
    public void OnCreated_SeveralHooksGone_WritesOneAggregatedWarningNamingEveryOne()
    {
        Install();
        MissionTickProfilerHooks.OnTickSites = 1;
        foreach (var hook in MissionTickProfilerHealth.RequiredHooks())
            _removed.Add(hook.Name);
        _settings.TickProfilerEnabled.Returns(true);

        Create().OnCreated();

        var warning = Lines(nameof(IModLogger.LogWarning)).Single();
        StringAssert.Contains(warning, "Mission.OnTick call sites 1/2");
        foreach (var hook in MissionTickProfilerHealth.RequiredHooks())
            StringAssert.Contains(warning, hook.Name);
    }

    [TestMethod]
    public void OnCreated_TheHookIsBackAtTheNextMission_MeasuresAgain()
    {
        Install();
        _removed.Add(MissionTickProfilerHealth.RequiredHooks()[1].Name);
        _settings.TickProfilerEnabled.Returns(true);
        Create().OnCreated();
        Assert.IsFalse(MissionTickProfilerHooks.Profiler!.Measuring);

        _removed.Clear();
        Create().OnCreated();

        Assert.IsTrue(MissionTickProfilerHooks.Profiler.Measuring, "Health is read at every mission start, not once at the install.");
    }

    [TestMethod]
    public void OnCreated_SwitchedOffAndAHookGone_SaysOnlyThatItIsSwitchedOff()
    {
        Install();
        _removed.Add(MissionTickProfilerHealth.RequiredHooks()[0].Name);
        _settings.TickProfilerEnabled.Returns(false);

        Create().OnCreated();

        Assert.AreEqual(0, Lines(nameof(IModLogger.LogWarning)).Length);
        StringAssert.Contains(Lines(nameof(IModLogger.LogInfo)).Single(), "not measuring, 'Enable Tick Profiler' is off in MCM");
    }

    [TestMethod]
    public void OnCreated_PatchInfoCannotBeRead_DoesNotMeasureAndSaysWhichHooksItCouldNotCheck()
    {
        Install();
        MissionTickProfilerHealth.PatchInfo = _ => throw new InvalidOperationException("harmony broke");
        _settings.TickProfilerEnabled.Returns(true);

        Create().OnCreated();

        Assert.IsFalse(MissionTickProfilerHooks.Profiler!.Measuring);
        StringAssert.Contains(Lines(nameof(IModLogger.LogWarning)).Single(), "(patch info unreadable: InvalidOperationException: harmony broke)");
    }

    // The behaviour stamps its start at OnCreated. Moving the stamp back makes its tick clock read a known elapsed
    // time without sleeping, so a line's t is pinned to what the behaviour computed and not to a value near 0.
    private static void BackdateMissionStart(MissionTickProfilerBehavior behavior, int seconds)
    {
        var field = typeof(MissionTickProfilerBehavior).GetField("_missionStart", BindingFlags.Instance | BindingFlags.NonPublic)!;
        field.SetValue(behavior, (long)field.GetValue(behavior)! - seconds * Stopwatch.Frequency);
    }

    // A rewrite can also happen while a mission runs (another mod patches Mission.OnTick late, which reruns every
    // transpiler on it). The site count is a field read, so every tick of a measuring mission checks it.
    [TestMethod]
    public void OnMissionTick_ASiteCountDropsMidMission_StopsMeasuringAtOnceAndWarnsOnce()
    {
        Install();
        _settings.TickProfilerEnabled.Returns(true);
        var behavior = Create();
        behavior.OnCreated();
        BackdateMissionStart(behavior, 70);
        MissionTickProfilerHooks.OnFrameBoundary();
        MissionTickProfilerHooks.OnFrameBoundary();
        behavior.OnMissionTick(0.016f);
        MissionTickProfilerHooks.OnTickSites = 0;

        behavior.OnMissionTick(0.016f);
        behavior.OnMissionTick(0.016f);
        behavior.OnEndMissionInternal();

        Assert.IsFalse(MissionTickProfilerHooks.Profiler!.Measuring);
        var warning = Lines(nameof(IModLogger.LogWarning)).Single();
        StringAssert.Matches(warning, new Regex(@"^\[TickProfiler\] mission \d+: measuring stopped at t=\+70s, required hooks missing: Mission\.OnTick call sites 0/2; "));
        Assert.AreEqual(1, Lines(nameof(IModLogger.LogInfo)).Count(l => l.StartsWith("[TickSummary] frames=1 ")),
            "The frames measured before the stop still get their summary.");
    }

    [TestMethod]
    public void OnMissionTick_EverySiteStillThere_KeepsMeasuringAndWarnsOfNothing()
    {
        Install();
        _settings.TickProfilerEnabled.Returns(true);
        var behavior = Create();
        behavior.OnCreated();

        behavior.OnMissionTick(0.016f);
        behavior.OnMissionTick(0.016f);

        Assert.IsTrue(MissionTickProfilerHooks.Profiler!.Measuring);
        Assert.AreEqual(0, Lines(nameof(IModLogger.LogWarning)).Length);
    }

    // Reading patch info deserializes it on every call and resolves each patch's method by scanning the loaded
    // assemblies, so a strip is read at a mission start only. This pins that limit: it is documented, not hidden.
    [TestMethod]
    public void OnMissionTick_AHookIsStrippedMidMission_KeepsMeasuringUntilTheNextMissionStartNamesIt()
    {
        Install();
        _settings.TickProfilerEnabled.Returns(true);
        var behavior = Create();
        behavior.OnCreated();
        behavior.OnMissionTick(0.016f);
        _removed.Add(MissionTickProfilerHealth.RequiredHooks()[1].Name);

        behavior.OnMissionTick(0.016f);
        Assert.IsTrue(MissionTickProfilerHooks.Profiler!.Measuring, "Not read mid-mission.");
        Create().OnCreated();

        Assert.IsFalse(MissionTickProfilerHooks.Profiler.Measuring);
        StringAssert.Contains(Lines(nameof(IModLogger.LogWarning)).Single(), "Mission.OnPreTick frame-boundary prefix (Patch98)");
    }
}
