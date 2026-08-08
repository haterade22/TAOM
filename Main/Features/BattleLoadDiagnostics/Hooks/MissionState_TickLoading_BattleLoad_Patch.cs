using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.BattleLoadDiagnostics.Hooks;

// Counts MissionState.TickLoading frames so the async load wait can be MEASURED rather than
// inferred. This hook NEVER LOGS — that is its whole design constraint. TickLoading runs once per
// frame while a mission loads, so at 60fps a 12-second wait is ~720 calls; a log line here would
// produce more noise than the blind spot it closes. The count is reported once, as a token on
// FinishMissionLoadingBegin.
//
// Reading the pair matters more than the count itself (MissionState.cs:221-233):
//   polls=1, waitMs large   -> the block was INSIDE a single TickLoading call. The main thread is
//                              spinning in native code, not waiting on workers. This is the #352
//                              PreloadHelper.WaitForMeshesToBeLoaded shape, and it is TAOM-data
//                              driven (an unresolvable physics-body name spins that loop forever).
//   polls ~ waitMs/16       -> genuine multi-frame async streaming at ~60fps; the main thread is
//                              healthy and the engine is waiting on disk/worker threads.
//   polls=0                 -> THE BINDING FAILED. It does not mean "no wait". Say so in triage.
//
// TickLoading is PRIVATE and takes a float; bound by string like the sibling LoadMission,
// BuildAgent, EndMissionInternal and OnFinalize patches in this category. AccessTools.Method
// searches non-public, so HarmonyPatchBindingTests covers it automatically.
//
// Inert outside a load by construction: MissionState only calls TickLoading while CurrentMission is
// in NewlyCreated/Initializing state (MissionState.cs:88-94), so this cannot fire during play.
[HarmonyPatch(typeof(MissionState), "TickLoading")]
[HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]
public static class MissionState_TickLoading_BattleLoad_Patch
{
    private static IBattleLoadDiagnosticsService? _service;

    public static void Initialize(IBattleLoadDiagnosticsService service) => _service = service;

    [HarmonyPrefix]
    public static void Prefix()
    {
        // Deliberately does NOT read IsEnabled: that resolves MCM's static Instance, and this runs
        // every frame of every load. The counter is state, not I/O — the service gates the one line
        // that eventually reports it.
        try { _service?.NoteLoadingPoll(); }
        catch { /* diagnostic only */ }
    }
}
