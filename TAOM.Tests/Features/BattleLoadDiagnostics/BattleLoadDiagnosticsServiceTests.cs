using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.BattleLoadDiagnostics;
using TAOM.Features.BattleLoadDiagnostics.Domain;

namespace TAOM.Tests.Features.BattleLoadDiagnostics;

[TestClass]
public class BattleLoadDiagnosticsServiceTests
{
    private IModLogger _logger;
    private IBattleLoadDiagnosticsSettingsProvider _settings;
    private IEquipmentDumpFormatter _formatter;
    private BattleLoadDiagnosticsService _sut;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _settings = Substitute.For<IBattleLoadDiagnosticsSettingsProvider>();
        _formatter = Substitute.For<IEquipmentDumpFormatter>();
        _formatter.Format(Arg.Any<EquipmentSnapshot>()).Returns(new List<string>());
        _settings.IsEnabled.Returns(true);
        _sut = new BattleLoadDiagnosticsService(_logger, _settings, _formatter);
    }

    private static EquipmentSnapshot Snap() =>
        new EquipmentSnapshot(7, "Orc", "mordor_orc", "mordor",
            new[] { new EquipmentSlotSnapshot("Weapon1", "shield", null, null, null, null, "Shield") });

    [TestMethod]
    public void IsEnabled_ReflectsSettings()
    {
        _settings.IsEnabled.Returns(false);
        Assert.IsFalse(_sut.IsEnabled);
    }

    [TestMethod]
    public void LogEncounterStart_WhenDisabled_WritesNothing()
    {
        _settings.IsEnabled.Returns(false);
        _sut.LogEncounterStart(10);
        _logger.DidNotReceive().LogInfo(Arg.Any<string>());
    }

    [TestMethod]
    public void LogMissionOpenNew_IncludesSceneNameAndSummary()
    {
        _sut.LogMissionOpenNew("Battle", "battle_terrain_a", "side=Attacker");
        _logger.Received().LogInfo(Arg.Is<string>(s =>
            s.Contains("battle_terrain_a") && s.Contains("side=Attacker") && s.Contains("MissionOpenNew")));
    }

    [TestMethod]
    public void LogBattleSceneSelected_IncludesMapIndexAndSceneId()
    {
        _sut.LogBattleSceneSelected(42, "battle_terrain_b", false);
        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("mapIndex=42") && s.Contains("battle_terrain_b")));
    }

    [TestMethod]
    public void LogAgentEquipBegin_WhenDisabled_WritesNothing()
    {
        _settings.IsEnabled.Returns(false);
        _sut.LogAgentEquipBegin(Snap());
        _logger.DidNotReceive().LogInfo(Arg.Any<string>());
        _formatter.DidNotReceive().Format(Arg.Any<EquipmentSnapshot>());
    }

    [TestMethod]
    public void LogAgentEquipBegin_DelegatesBodyToFormatter()
    {
        _formatter.Format(Arg.Any<EquipmentSnapshot>()).Returns(new List<string> { "slot=Weapon1 ..." });
        var snap = Snap();

        _sut.LogAgentEquipBegin(snap);

        _formatter.Received(1).Format(snap);
        _logger.Received().LogDebug(Arg.Is<string>(s => s.Contains("slot=Weapon1")));
    }

    [TestMethod]
    public void LogAgentEquipBegin_WritesBeginLineBeforeBody()
    {
        _formatter.Format(Arg.Any<EquipmentSnapshot>()).Returns(new List<string> { "BODYLINE" });

        _sut.LogAgentEquipBegin(Snap());

        Received.InOrder(() =>
        {
            _logger.LogInfo(Arg.Is<string>(s => s.Contains("AgentEquipBegin")));
            _logger.LogDebug(Arg.Is<string>(s => s.Contains("BODYLINE")));
        });
    }

    [TestMethod]
    public void CurrentStatusLine_UpdatesAfterPhaseMarker()
    {
        _sut.LogMissionInitialize("scene_x");
        StringAssert.Contains(_sut.CurrentStatusLine, "MissionInitialize");
    }

    [TestMethod]
    public void Emit_IncludesSeqAndElapsedAndTagTokens()
    {
        _sut.LogEncounterStart(5);
        _logger.Received().LogInfo(Arg.Is<string>(s =>
            s.Contains("seq=") && s.Contains("t=+") && s.Contains("[BattleLoad]")));
    }

    [TestMethod]
    public void AllPhaseMethods_WhenLoggerThrows_DoNotPropagate()
    {
        _logger.When(l => l.LogInfo(Arg.Any<string>())).Do(_ => throw new InvalidOperationException("boom"));
        _logger.When(l => l.LogDebug(Arg.Any<string>())).Do(_ => throw new InvalidOperationException("boom"));
        _logger.When(l => l.LogWarning(Arg.Any<string>())).Do(_ => throw new InvalidOperationException("boom"));
        _logger.When(l => l.LogError(Arg.Any<string>())).Do(_ => throw new InvalidOperationException("boom"));

        // None of these may propagate — a diagnostic feature must never crash the game.
        _sut.ResetLifecycle();
        _sut.LogEncounterStart(1);
        _sut.LogMissionOpenNew("m", "s", "x");
        _sut.LogBattleSceneSelected(1, "s", false);
        _sut.LogMissionInitialize("s");
        _sut.LogAgentEquipBegin(Snap());
        _sut.LogAgentEquipOk(1, "a");
        _sut.LogBattlePlayable("s", 5);
        _sut.LogExitBegin("TournamentFight", "arena_x", 20, 230);
        _sut.LogExitTeardownBegin();
        _sut.LogExitTeardownDone();
        _sut.LogExitStateFinalizeBegin();
        _sut.LogExitResourceClearBegin(true);
        _sut.LogExitResourceClearDone();
        _sut.LogExitStateFinalizeDone();
        _sut.LogMapResumed(false);
        _sut.LogFirstMapTick(false);
    }

    // ---- Mission-exit phase lifecycle (issue #331 — tournament-exit hang localization) ----

    [TestMethod]
    public void LogExitBegin_WhenEnabled_EmitsMissionSceneAgentsAndGcStats()
    {
        _sut.LogExitBegin("TournamentFight", "arena_sturgia_a", 24, 234);
        _logger.Received().LogInfo(Arg.Is<string>(s =>
            s.Contains("ExitBegin") && s.Contains("mission='TournamentFight'") &&
            s.Contains("scene='arena_sturgia_a'") && s.Contains("agents=24/234") &&
            s.Contains("gc=") && s.Contains("heapMB=")));
    }

    [TestMethod]
    public void LogExitBegin_WhenDisabled_WritesNothingAndWindowStaysClosed()
    {
        _settings.IsEnabled.Returns(false);
        _sut.LogExitBegin("m", "s", 1, 1);
        _logger.DidNotReceive().LogInfo(Arg.Any<string>());
        Assert.IsFalse(_sut.IsExitWindowActive);
    }

    [TestMethod]
    public void LogExitBegin_OpensExitWindow()
    {
        _sut.LogExitBegin("m", "s", 1, 1);
        Assert.IsTrue(_sut.IsExitWindowActive);
    }

    [TestMethod]
    public void LogExitBegin_RestartsSequenceCounter()
    {
        _sut.LogEncounterStart(3);
        _sut.LogMissionInitialize("s");

        _sut.LogExitBegin("m", "s", 1, 1);

        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("seq=1") && s.Contains("ExitBegin")));
    }

    [TestMethod]
    public void ExitPhases_BeforeExitBegin_WriteNothing()
    {
        _sut.LogExitTeardownBegin();
        _sut.LogExitTeardownDone();
        _sut.LogExitStateFinalizeBegin();
        _sut.LogExitResourceClearBegin(false);
        _sut.LogExitResourceClearDone();
        _sut.LogExitStateFinalizeDone();
        _sut.LogMapResumed(false);
        _sut.LogFirstMapTick(false);
        _logger.DidNotReceive().LogInfo(Arg.Any<string>());
    }

    [TestMethod]
    public void ExitPhases_AfterExitBegin_EmitInOrder()
    {
        _sut.LogExitBegin("m", "s", 1, 1);
        _sut.LogExitTeardownBegin();
        _sut.LogExitTeardownDone();

        Received.InOrder(() =>
        {
            _logger.LogInfo(Arg.Is<string>(s => s.Contains("ExitBegin")));
            _logger.LogInfo(Arg.Is<string>(s => s.Contains("ExitTeardownBegin")));
            _logger.LogInfo(Arg.Is<string>(s => s.Contains("ExitTeardownDone")));
        });
    }

    [TestMethod]
    public void LogExitResourceClearBegin_InWindow_IncludesForceFlag()
    {
        _sut.LogExitBegin("m", "s", 1, 1);
        _sut.LogExitResourceClearBegin(true);
        _logger.Received().LogInfo(Arg.Is<string>(s =>
            s.Contains("ExitResourceClearBegin") && s.Contains("forceClearGpu=True")));
    }

    [TestMethod]
    public void LogMapResumed_InWindow_IncludesGcStatsAndSavingFlag()
    {
        _sut.LogExitBegin("m", "s", 1, 1);
        _sut.LogMapResumed(true);
        _logger.Received().LogInfo(Arg.Is<string>(s =>
            s.Contains("MapResumed") && s.Contains("isSaving=True") &&
            s.Contains("gc=") && s.Contains("heapMB=")));
    }

    [TestMethod]
    public void LogFirstMapTick_ClosesExitWindow()
    {
        _sut.LogExitBegin("m", "s", 1, 1);
        _sut.LogFirstMapTick(false);

        Assert.IsFalse(_sut.IsExitWindowActive);
        _logger.ClearReceivedCalls();
        _sut.LogMapResumed(false); // window closed — must be silent
        _logger.DidNotReceive().LogInfo(Arg.Any<string>());
    }

    [TestMethod]
    public void ResetLifecycle_ClosesExitWindow()
    {
        _sut.LogExitBegin("m", "s", 1, 1);
        _sut.ResetLifecycle();
        Assert.IsFalse(_sut.IsExitWindowActive);
    }

    // Window state transitions must be independent of the master toggle — a mid-window
    // toggle-off must never latch the window open (deep-review data-flow finding, 2026-07-06).

    [TestMethod]
    public void ResetLifecycle_WhenDisabledMidWindow_StillClosesExitWindow()
    {
        _sut.LogExitBegin("m", "s", 1, 1);
        _settings.IsEnabled.Returns(false);

        _sut.ResetLifecycle();

        Assert.IsFalse(_sut.IsExitWindowActive);
    }

    [TestMethod]
    public void LogFirstMapTick_WhenDisabledMidWindow_ClosesExitWindowSilently()
    {
        _sut.LogExitBegin("m", "s", 1, 1);
        _settings.IsEnabled.Returns(false);
        _logger.ClearReceivedCalls();

        _sut.LogFirstMapTick(false);

        Assert.IsFalse(_sut.IsExitWindowActive);
        _logger.DidNotReceive().LogInfo(Arg.Any<string>());
    }

    [TestMethod]
    public void LogMissionInitialize_ClosesStaleExitWindow()
    {
        // Chained mission without map activation: exit window from mission A is stale
        // the moment mission B starts initializing.
        _sut.LogExitBegin("m", "s", 1, 1);

        _sut.LogMissionInitialize("next_scene");

        Assert.IsFalse(_sut.IsExitWindowActive);
    }
}
