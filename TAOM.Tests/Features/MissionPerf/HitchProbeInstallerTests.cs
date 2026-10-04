using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.BattleLoadDiagnostics;
using TAOM.Features.MissionPerf;
using TAOM.Features.MissionPerf.Hooks;

namespace TAOM.Tests.Features.MissionPerf;

/// <summary>
/// The game-start decision between the three modes, driven through the one public entry point
/// <c>MissionTickProfilerInstaller.InstallIfEnabled</c> (SubModule's once-per-process game-init block calls it
/// once): both toggles off install nothing and say so twice; the hitch probe alone applies Patch98; the tick
/// profiler applies Patch97 and then Patch98, which owns the frame boundary. The category apply is a recorder.
/// </summary>
[TestClass]
public class HitchProbeInstallerTests
{
    private IModLogger _logger = null!;
    private IBattleLoadDiagnosticsSettingsProvider _settings = null!;
    private List<string> _applied = null!;

    [TestInitialize]
    public void Setup()
    {
        Reset();
        _logger = Substitute.For<IModLogger>();
        _settings = Substitute.For<IBattleLoadDiagnosticsSettingsProvider>();
        _applied = new List<string>();
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

    private Func<string, bool> Recorder(bool patch97 = true, bool patch98 = true, Action? onPatch97 = null) => category =>
    {
        _applied.Add(category);
        if (category == MissionTickProfilerInstaller.Category)
        {
            if (onPatch97 != null)
                onPatch97();
            else
            {
                MissionTickProfilerHooks.OnTickSites = 2;
                MissionTickProfilerHooks.OnPreTickSites = 2;
            }
            return patch97;
        }
        return patch98;
    };

    private string[] Lines(string level) => _logger.ReceivedCalls()
        .Where(c => c.GetMethodInfo().Name == level)
        .Select(c => (string)c.GetArguments()[0]!)
        .ToArray();

    [TestMethod]
    public void Install_BothTogglesOff_AppliesNothing_AndLogsBothOffLines()
    {
        MissionTickProfilerInstaller.InstallIfEnabled(_settings, _logger, Recorder());

        Assert.AreEqual(0, _applied.Count);
        CollectionAssert.AreEqual(new[] { TickProfileLines.OffLine, HitchProbeLines.ProbeOffLine }, Lines(nameof(IModLogger.LogInfo)));
        Assert.IsFalse(HitchProbeInstaller.ProbeInstalled);
        Assert.IsNull(MissionTickProfilerHooks.Profiler);
        Assert.IsNull(HitchProbeHooks.Sampler);
    }

    [TestMethod]
    public void Install_ProbeOnProfilerOff_AppliesOnlyTheProbeCategory_AndLogsTheInstallLine()
    {
        _settings.HitchProbeEnabled.Returns(true);

        MissionTickProfilerInstaller.InstallIfEnabled(_settings, _logger, Recorder());

        CollectionAssert.AreEqual(new[] { "Patch98_HitchProbe" }, _applied);
        Assert.AreEqual(1, Lines(nameof(IModLogger.LogInfo))
            .Count(l => l.StartsWith("[TickProfiler] probe install: category applied, enabled by hitch probe,")));
        Assert.IsTrue(HitchProbeInstaller.ProbeInstalled);
        Assert.IsFalse(HitchProbeHooks.WaitSwapActive);
        Assert.IsFalse(MissionTickProfilerHooks.Installed);
        Assert.IsNotNull(MissionTickProfilerHooks.Profiler, "The probe creates the profiler it feeds.");
        Assert.AreSame(_logger, MissionTickProfilerHooks.Logger);
        Assert.IsNotNull(HitchProbeHooks.Sampler);
        Assert.IsFalse(HitchProbeHooks.Sampler.Enabled, "The sampler measures its cost at the first measured mission, not at game start.");
    }

    [TestMethod]
    public void Install_ProfilerOn_AppliesPatch97ThenTheProbe()
    {
        _settings.HitchProbeEnabled.Returns(true);
        _settings.TickProfilerEnabled.Returns(true);

        MissionTickProfilerInstaller.InstallIfEnabled(_settings, _logger, Recorder());

        CollectionAssert.AreEqual(new[] { "Patch97_MissionTickProfiler", "Patch98_HitchProbe" }, _applied);
        Assert.AreEqual(1, Lines(nameof(IModLogger.LogInfo))
            .Count(l => l.StartsWith("[TickProfiler] probe install: category applied, enabled by both,")));
        Assert.IsTrue(MissionTickProfilerHooks.Installed);
        Assert.IsTrue(HitchProbeInstaller.ProbeInstalled);
    }

    [TestMethod]
    public void Install_ProbeCategoryFails_LeavesItUninstalled_AndSaysSo()
    {
        _settings.HitchProbeEnabled.Returns(true);
        _settings.TickProfilerEnabled.Returns(true);

        MissionTickProfilerInstaller.InstallIfEnabled(_settings, _logger, Recorder(patch98: false));

        Assert.AreEqual(1, Lines(nameof(IModLogger.LogInfo))
            .Count(l => l.StartsWith("[TickProfiler] probe install: category failed, enabled by both,")));
        Assert.IsFalse(HitchProbeInstaller.ProbeInstalled);
        Assert.IsFalse(MissionTickProfilerHooks.Installed, "Without Patch98 there is no frame boundary, so Patch97 measures nothing.");
    }

    [TestMethod]
    public void Install_ProbeInstallThrows_LeavesNeitherCategoryCountedAsInstalled()
    {
        _settings.HitchProbeEnabled.Returns(true);
        _settings.TickProfilerEnabled.Returns(true);
        var boom = new InvalidOperationException("probe apply broke");
        var recorder = Recorder();

        MissionTickProfilerInstaller.InstallIfEnabled(_settings, _logger,
            category => category == HitchProbeInstaller.Category ? throw boom : recorder(category));

        Assert.IsFalse(HitchProbeInstaller.ProbeInstalled);
        Assert.IsFalse(MissionTickProfilerHooks.Installed,
            "Without Patch98 there is no frame boundary, so no mission may time behaviours (review 041).");
        Assert.AreEqual(HitchProbeLines.BuildHookFault("probe install", boom), Lines(nameof(IModLogger.LogError)).Single());
    }

    [TestMethod]
    public void Install_Patch97WaitSwapLiveButOnTickSitesShort_MarksTheWaitSwapActive()
    {
        _settings.TickProfilerEnabled.Returns(true);

        MissionTickProfilerInstaller.InstallIfEnabled(_settings, _logger, Recorder(onPatch97: () =>
        {
            MissionTickProfilerHooks.OnPreTickSites = 2;
            MissionTickProfilerHooks.OnTickSites = 1;
            MissionTickProfilerHooks.WaitTickCompletionCall = _ => { };
        }));

        Assert.IsFalse(MissionTickProfilerHooks.Installed);
        Assert.IsTrue(HitchProbeHooks.WaitSwapActive, "The swapped wait helper is the one wait recorder.");
        Assert.AreEqual(1, Lines(nameof(IModLogger.LogInfo))
            .Count(l => l.StartsWith("[TickProfiler] probe install: category applied, enabled by tick profiler,")));
    }
}
