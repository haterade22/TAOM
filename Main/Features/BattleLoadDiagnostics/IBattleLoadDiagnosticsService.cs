using TAOM.Features.BattleLoadDiagnostics.Domain;

namespace TAOM.Features.BattleLoadDiagnostics;

// Phase-marker capture surface for the battle-load lifecycle. Each thin hook delegates a
// single call here; the service owns the sequence counter + stopwatch and the consistent
// "[BattleLoad] seq=.. t=+..ms phase=.." line format. Every method swallows its own
// exceptions — a diagnostic feature must never cause a crash or a hang.
public interface IBattleLoadDiagnosticsService
{
    bool IsEnabled { get; }

    // The most recent phase marker as a compact string (e.g. "phase=AgentEquipBegin
    // seq=57 agent#57 ..."). Read by the stall watchdog (off-thread) for its STILL-LOADING
    // line so the bundle/marker names the exact phase that froze.
    string CurrentStatusLine { get; }

    // Phase 1 — restart the stopwatch + sequence counter at the start of a new encounter.
    void ResetLifecycle();
    void LogEncounterStart(int mainPartySize);

    // Phase 2 — mission opening; encounterSummary is built by the hook from the sealed
    // PlayerEncounter (or null) so this stays free of TaleWorlds types.
    void LogMissionOpenNew(string missionName, string sceneName, string? encounterSummary);

    // Phase 2b — OpenNew's body returned (mission constructed, state pushed). Its absence after a
    // MissionOpenNew stamp means the crash was inside OpenNew itself.
    void LogMissionOpenNewDone(string missionName, bool missionCreated);

    // Phase 2c — MissionState.LoadMission entered, on the tick AFTER OpenNew. Together with
    // MissionInitialize this brackets OnMissionScreenPreLoad + the native resource clear.
    void LogLoadMissionBegin();

    // Phase 2d — brackets the NATIVE Utilities.ClearOldResourcesAndObjects(). A Begin with no Done
    // puts the fault in native resource teardown rather than in managed load code.
    void LogResourceClearOldBegin();
    void LogResourceClearOldDone();

    // Phase 3 — battle-terrain scene chosen for the world-map tile.
    void LogBattleSceneSelected(int mapIndex, string sceneId, bool isNaval);

    // Phase 4 — Mission.Initialize entered.
    void LogMissionInitialize(string sceneName);

    // Phase 4b — Mission.AfterStart brackets EVERY submodule's OnMissionBehaviorInitialize, so the
    // AfterStartBegin -> TaomBehaviorsBegin gap is other mods. TaomBehaviorAdded names each TAOM
    // behavior as it is added, so a death inside our own 11 identifies the culprit.
    void LogMissionAfterStartBegin();
    void LogMissionAfterStartDone();
    void LogTaomBehaviorsBegin();
    void LogTaomBehaviorAdded(string behaviorName);
    void LogTaomBehaviorsDone(int count);

    // Phase 5 — per-agent equipment spawn. Begin is written (and flushed) BEFORE the
    // engine equips the agent; Ok only after it returns. A begin with no matching Ok = the
    // freeze, and the dumped loadout names the suspect item.
    void LogAgentEquipBegin(EquipmentSnapshot snapshot);
    void LogAgentEquipOk(int agentIndex, string agentName);

    // Phase 5b — Mission.BuildAgent returned. AgentEquipOk only proves the equip call came back;
    // the engine then does ~14 more lines of native work on the same agent (Mission.cs:4035-4049).
    // An Ok with no BuildDone puts the fault in that tail rather than between two agents.
    void LogAgentBuildDone(int agentIndex, string agentName);

    // Phase 6 — first OnMissionTick reached: the battle is playable (load succeeded).
    void LogBattlePlayable(string sceneName, int agentCount);

    // ---- Mission-EXIT lifecycle (issue #331 — localize the tournament-exit hang) ----
    // ExitBegin opens an "exit window", restarts the stopwatch + sequence counter, and
    // stamps GC/heap stats. Every other exit phase is silent unless the window is open,
    // so probes on methods that also fire at load time (ClearUnreferencedResources) or on
    // every map frame (MapState.OnTick) stay inert outside a mission exit. The window
    // closes at FirstMapTick or on the next ResetLifecycle.
    bool IsExitWindowActive { get; }

    /// <summary>UTC ticks when the exit window opened; 0 while closed. Feeds the exit-stall
    /// stack sampler (#331 round 2) the same way BattleLoadLoadingWindow feeds the watchdog.</summary>
    long ExitWindowOpenedUtcTicks { get; }

    void LogExitBegin(string missionName, string sceneName, int agentCount, int allAgentCount);
    void LogExitTeardownBegin();
    void LogExitTeardownDone();
    void LogExitStateFinalizeBegin();
    void LogExitStateFinalizeDone();
    void LogExitResourceClearBegin(bool forceClearGpuResources);
    void LogExitResourceClearDone();
    void LogMapResumed(bool isSaving);
    void LogFirstMapTick(bool isSaving);
}
