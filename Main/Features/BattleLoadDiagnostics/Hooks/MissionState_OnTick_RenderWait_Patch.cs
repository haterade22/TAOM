using HarmonyLib;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.BattleLoadDiagnostics.Hooks;

// Phase 4f — the FinishMissionLoadingDone -> BattlePlayable window, from the inside.
//
// Why (player bundle b18f3441, 2026-09-04): a load sat 305 s past FinishMissionLoadingDone with
// NOTHING in the TAOM phase log, and the stall watchdog (removed 2026-11-12) wrote a crash bundle for it. The engine's
// own rgl_log for that exact 290-second window holds 818 `compile_shader` lines plus two others:
// the load was working the whole time. MissionState.OnTick reaches TickMission only through
//
//     if (!flag && (Handler == null || Handler.RenderIsReady())) TickMission(realDt);
//
// and Handler is MissionScreen, whose IMissionSystemHandler.RenderIsReady() returns
// MissionStartedRendering() -> the native SceneView.ReadyToRender(). That stays false while the
// scene's shaders compile, and FinishMissionLoading's last line (Scene.ResumeLoadingRenderings)
// is what starts the compile flood. So on a cold shader cache the mission sits one frame short of
// playable, BattleLoadPhaseBehavior.OnMissionTick never runs, and the loading window never closes.
// Verified against the installed v1.5.7: MissionState.cs:110-113, MissionScreen.cs:2370-2376
// (MissionStartedRendering) and :2907-2909 (RenderIsReady).
//
// POSTFIX by design: on the frame the mission finally ticks, TickMission has already cleared
// FirstMissionTickAfterLoading, so the guard skips it. The marker fires only on frames that did
// NOT tick, which is exactly the window being measured.
//
// This is a PER-FRAME hook, so the guard order is load-bearing: two property reads before anything
// else, and the native shader read only once past them. The 1 Hz throttle lives in the service
// (NoteLoadingPoll's division of labour), not here.
//
// Observation matrix (static-state rule, .claude/rules/harmony-patches.md): this postfix keeps no
// state between frames. The value goes only into the 1 Hz log stamp, where -1 means the native
// read threw (the stamp then leaves the shaders= token out) and 0 means nothing is compiling.
//
// MissionState.OnTick is `protected override`, so it is bound BY STRING like the sibling
// TickLoading / FinishMissionLoading patches in this category. AccessTools.Method searches
// non-public, so HarmonyPatchBindingTests covers it automatically; Patch43LoadPhaseBindingTests adds
// the named message and the not-overloaded guard.
[HarmonyPatch(typeof(MissionState), "OnTick")]
[HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]
public static class MissionState_OnTick_RenderWait_Patch
{
    private static IBattleLoadDiagnosticsService? _service;

    public static void Initialize(IBattleLoadDiagnosticsService service) => _service = service;

    [HarmonyPostfix]
    public static void Postfix(MissionState __instance)
    {
        try
        {
            // Cheap guards first — this runs every frame of every mission for the mission's whole life.
            if (!__instance.FirstMissionTickAfterLoading) return;
            if (!BattleLoadLoadingWindow.IsOpen) return;

            // Continuing means FinishMissionLoading has run: the async-load buckets are already
            // measured by polls=/waitMs=, and only the render-ready wait is left.
            var mission = __instance.CurrentMission;
            if (mission == null || mission.CurrentState != Mission.State.Continuing) return;

            int shaders;
            try { shaders = Utilities.GetNumberOfShaderCompilationsInProgress(); }
            catch { shaders = -1; }

            _service?.NoteWaitingForRender(shaders);
        }
        catch { /* diagnostic only — a diagnostic must never break a mission tick */ }
    }
}
