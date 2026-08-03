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

    // ---- ExitWindowOpenedUtcTicks (feeds ExitStallSampler, #331 round 2) ----
    // The ticks latch must mirror the bool exactly: nonzero only while the window is open,
    // cleared by every closer (incl. the unconditional-close paths).

    [TestMethod]
    public void LogExitBegin_SetsExitWindowOpenedTicks()
    {
        _sut.LogExitBegin("m", "s", 1, 1);
        Assert.AreNotEqual(0L, _sut.ExitWindowOpenedUtcTicks);
    }

    [TestMethod]
    public void LogFirstMapTick_ClearsExitWindowOpenedTicks()
    {
        _sut.LogExitBegin("m", "s", 1, 1);
        _sut.LogFirstMapTick(false);
        Assert.AreEqual(0L, _sut.ExitWindowOpenedUtcTicks);
    }

    [TestMethod]
    public void ResetLifecycle_WhenDisabledMidWindow_ClearsExitWindowOpenedTicks()
    {
        _sut.LogExitBegin("m", "s", 1, 1);
        _settings.IsEnabled.Returns(false);
        _sut.ResetLifecycle();
        Assert.AreEqual(0L, _sut.ExitWindowOpenedUtcTicks);
    }

    [TestMethod]
    public void LogMissionInitialize_ClearsExitWindowOpenedTicks()
    {
        _sut.LogExitBegin("m", "s", 1, 1);
        _sut.LogMissionInitialize("next_scene");
        Assert.AreEqual(0L, _sut.ExitWindowOpenedUtcTicks);
    }

    // --- OpenNew -> Initialize blind-window stamps (2026-07-16 Nan Angren player CTD) ---------

    [TestMethod]
    public void LogMissionOpenNewDone_Enabled_EmitsPhaseWithCreatedFlag()
    {
        _sut.LogMissionOpenNewDone("Battle", missionCreated: true);
        _logger.Received().LogInfo(Arg.Is<string>(s =>
            s.Contains("phase=MissionOpenNewDone") && s.Contains("mission='Battle'") && s.Contains("created=True")));
    }

    [TestMethod]
    public void LogLoadMissionBegin_Enabled_EmitsPhase()
    {
        _sut.LogLoadMissionBegin();
        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("phase=LoadMissionBegin")));
    }

    [TestMethod]
    public void LogResourceClearOldBegin_Enabled_EmitsPhase()
    {
        _sut.LogResourceClearOldBegin();
        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("phase=ResourceClearOldBegin")));
    }

    [TestMethod]
    public void LogResourceClearOldDone_Enabled_EmitsPhase()
    {
        _sut.LogResourceClearOldDone();
        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("phase=ResourceClearOldDone")));
    }

    [TestMethod]
    public void LogMissionAfterStartBegin_Enabled_EmitsPhase()
    {
        _sut.LogMissionAfterStartBegin();
        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("phase=MissionAfterStartBegin")));
    }

    [TestMethod]
    public void LogMissionAfterStartDone_Enabled_EmitsPhase()
    {
        _sut.LogMissionAfterStartDone();
        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("phase=MissionAfterStartDone")));
    }

    [TestMethod]
    public void LogTaomBehaviorsBegin_Enabled_EmitsPhase()
    {
        _sut.LogTaomBehaviorsBegin();
        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("phase=TaomBehaviorsBegin")));
    }

    [TestMethod]
    public void LogTaomBehaviorsDone_Enabled_EmitsPhaseWithCount()
    {
        _sut.LogTaomBehaviorsDone(11);
        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("phase=TaomBehaviorsDone") && s.Contains("count=11")));
    }

    [TestMethod]
    public void LogTaomBehaviorAdded_Enabled_EmitsPhaseWithBehaviorName()
    {
        _sut.LogTaomBehaviorAdded("SpiderMissionBehavior");
        _logger.Received().LogInfo(Arg.Is<string>(s =>
            s.Contains("phase=TaomBehaviorAdded") && s.Contains("behavior='SpiderMissionBehavior'")));
    }

    // The whole point of this stamp is surviving a hard crash. LogDebug is the async path and gets
    // dropped when the process dies, so a well-meaning "it's just noise, make it DEBUG" refactor
    // would silently re-open the blind window with every other test still green.
    [TestMethod]
    public void LogTaomBehaviorAdded_UsesDurableLogInfo_NotLogDebug()
    {
        _sut.LogTaomBehaviorAdded("WargMissionBehavior");
        _logger.Received().LogInfo(Arg.Any<string>());
        _logger.DidNotReceive().LogDebug(Arg.Is<string>(s => s.Contains("WargMissionBehavior")));
    }

    [TestMethod]
    public void NewPhaseMethods_WhenDisabled_WriteNothing()
    {
        _settings.IsEnabled.Returns(false);

        _sut.LogMissionOpenNewDone("Battle", true);
        _sut.LogLoadMissionBegin();
        _sut.LogResourceClearOldBegin();
        _sut.LogResourceClearOldDone();
        _sut.LogMissionAfterStartBegin();
        _sut.LogMissionAfterStartDone();
        _sut.LogTaomBehaviorsBegin();
        _sut.LogTaomBehaviorAdded("X");
        _sut.LogTaomBehaviorsDone(0);

        _logger.DidNotReceive().LogInfo(Arg.Any<string>());
    }

    // These are pure probes. The exit-window latch has its own opener/closer contract (#331), and
    // a stamp that silently closed it would strand the exit diagnostics.
    [TestMethod]
    public void NewPhaseMethods_DoNotAlterExitWindowState()
    {
        _sut.LogExitBegin("m", "s", 1, 1);

        _sut.LogMissionOpenNewDone("Battle", true);
        _sut.LogLoadMissionBegin();
        _sut.LogResourceClearOldBegin();
        _sut.LogResourceClearOldDone();
        _sut.LogMissionAfterStartBegin();
        _sut.LogTaomBehaviorsBegin();
        _sut.LogTaomBehaviorAdded("X");
        _sut.LogTaomBehaviorsDone(1);
        _sut.LogMissionAfterStartDone();

        Assert.IsTrue(_sut.IsExitWindowActive);
    }

    [TestMethod]
    public void NewPhaseMethods_WhenLoggerThrows_DoNotPropagate()
    {
        _logger.When(l => l.LogInfo(Arg.Any<string>())).Do(_ => throw new InvalidOperationException("boom"));

        _sut.LogMissionOpenNewDone("Battle", true);
        _sut.LogLoadMissionBegin();
        _sut.LogResourceClearOldBegin();
        _sut.LogResourceClearOldDone();
        _sut.LogMissionAfterStartBegin();
        _sut.LogMissionAfterStartDone();
        _sut.LogTaomBehaviorsBegin();
        _sut.LogTaomBehaviorAdded("X");
        _sut.LogTaomBehaviorsDone(1);
    }

    [TestMethod]
    public void CurrentStatusLine_AfterNewPhase_ReflectsLatestPhase()
    {
        _sut.LogResourceClearOldBegin();
        StringAssert.Contains(_sut.CurrentStatusLine, "phase=ResourceClearOldBegin");
    }

    // ---- Per-loadout dedupe of the equipment dump (2026-08-03) --------------------------------
    // An arena audience of 429 agents drew from 9 character kits and produced 1,146 slot lines
    // encoding 11 distinct rows. The dump is per-LOADOUT; only the phase stamps are per-agent.

    // Distinct item id per snapshot so each loadout renders a distinct body.
    private void FormatterEchoesItemId() =>
        _formatter.Format(Arg.Any<EquipmentSnapshot>()).Returns(ci =>
        {
            var s = (EquipmentSnapshot)ci[0];
            var id = s.Slots.Count > 0 ? s.Slots[0].ItemId : "<none>";
            return new List<string> { $"slot=Weapon1 id={id}" };
        });

    private static EquipmentSnapshot Snap(int agentIndex, string itemId, string? race = null) =>
        new EquipmentSnapshot(agentIndex, $"Agent{agentIndex}", "mordor_orc", "mordor",
            new[] { new EquipmentSlotSnapshot("Weapon1", itemId, null, null, null, null, "Shield") },
            race);

    [TestMethod]
    public void LogAgentEquipBegin_IdenticalLoadout_WritesBodyOnlyOnce()
    {
        FormatterEchoesItemId();

        _sut.LogAgentEquipBegin(Snap(0, "shield_a"));
        _sut.LogAgentEquipBegin(Snap(1, "shield_a"));

        _logger.Received(1).LogDebug(Arg.Is<string>(s => s.Contains("id=shield_a")));
    }

    [TestMethod]
    public void LogAgentEquipBegin_IdenticalLoadout_BothBeginLinesCarrySameLoadoutId()
    {
        FormatterEchoesItemId();

        _sut.LogAgentEquipBegin(Snap(0, "shield_a"));
        _sut.LogAgentEquipBegin(Snap(1, "shield_a"));

        _logger.Received(2).LogInfo(Arg.Is<string>(s =>
            s.Contains("phase=AgentEquipBegin") && s.Contains("loadout=#1")));
    }

    // Every agent still gets its own durable stamp — the dedupe compresses the body, never
    // the per-agent evidence that the agent existed.
    [TestMethod]
    public void LogAgentEquipBegin_IdenticalLoadout_StillEmitsOneBeginLinePerAgent()
    {
        FormatterEchoesItemId();

        _sut.LogAgentEquipBegin(Snap(0, "shield_a"));
        _sut.LogAgentEquipBegin(Snap(1, "shield_a"));

        _logger.Received(1).LogInfo(Arg.Is<string>(s => s.Contains("agent#0")));
        _logger.Received(1).LogInfo(Arg.Is<string>(s => s.Contains("agent#1")));
    }

    // The suspect-#1 case: TaomTournamentModel.GetParticipantArmor rewrites MatchEquipment
    // mid-load. A divergent loadout must surface as a NEW id + a fresh dump, never be swallowed.
    [TestMethod]
    public void LogAgentEquipBegin_DifferentLoadout_WritesNewBodyUnderNewId()
    {
        FormatterEchoesItemId();

        _sut.LogAgentEquipBegin(Snap(0, "shield_a"));
        _sut.LogAgentEquipBegin(Snap(1, "shield_b"));

        _logger.Received(1).LogDebug(Arg.Is<string>(s => s.Contains("id=shield_a")));
        _logger.Received(1).LogDebug(Arg.Is<string>(s => s.Contains("id=shield_b")));
        _logger.Received(1).LogInfo(Arg.Is<string>(s => s.Contains("agent#1") && s.Contains("loadout=#2")));
    }

    // Skeleton identity is part of the key: identical gear on a different race is a different
    // thing for the engine to assemble, and that mismatch is the shape that access-violates.
    [TestMethod]
    public void LogAgentEquipBegin_SameSlotsDifferentRace_WritesNewBody()
    {
        FormatterEchoesItemId();

        _sut.LogAgentEquipBegin(Snap(0, "shield_a", race: "human"));
        _sut.LogAgentEquipBegin(Snap(1, "shield_a", race: "dwarf"));

        _logger.Received(2).LogDebug(Arg.Is<string>(s => s.Contains("id=shield_a")));
    }

    [TestMethod]
    public void LogMissionInitialize_ClearsLoadoutCache_NextLoadReDumps()
    {
        FormatterEchoesItemId();
        _sut.LogAgentEquipBegin(Snap(0, "shield_a"));

        _sut.LogMissionInitialize("next_scene");
        _sut.LogAgentEquipBegin(Snap(0, "shield_a"));

        _logger.Received(2).LogDebug(Arg.Is<string>(s => s.Contains("id=shield_a")));
    }

    [TestMethod]
    public void ResetLifecycle_ClearsLoadoutCache_NextLoadReDumps()
    {
        FormatterEchoesItemId();
        _sut.LogAgentEquipBegin(Snap(0, "shield_a"));

        _sut.ResetLifecycle();
        _sut.LogAgentEquipBegin(Snap(0, "shield_a"));

        _logger.Received(2).LogDebug(Arg.Is<string>(s => s.Contains("id=shield_a")));
    }

    // The map sits on the hot agent-spawn path. Past the cap it must degrade to always-dump
    // rather than grow without bound.
    [TestMethod]
    public void LogAgentEquipBegin_BeyondLoadoutCap_StillWritesBody()
    {
        FormatterEchoesItemId();
        for (var i = 0; i < BattleLoadDiagnosticsService.MaxTrackedLoadouts; i++)
            _sut.LogAgentEquipBegin(Snap(i, $"item_{i}"));
        _logger.ClearReceivedCalls();

        _sut.LogAgentEquipBegin(Snap(9001, "overflow_item"));
        _sut.LogAgentEquipBegin(Snap(9002, "overflow_item"));

        _logger.Received(2).LogDebug(Arg.Is<string>(s => s.Contains("id=overflow_item")));
    }
}
