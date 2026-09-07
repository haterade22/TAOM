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

    // MissionInitialize -> MissionAfterStartBegin was a MEASURED ~11.9 s blind window (2026-08-07).
    // MissionState.cs:221-350 says it is exactly three things, and these three markers name them:
    //   bucket 1  MissionInitialize        -> MissionInitializeDone      native MBAPI.IMBMission.InitializeMission
    //                                                                    (the whole body of Mission.Initialize)
    //   bucket 2  MissionInitializeDone    -> FinishMissionLoadingBegin  N x TickLoading polling the native
    //                                                                    Mission.IsLoadingFinished
    //   bucket 3a MissionAfterStartBegin is reached from inside FinishMissionLoading: Scene.SetOwnerThread,
    //             two warm-up Mission.Tick(0.001f) calls, then Handler.OnMissionAfterStarting
    //   bucket 3b MissionAfterStartBegin   -> MissionAfterStartDone      Mission.AfterStart (the AgentEquip burst)
    //   bucket 3c MissionAfterStartDone    -> FinishMissionLoadingDone   OnMissionLoadingFinished +
    //                                                                    Scene.ResumeLoadingRenderings
    // FinishMissionLoadingBegin carries `polls=N` (TickLoading frames since Initialize returned) and
    // `waitMs=N`. polls=1 with a large waitMs means the block was INSIDE one frame — a blocking native
    // spin, not async waiting. polls=0 means the TickLoading binding FAILED, not "there was no wait".
    MissionInitializeDone,
    FinishMissionLoadingBegin,
    FinishMissionLoadingDone,

    // Bucket 2's own heartbeat. TickLoading is a COUNTER that never logs (720 frames per 12 s
    // load), so a process that DIES inside the async wait and one that HANGS there produce the
    // identical log tail: seq=7 MissionInitializeDone and nothing after it. Two player CTDs on
    // battle_terrain_biome_094 (2026-09-06) were bounded only to "somewhere in a 30-second
    // window" for that reason, while three healthy loads in the same session cleared the same
    // bucket in 383-933 ms.
    //
    // Emitted only once the wait passes SceneLoadWaitWarnAfterMs, then at
    // SceneLoadWaitEmitIntervalMs — so a healthy load stays completely silent and a pathological
    // one leaves a trail whose LAST line bounds the fault to one interval. Carries the same
    // `polls=` / `waitMs=` pair FinishMissionLoadingBegin does so triage_battle_load.py parses
    // both with one regex.
    //
    // THE TOKENS ARE THE SAME; THE READING IS NOT. FinishMissionLoadingBegin is written after
    // the wait ENDED, so polls=1 there means the thread blocked INSIDE frame 1 — the #352
    // WaitForMeshesToBeLoaded shape. This line is emitted FROM INSIDE a TickLoading frame, so
    // polls=1 here means frame 1 had not even arrived when the threshold elapsed: the block is
    // BEFORE the loop, not inside it. Opposite locations. triage_battle_load.py branches on its
    // `wait_incomplete` flag to keep them apart; a reader must too.
    //
    // COVERAGE BOUNDARY, and it is the counter-intuitive half. This marker is driven BY the loop
    // it measures: NoteLoadingPoll runs from the TickLoading prefix. So a main thread wedged
    // inside one native frame emits nothing further, and the #352 shape produces SILENCE here
    // rather than a trail. That is not a blind spot once it is written down, it is the second
    // reading: on a load known to have run for many seconds, ABSENCE of this phase is itself the
    // blocking-spin signal, and presence rules that shape out. Silence on a SHORT load just means
    // it ended before SceneLoadWaitWarnAfterMs.
    WaitingForSceneLoad,

    // FinishMissionLoadingDone -> BattlePlayable was itself a blind window, and a player bundle
    // (b18f3441, 2026-09-04) spent 290 s inside it with nothing logged. MissionState.OnTick reaches
    // TickMission only through `Handler.RenderIsReady()`, which is
    // MissionScreen.MissionStartedRendering() -> the native SceneView.ReadyToRender(); that stays
    // false while the scene's shaders compile. FinishMissionLoading ends with
    // Scene.ResumeLoadingRenderings(), which is what starts the compile flood. So a cold shader
    // cache holds the mission one frame short of playable for as long as the queue takes to drain,
    // and the phase log could not tell that apart from a wedge.
    //
    // Emitted at 1 Hz (it is called once per FRAME of the wait) carrying `waitedMs=` and the live
    // `shaders=` count. shaders= counting DOWN is a working load; a frozen count is the wedge.
    WaitingForRender,

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
