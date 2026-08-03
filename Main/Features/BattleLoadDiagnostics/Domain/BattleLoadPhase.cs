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

    // AgentEquipOk -> the next AgentEquipBegin used to be a blind window: our bracket wraps
    // Agent.EquipItemsFromSpawnEquipment, but Mission.BuildAgent keeps working afterwards
    // (InitializeAgentRecord, AgentVisuals.BatchLastLodMeshes, PreloadForRendering,
    // SetActionChannel, InitializeComponents, _activeAgents.Add — Mission.cs:4035-4049), so a
    // death in that native tail looked identical to a death between two agents. AgentBuildDone
    // is a postfix on Mission.BuildAgent: an AgentEquipOk with no matching AgentBuildDone means
    // the fault is inside that tail. (2026-08-02 Dunland tournament CTD.)
    AgentBuildDone,

    // MissionOpenNew -> MissionInitialize used to be one dark window spanning a tick boundary:
    // the OpenNew stamp is a Prefix, so a crash anywhere in OpenNew's body, in LoadMission, or in
    // the native resource clear looked identical (2026-07-16 Nan Angren player CTD). These split
    // it into named segments. Runtime order:
    //   MissionOpenNew -> MissionOpenNewDone -> [tick] -> LoadMissionBegin ->
    //   ResourceClearOldBegin -> ResourceClearOldDone -> MissionInitialize
    // ResourceClearOld* brackets the NATIVE Utilities.ClearOldResourcesAndObjects() — the one
    // native call in the window, and the shape that access-violates.
    MissionOpenNewDone,
    LoadMissionBegin,
    ResourceClearOldBegin,
    ResourceClearOldDone,

    // MissionInitialize -> BattlePlayable. Mission.AfterStart runs OnMissionBehaviorInitialize for
    // EVERY submodule, so AfterStartBegin -> TaomBehaviorsBegin is other mods' work: that gap is
    // what lets a report exonerate TAOM rather than merely implicate it. TaomBehaviorAdded names
    // each of TAOM's own behaviors, so a death inside them says WHICH one.
    MissionAfterStartBegin,
    TaomBehaviorsBegin,
    TaomBehaviorAdded,
    TaomBehaviorsDone,
    MissionAfterStartDone,

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
