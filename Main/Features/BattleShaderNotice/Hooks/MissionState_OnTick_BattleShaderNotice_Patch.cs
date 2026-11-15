using HarmonyLib;
using TAOM.Core.Logging;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.BattleShaderNotice.Hooks;

// Patch155: tells BattleShaderNoticeService, once per frame, whether the mission is in its render wait.
//
// v1.5.7 MissionState.OnTick reaches TickMission only through `Handler == null || Handler.RenderIsReady()`, and
// RenderIsReady is MissionScreen.MissionStartedRendering() -> SceneView.ReadyToRender(), false while the scene's shaders
// compile. FirstMissionTickAfterLoading is set with EnableGlobalLoadingWindow and cleared by the first TickMission, so
// "FirstMissionTickAfterLoading and Continuing" is exactly that wait (the same window Patch43's
// MissionState_OnTick_RenderWait_Patch measures).
//
// POSTFIX: on the frame the mission finally ticks, TickMission has already cleared the flag, so that frame reads as
// "not waiting" and closes the notice.
//
// Per-frame hook on every mission: the two property reads come first, the service call only while waiting or while the
// notice is up, and the native shader count only on a waiting frame before the notice starts.
[HarmonyPatch(typeof(MissionState), "OnTick")]
[HarmonyPatchCategory(BattleShaderNoticeModule.PatchCategory)]
public static class MissionState_OnTick_BattleShaderNotice_Patch
{
    private static BattleShaderNoticeService? _service;
    private static IModLogger? _logger;
    private static bool _reported;

    public static void Initialize(BattleShaderNoticeService service, IModLogger logger)
    {
        _service = service;
        _logger = logger;
    }

    [HarmonyPostfix]
    public static void Postfix(MissionState __instance)
    {
        try
        {
            var service = _service;
            if (service == null || service.IsDedicatedServer) return;

            bool waiting = __instance.FirstMissionTickAfterLoading
                && __instance.CurrentMission?.CurrentState == Mission.State.Continuing;
            if (!waiting && !service.IsShowing) return;

            int queued = -1;
            if (waiting && !service.IsShowing)
            {
                try { queued = Utilities.GetNumberOfShaderCompilationsInProgress(); }
                catch { queued = -1; /* swallow-ok: an unreadable count means "do not start", which -1 says */ }
            }

            service.OnMissionStateTick(waiting, queued);
        }
        catch (System.Exception e)
        {
            // A notice must never break a mission tick; report once, not every frame.
            if (_reported) return;
            _reported = true;
            _logger?.LogWarning($"[BattleShaderNotice] tick hook failed: {e.GetType().Name}");
        }
    }
}
