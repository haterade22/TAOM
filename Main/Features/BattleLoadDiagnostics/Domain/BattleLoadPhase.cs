namespace TAOM.Features.BattleLoadDiagnostics.Domain;

// Stable phase names for the [BattleLoad] lifecycle log. The LAST phase written before
// a hang localizes the freeze. These names are part of the log contract — users upload
// logs and we grep for them — so do NOT rename casually.
public enum BattleLoadPhase
{
    EncounterStart,
    MissionOpenNew,
    BattleSceneSelected,
    MissionInitialize,
    AgentEquipBegin,
    AgentEquipOk,
    BattlePlayable,
    StallWatchdog,

    // Mission-exit lifecycle (issue #331). Order mirrors the engine's teardown flow:
    // Mission.EndMission -> EndMissionInternal -> MissionState.OnFinalize -> MapState.OnActivate.
    // ResourceClear (MemoryCleanupGC + native ClearResources) runs NESTED INSIDE
    // MissionState.OnFinalize, so StateFinalizeBegin -> ResourceClearBegin/Done ->
    // StateFinalizeDone is the actual runtime order — not a typo.
    ExitBegin,
    ExitTeardownBegin,
    ExitTeardownDone,
    ExitStateFinalizeBegin,
    ExitResourceClearBegin,
    ExitResourceClearDone,
    ExitStateFinalizeDone,
    MapResumed,
    FirstMapTick,
}
