using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
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
    public void LogExitBegin_WhenEnabled_EmitsMissionSceneAgentsAndMemStats()
    {
        _sut.LogExitBegin("TournamentFight", "arena_sturgia_a", 24, 234);
        _logger.Received().LogInfo(Arg.Is<string>(s =>
            s.Contains("ExitBegin") && s.Contains("mission='TournamentFight'") &&
            s.Contains("scene='arena_sturgia_a'") && s.Contains("agents=24/234") &&
            s.Contains("gc=") && s.Contains("heapMB=") &&
            s.Contains("privMB=") && s.Contains("wsMB=")));
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
    public void LogMapResumed_InWindow_IncludesMemStatsAndSavingFlag()
    {
        _sut.LogExitBegin("m", "s", 1, 1);
        _sut.LogMapResumed(true);
        _logger.Received().LogInfo(Arg.Is<string>(s =>
            s.Contains("MapResumed") && s.Contains("isSaving=True") &&
            s.Contains("gc=") && s.Contains("heapMB=") &&
            s.Contains("privMB=") && s.Contains("wsMB=")));
    }

    // ---- [MemSample] phase-line tokens (#386): the entry-side memory anchors ----------------
    // EncounterStart / MissionInitialize / BattlePlayable carry MemStats so a crash log shows
    // the process footprint at the load's start, scene-init, and playable points.

    [TestMethod]
    public void LogEncounterStart_WhenEnabled_IncludesMemStats()
    {
        _sut.LogEncounterStart(10);
        _logger.Received().LogInfo(Arg.Is<string>(s =>
            s.Contains("EncounterStart") && s.Contains("gc=") && s.Contains("heapMB=") &&
            s.Contains("privMB=") && s.Contains("wsMB=")));
    }

    [TestMethod]
    public void LogMissionInitialize_WhenEnabled_IncludesMemStats()
    {
        _sut.LogMissionInitialize("scene_x");
        _logger.Received().LogInfo(Arg.Is<string>(s =>
            s.Contains("MissionInitialize") && s.Contains("scene='scene_x'") &&
            s.Contains("gc=") && s.Contains("heapMB=") &&
            s.Contains("privMB=") && s.Contains("wsMB=")));
    }

    [TestMethod]
    public void LogBattlePlayable_WhenEnabled_IncludesMemStats()
    {
        _sut.LogBattlePlayable("scene_x", 42);
        _logger.Received().LogInfo(Arg.Is<string>(s =>
            s.Contains("BattlePlayable") && s.Contains("agents=42") &&
            s.Contains("gc=") && s.Contains("heapMB=") &&
            s.Contains("privMB=") && s.Contains("wsMB=")));
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

    // ---- Three-bucket split of the MissionInitialize -> MissionAfterStartBegin gap (2026-08-07) ----
    // A measured ~11.9 s sat unattributed between Mission.Initialize's prefix and Mission.AfterStart.
    // MissionState.cs:221-350 says it is exactly three things: the native InitializeMission call,
    // the IsLoadingFinished poll loop, and FinishMissionLoading's pre-AfterStart work.

    private IEnumerable<string> InfoLines() =>
        _logger.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(IModLogger.LogInfo))
            .Select(c => (string)c.GetArguments()[0]);

    private string LineFor(string phase) =>
        InfoLines().FirstOrDefault(s => s.Contains($"phase={phase} "))
        ?? InfoLines().FirstOrDefault(s => s.Contains($"phase={phase}"));

    private static readonly Regex ElapsedRe = new Regex(@"t=\+(\d+)ms");

    private static long ElapsedOf(string line)
    {
        Assert.IsNotNull(line, "expected a [BattleLoad] line to parse t=+ from");
        var m = ElapsedRe.Match(line);
        Assert.IsTrue(m.Success, $"line carried no t=+Nms token: {line}");
        return long.Parse(m.Groups[1].Value);
    }

    // THE headline guard for the counter design: TickLoading runs every frame of the load, and a
    // 12 s wait at 60 fps is 720 frames. A per-frame log line is why this is a counter, not a marker.
    [TestMethod]
    public void NoteLoadingPoll_CalledOneThousandTimes_WritesNoLogLine()
    {
        for (var i = 0; i < 1000; i++) _sut.NoteLoadingPoll();

        _logger.DidNotReceive().LogInfo(Arg.Any<string>());
        _logger.DidNotReceive().LogDebug(Arg.Any<string>());
    }

    // Latch rules 2/3: the counter is STATE, not I/O. A mid-load toggle-off must not corrupt the
    // count the next FinishMissionLoadingBegin reports.
    [TestMethod]
    public void NoteLoadingPoll_WhenDisabled_StillCounts()
    {
        _settings.IsEnabled.Returns(false);
        for (var i = 0; i < 5; i++) _sut.NoteLoadingPoll();
        _sut.LogMissionInitializeDone("scene_a");
        for (var i = 0; i < 3; i++) _sut.NoteLoadingPoll();

        _settings.IsEnabled.Returns(true);
        _sut.LogFinishMissionLoadingBegin();

        StringAssert.Contains(LineFor("FinishMissionLoadingBegin"), "polls=3");
    }

    [TestMethod]
    public void LogMissionInitializeDone_ResetsPollCounter()
    {
        for (var i = 0; i < 7; i++) _sut.NoteLoadingPoll();
        _sut.LogMissionInitializeDone("scene_a");
        _sut.NoteLoadingPoll();

        _sut.LogFinishMissionLoadingBegin();

        StringAssert.Contains(LineFor("FinishMissionLoadingBegin"), "polls=1");
    }

    [TestMethod]
    public void ResetLifecycle_ResetsPollCounter()
    {
        for (var i = 0; i < 7; i++) _sut.NoteLoadingPoll();
        _sut.ResetLifecycle();

        _sut.LogFinishMissionLoadingBegin();

        StringAssert.Contains(LineFor("FinishMissionLoadingBegin"), "polls=0");
    }

    // Covers a postfix that never ran (a throw inside Mission.Initialize): the prefix still
    // has to zero the counter, or the next load reports the previous one's frames.
    [TestMethod]
    public void LogMissionInitialize_ResetsPollCounter()
    {
        for (var i = 0; i < 7; i++) _sut.NoteLoadingPoll();
        _sut.LogMissionInitialize("scene_a");

        _sut.LogFinishMissionLoadingBegin();

        StringAssert.Contains(LineFor("FinishMissionLoadingBegin"), "polls=0");
    }

    // polls=0 is a real, meaningful reading — it means the TickLoading binding failed, NOT
    // "there was no wait". The feature doc's read-a-hang-log table says so; this pins it.
    [TestMethod]
    public void LogFinishMissionLoadingBegin_NoPollsRecorded_EmitsPollsZero()
    {
        _sut.LogMissionInitializeDone("scene_a");
        _sut.LogFinishMissionLoadingBegin();

        StringAssert.Contains(LineFor("FinishMissionLoadingBegin"), "polls=0");
    }

    [TestMethod]
    public void LogMissionInitializeDone_WhenDisabled_WritesNothing()
    {
        _settings.IsEnabled.Returns(false);
        _sut.LogMissionInitializeDone("scene_a");
        _logger.DidNotReceive().LogInfo(Arg.Any<string>());
    }

    [TestMethod]
    public void LogMissionInitializeDone_IncludesSceneAndPhase()
    {
        _sut.LogMissionInitializeDone("battle_terrain_a");

        _logger.Received().LogInfo(Arg.Is<string>(s =>
            s.Contains("phase=MissionInitializeDone") && s.Contains("scene='battle_terrain_a'")));
    }

    [TestMethod]
    public void LogFinishMissionLoadingBegin_WhenDisabled_WritesNothing()
    {
        _settings.IsEnabled.Returns(false);
        _sut.LogFinishMissionLoadingBegin();
        _logger.DidNotReceive().LogInfo(Arg.Any<string>());
    }

    [TestMethod]
    public void LogFinishMissionLoadingBegin_IncludesPollsAndWaitMs()
    {
        _sut.LogMissionInitializeDone("scene_a");
        for (var i = 0; i < 3; i++) _sut.NoteLoadingPoll();

        _sut.LogFinishMissionLoadingBegin();

        var line = LineFor("FinishMissionLoadingBegin");
        StringAssert.Contains(line, "polls=3");
        StringAssert.Contains(line, "waitMs=");
    }

    // Never a fabricated 0 — same contract as MemStats()'s absent-process-tokens rule. Without a
    // MissionInitializeDone stamp there is no wait ORIGIN, so there is no wait to report.
    [TestMethod]
    public void LogFinishMissionLoadingBegin_WithoutInitializeDone_OmitsWaitMs()
    {
        _sut.NoteLoadingPoll();
        _sut.LogFinishMissionLoadingBegin();

        var line = LineFor("FinishMissionLoadingBegin");
        StringAssert.Contains(line, "polls=1");
        Assert.IsFalse(line.Contains("waitMs="), $"waitMs must be omitted, not fabricated: {line}");
    }

    [TestMethod]
    public void LogFinishMissionLoadingDone_WhenDisabled_WritesNothing()
    {
        _settings.IsEnabled.Returns(false);
        _sut.LogFinishMissionLoadingDone();
        _logger.DidNotReceive().LogInfo(Arg.Any<string>());
    }

    [TestMethod]
    public void LogFinishMissionLoadingDone_EmitsPhase()
    {
        _sut.LogFinishMissionLoadingDone();
        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("phase=FinishMissionLoadingDone")));
    }

    // The whole point of stamping memory on these three: privMB across the stall is the only
    // reading that can say whether the load stall and the commit growth are ONE problem.
    // gc= is asserted rather than privMB= because the process read is environment-dependent.
    [TestMethod]
    public void NewLoadPhases_CarryMemStatsTokens()
    {
        _sut.LogMissionInitializeDone("scene_a");
        _sut.LogFinishMissionLoadingBegin();
        _sut.LogFinishMissionLoadingDone();

        StringAssert.Contains(LineFor("MissionInitializeDone"), "gc=");
        StringAssert.Contains(LineFor("FinishMissionLoadingBegin"), "gc=");
        StringAssert.Contains(LineFor("FinishMissionLoadingDone"), "gc=");
    }

    [TestMethod]
    public void NewLoadPhaseMethods_SwallowThrowingLogger()
    {
        _logger.When(l => l.LogInfo(Arg.Any<string>())).Do(_ => throw new InvalidOperationException("boom"));

        _sut.LogMissionInitializeDone("scene_a");
        _sut.NoteLoadingPoll();
        _sut.LogFinishMissionLoadingBegin();
        _sut.LogFinishMissionLoadingDone();
    }

    // These are pure probes: the exit-window latch (#331) has its own opener/closer contract and
    // a load stamp that silently closed it would strand the exit diagnostics.
    [TestMethod]
    public void NewLoadPhaseMethods_DoNotAlterExitWindowState()
    {
        _sut.LogExitBegin("m", "s", 1, 1);

        _sut.LogMissionInitializeDone("scene_a");
        _sut.NoteLoadingPoll();
        _sut.LogFinishMissionLoadingBegin();
        _sut.LogFinishMissionLoadingDone();

        Assert.IsTrue(_sut.IsExitWindowActive);
    }

    // ---- Twin literal pin, half A. Half B is the fixture in tools/tests/test_triage_battle_load.py.
    // The Python triage reader parses these tokens; the two halves must stay byte-identical.

    [TestMethod]
    public void FormatFinishWaitDetail_WithWait_ProducesPinnedLiteral()
        => Assert.AreEqual("polls=87 waitMs=1449",
            BattleLoadDiagnosticsService.FormatFinishWaitDetail(87, 1449L));

    [TestMethod]
    public void FormatFinishWaitDetail_WithoutWait_ProducesPinnedLiteral()
        => Assert.AreEqual("polls=87",
            BattleLoadDiagnosticsService.FormatFinishWaitDetail(87, null));

    // ---- The stopwatch blocker (2026-08-07) ---------------------------------------------------
    // _stopwatch was started ONLY from LogEncounterStart / ResetLifecycle, both reachable only
    // from PlayerEncounter_Start_Patch — which is campaign-only. In a CUSTOM BATTLE the clock
    // never ran, so every [BattleLoad] line read t=+0ms and the new markers would have been
    // worthless in the exact station the measurement matrix uses.

    [TestMethod]
    public void LogMissionOpenNew_WithStoppedClock_StartsIt()
    {
        _sut.LogMissionOpenNew("Battle", "scene_a", null);
        Thread.Sleep(30);
        _sut.LogLoadMissionBegin();

        Assert.IsTrue(ElapsedOf(LineFor("LoadMissionBegin")) >= 5,
            "the clock must be running after MissionOpenNew — otherwise every custom-battle line reads t=+0ms");
    }

    [TestMethod]
    public void LogMissionOpenNew_WithRunningClock_DoesNotRestartIt()
    {
        _sut.LogEncounterStart(10);
        Thread.Sleep(30);
        _sut.LogMissionOpenNew("Battle", "scene_a", null);

        Assert.IsTrue(ElapsedOf(LineFor("MissionOpenNew")) >= 5,
            "an already-running clock must not be restarted — the campaign path's deltas depend on it");
    }

    [TestMethod]
    public void LogMissionInitialize_WithStoppedClock_StartsIt()
    {
        _sut.LogMissionInitialize("scene_a");
        Thread.Sleep(30);
        _sut.LogMissionInitializeDone("scene_a");

        Assert.IsTrue(ElapsedOf(LineFor("MissionInitializeDone")) >= 5,
            "the clock must be running after MissionInitialize");
    }

    // ---- The render-wait window (bundle b18f3441, 2026-09-04) ----
    // FinishMissionLoadingDone -> BattlePlayable held 290 s with NOTHING logged inside it. The
    // engine's own log for that window is 818 compile_shader lines: MissionState.OnTick withholds
    // the first Mission.Tick behind MissionScreen.RenderIsReady() -> SceneView.ReadyToRender(),
    // which stays false while shaders compile. These pin the marker that closes that hole.

    // The headline design guard, same reasoning as NoteLoadingPoll's: this is called once per FRAME
    // of the wait. At 60 fps the b18f3441 window would have been ~17,000 lines unthrottled.
    [TestMethod]
    public void NoteWaitingForRender_CalledOneThousandTimesInsideOneSecond_WritesAtMostOneLine()
    {
        for (var i = 0; i < 1000; i++) _sut.NoteWaitingForRender(412 - (i % 7));

        var lines = InfoLines().Count(s => s.Contains("phase=WaitingForRender"));
        Assert.IsTrue(lines <= 1, $"expected at most one throttled line, got {lines}");
    }

    [TestMethod]
    public void NoteWaitingForRender_FirstCall_EmitsImmediately()
    {
        _sut.NoteWaitingForRender(412);

        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("phase=WaitingForRender")));
    }

    [TestMethod]
    public void NoteWaitingForRender_WhenDisabled_WritesNothing()
    {
        _settings.IsEnabled.Returns(false);
        _sut.NoteWaitingForRender(412);
        _logger.DidNotReceive().LogInfo(Arg.Any<string>());
    }

    [TestMethod]
    public void NoteWaitingForRender_IncludesShaderCountAndWaitedMs()
    {
        _sut.LogFinishMissionLoadingDone();
        Thread.Sleep(30);
        _sut.NoteWaitingForRender(412);

        var line = LineFor("WaitingForRender");
        StringAssert.Contains(line, "shaders=412");
        StringAssert.Contains(line, "waitedMs=");
    }

    // Absent, never zero — the same contract FormatFinishWaitDetail follows for waitMs. Without a
    // FinishMissionLoadingDone stamp there is no render-wait ORIGIN to measure from.
    [TestMethod]
    public void NoteWaitingForRender_WithoutFinishMissionLoadingDone_OmitsWaitedMs()
    {
        _sut.NoteWaitingForRender(412);

        var line = LineFor("WaitingForRender");
        Assert.IsFalse(line.Contains("waitedMs="), $"waitedMs must be omitted, not fabricated: {line}");
    }

    // -1 is the hook's "the native read threw" sentinel. A user log must not show shaders=-1 or a
    // fabricated shaders=0, either of which reads as a real engine value.
    [TestMethod]
    public void NoteWaitingForRender_UnreadableShaderCount_OmitsShadersToken()
    {
        _sut.NoteWaitingForRender(-1);

        var line = LineFor("WaitingForRender");
        Assert.IsFalse(line.Contains("shaders="), $"shaders must be omitted when unreadable: {line}");
    }

    // The origin is STATE, not I/O: a mid-load toggle-off must not leave the next load measuring
    // its render wait from the previous mission's stamp (latch rule 2).
    [TestMethod]
    public void LogFinishMissionLoadingDone_WhenDisabled_StillArmsTheRenderWaitOrigin()
    {
        _settings.IsEnabled.Returns(false);
        _sut.LogFinishMissionLoadingDone();
        _settings.IsEnabled.Returns(true);

        _sut.NoteWaitingForRender(412);

        StringAssert.Contains(LineFor("WaitingForRender"), "waitedMs=");
    }

    [TestMethod]
    public void LogMissionInitialize_ResetsRenderWaitThrottle()
    {
        _sut.NoteWaitingForRender(412);
        _sut.LogMissionInitialize("scene_a");
        _sut.NoteWaitingForRender(411);

        Assert.AreEqual(2, InfoLines().Count(s => s.Contains("phase=WaitingForRender")),
            "a new load must emit its first render-wait line immediately, not wait out the previous throttle");
    }

    [TestMethod]
    public void ShouldEmitRenderWait_FirstEverCall_ReturnsTrue()
        => Assert.IsTrue(BattleLoadDiagnosticsService.ShouldEmitRenderWait(0L, -1L, 1000L));

    [TestMethod]
    public void ShouldEmitRenderWait_InsideInterval_ReturnsFalse()
        => Assert.IsFalse(BattleLoadDiagnosticsService.ShouldEmitRenderWait(1500L, 1000L, 1000L));

    [TestMethod]
    public void ShouldEmitRenderWait_ExactlyAtInterval_ReturnsTrue()
        => Assert.IsTrue(BattleLoadDiagnosticsService.ShouldEmitRenderWait(2000L, 1000L, 1000L));

    // The stopwatch restarts between loads, so "now" can legitimately go BACKWARDS relative to a
    // stale stamp. That must emit, not latch the marker off for the rest of the load.
    [TestMethod]
    public void ShouldEmitRenderWait_ClockWentBackwards_ReturnsTrue()
        => Assert.IsTrue(BattleLoadDiagnosticsService.ShouldEmitRenderWait(10L, 9000L, 1000L));

    [TestMethod]
    public void FormatRenderWaitDetail_BothKnown_EmitsBothTokens()
        => Assert.AreEqual("waitedMs=290000 shaders=412",
            BattleLoadDiagnosticsService.FormatRenderWaitDetail(290000L, 412));

    [TestMethod]
    public void FormatRenderWaitDetail_NoOrigin_OmitsWaitedMs()
        => Assert.AreEqual("shaders=412",
            BattleLoadDiagnosticsService.FormatRenderWaitDetail(null, 412));

    [TestMethod]
    public void FormatRenderWaitDetail_UnreadableCount_OmitsShaders()
        => Assert.AreEqual("waitedMs=290000",
            BattleLoadDiagnosticsService.FormatRenderWaitDetail(290000L, -1));

    [TestMethod]
    public void FormatRenderWaitDetail_NeitherKnown_EmitsEmpty()
        => Assert.AreEqual(string.Empty,
            BattleLoadDiagnosticsService.FormatRenderWaitDetail(null, -1));
}
