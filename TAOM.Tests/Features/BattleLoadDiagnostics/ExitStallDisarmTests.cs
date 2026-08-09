using System.Collections.Generic;
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
}
