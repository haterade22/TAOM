namespace TAOM.Features.SaveLoadDiagnostics.Domain;

// Stable phase names for the [SaveLoad] lifecycle log. The engine swallows the real
// exception behind the generic "A problem occured while trying to load the saved game."
// dialog (LoadContext.Load catches and prints only ex.Message), so these stamps are the
// only attribution a user's log carries. Part of the log contract — users upload logs
// and we grep for them — so do NOT rename casually.
public enum SaveLoadPhase
{
    // Load side, in engine call order:
    // SandBoxSaveHelper.TryLoadSave -> MBSaveLoad.LoadSaveGameData -> SaveManager.Load
    // -> LoadContext.Load (object/container graph fill, TWParallel) -> behavior SyncData.
    LoadRequested,
    ModuleCheck,
    LoadDataOk,
    LoadFailed,
    LoadFault,
    GraphFault,
    UnknownSaveId,
    BehaviorSyncFault,
    AllBehaviorDataLoaded,
    // The deferred [LoadInitializationCallback] phase — on the campaign path it runs from
    // Game.LoadSaveGame AFTER LoadDataOk, and SaveShield (TAOM.Dependencies) swallows its
    // exceptions into a silent half-load; the Finalizer stamp is the only failure signal.
    ObjectsInitialized,

    // Campaign-launch side, stamped from the MBSubModuleBase overrides in SubModule.cs and carrying
    // the process memory tokens (gc/heapMB/privMB/wsMB). Engine order for a SAVED campaign
    // (installed v1.4.8 Campaign.cs:1420-1452): GameManager.OnGameLoaded, then
    // LoadBehaviorData (AllBehaviorDataLoaded), RegisterEvents, ..., then OnGameInitializationFinished.
    // So GameLoaded brackets the behavior-data load from ABOVE and only GameInitializationFinished
    // bisects the 95 s / +2.7 GB stretch the 2026-09-12 live run measured between
    // AllBehaviorDataLoaded and the map screen (#509 saw the same window stall for 10 minutes).
    // A NEW campaign never fires OnGameLoaded (it takes the OnAfterCampaignStart branch), so it
    // stamps GameInitializationFinished only, with no LoadRequested before it.
    GameLoaded,
    GameInitializationFinished,

    // Save side. SaveWriteFault fires on the AsyncFileSaveDriver background thread at the
    // original throw site inside FileDriver.Save (the #292 GameData.Write class), so a bad
    // WRITE is caught when it happens instead of being discovered at the next load.
    SaveBegin,
    SaveWriteFault,
    SaveStatusFault,
    SaveCompleted,
}
