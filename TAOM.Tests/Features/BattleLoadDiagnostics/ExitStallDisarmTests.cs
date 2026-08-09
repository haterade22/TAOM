using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.BattleLoadDiagnostics;

namespace TAOM.Tests.Features.BattleLoadDiagnostics;

/// <summary>
/// #425 — the stall sampler must disarm at MapResumed while the phase-logging window stays
/// open to FirstMapTick. Field evidence for the split: MapResumed landed within ~1 s of
/// ExitBegin on all 16 observed exits across two sessions, while the sampler kept firing
/// [ERROR] Thread.Suspend captures up to 123 s later into ordinary menus, and — worst case —
/// into the next campaign's XML load after a quit-to-load (no map tick ever comes, so
/// FirstMapTick alone can never close that path; SubModule.OnGameEnd now does).
/// </summary>
[TestClass]
public class ExitStallDisarmTests
{
    private IBattleLoadDiagnosticsSettingsProvider _settings;
    private BattleLoadDiagnosticsService _sut;

    [TestInitialize]
    public void Setup()
    {
        _settings = Substitute.For<IBattleLoadDiagnosticsSettingsProvider>();
        var formatter = Substitute.For<IEquipmentDumpFormatter>();
        formatter.Format(Arg.Any<TAOM.Features.BattleLoadDiagnostics.Domain.EquipmentSnapshot>())
            .Returns(new List<string>());
        _settings.IsEnabled.Returns(true);
        _sut = new BattleLoadDiagnosticsService(Substitute.For<IModLogger>(), _settings, formatter);
    }

    [TestMethod]
    public void MapResumed_DisarmsSampler_WhileLoggingWindowStaysOpen()
    {
        _sut.LogExitBegin("m", "s", 1, 1);
        Assert.AreNotEqual(0L, _sut.ExitWindowOpenedUtcTicks, "ExitBegin must arm the sampler.");

        _sut.LogMapResumed(isSaving: false);

        Assert.AreEqual(0L, _sut.ExitWindowOpenedUtcTicks,
            "MapResumed must disarm the sampler — time after it is player time, not teardown.");
        Assert.IsTrue(_sut.IsExitWindowActive,
            "The phase-logging window must survive MapResumed so FirstMapTick still logs.");

        _sut.LogFirstMapTick(isSaving: false);
        Assert.IsFalse(_sut.IsExitWindowActive, "FirstMapTick closes the logging window.");
    }

    [TestMethod]
    public void MapResumed_Disarm_IsUnconditional_WhenToggledOffMidWindow()
    {
        _sut.LogExitBegin("m", "s", 1, 1);
        Assert.AreNotEqual(0L, _sut.ExitWindowOpenedUtcTicks);

        // A toggle-off between ExitBegin and MapResumed must not latch the sampler armed —
        // the same rule CloseExitWindow already documents for the logging window.
        _settings.IsEnabled.Returns(false);
        _sut.LogMapResumed(isSaving: false);

        Assert.AreEqual(0L, _sut.ExitWindowOpenedUtcTicks,
            "Disarm must run even while the master toggle is off, or a mid-window toggle-off " +
            "leaves the sampler suspending the main thread indefinitely.");
    }

    [TestMethod]
    public void ResetLifecycle_ClosesAnArmedWindow_TheQuitToLoadPath()
    {
        _sut.LogExitBegin("m", "s", 1, 1);
        Assert.AreNotEqual(0L, _sut.ExitWindowOpenedUtcTicks);

        // SubModule.OnGameEnd routes here when the Game that opened the window dies
        // (quit-to-load / quit-to-menu). MapResumed and FirstMapTick never fire on that path.
        _sut.ResetLifecycle();

        Assert.AreEqual(0L, _sut.ExitWindowOpenedUtcTicks, "ResetLifecycle must disarm the sampler.");
        Assert.IsFalse(_sut.IsExitWindowActive, "ResetLifecycle must close the logging window.");
    }

    [TestMethod]
    public void PreMapResumedStall_KeepsTheSamplerArmed()
    {
        // The preservation half of #425 (PR #429 review, LOW): the ~107s tournament-exit
        // stall (#331) hangs BEFORE MapResumed, and it must still get all three samples.
        // A later edit that disarms earlier than MapResumed silently deletes the feature;
        // this pins the semantics the disarm boundary was chosen to preserve.
        _sut.LogExitBegin("m", "s", 1, 1);
        _sut.LogExitTeardownBegin();
        _sut.LogExitStateFinalizeBegin();
        _sut.LogExitResourceClearBegin(forceClearGpuResources: false);

        Assert.AreNotEqual(0L, _sut.ExitWindowOpenedUtcTicks,
            "No exit phase before MapResumed may disarm the sampler — a real teardown stall " +
            "must remain sampleable through the entire teardown sequence.");
    }

    [TestMethod]
    public void DisarmWiring_IsPresentInBothClosers()
    {
        // PR #429 review, LOW: the service tests stay green if the wiring is deleted. Pin the
        // two closers at the source level: SubModule.OnGameEnd (quit-to-menu; Game.Destroy's
        // only menu-path caller is MBInitialScreenBase.OnInitialize) and the TryLoadSave
        // prefix (quit-to-load; precedes any teardown of the old Game by construction).
        var subModule = File.ReadAllText(FromRepoRoot("Main/SubModule.cs"));
        StringAssert.Contains(subModule, "SandBoxSaveHelper_TryLoadSave_DisarmPatch.Initialize",
            "The quit-to-load disarm patch is no longer wired from SubModule — the sampler can " +
            "again ride into the next campaign's LoadXML (#425 HIGH).");
        StringAssert.Contains(subModule, "public override void OnGameEnd",
            "SubModule.OnGameEnd is gone — the quit-to-menu path no longer closes the exit window.");

        var patch = File.ReadAllText(FromRepoRoot(
            "Main/Features/BattleLoadDiagnostics/Hooks/SandBoxSaveHelper_TryLoadSave_DisarmPatch.cs"));
        StringAssert.Contains(patch, "ResetLifecycle",
            "The TryLoadSave prefix no longer resets the lifecycle — the disarm is a no-op.");
    }

    private static string FromRepoRoot(string relPath)
    {
        var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (dir != null)
        {
            // File.Exists too, not just Directory.Exists: in a git WORKTREE `.git` is a FILE
            // holding a `gitdir:` pointer, not a directory (see f1bc6b39).
            var gitPath = Path.Combine(dir.FullName, ".git");
            if (Directory.Exists(gitPath) || File.Exists(gitPath))
                return Path.Combine(dir.FullName, relPath.Replace('/', Path.DirectorySeparatorChar));
            dir = dir.Parent;
        }

        throw new InvalidOperationException("repo root (.git) not found from " + AppDomain.CurrentDomain.BaseDirectory);
    }
}
