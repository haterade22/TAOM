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

    // Phase 3 — battle-terrain scene chosen for the world-map tile.
    void LogBattleSceneSelected(int mapIndex, string sceneId, bool isNaval);

    // Phase 4 — Mission.Initialize entered.
    void LogMissionInitialize(string sceneName);

    // Phase 5 — per-agent equipment spawn. Begin is written (and flushed) BEFORE the
    // engine equips the agent; Ok only after it returns. A begin with no matching Ok = the
    // freeze, and the dumped loadout names the suspect item.
    void LogAgentEquipBegin(EquipmentSnapshot snapshot);
    void LogAgentEquipOk(int agentIndex, string agentName);

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
