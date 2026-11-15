using System.Reflection;
using HarmonyLib;
using TAOM.Core.Logging;
using TaleWorlds.MountAndBlade.GauntletUI;

namespace TAOM.Features.BattleShaderNotice.Hooks;

// Patch155: writes BattleShaderNoticeService.LoadingScreenLine into the loading window's DescriptionText while the
// battle notice is up. TAOM's TAOMLoadingWindow.xml shows DescriptionText in its bottom bar; in single player nothing
// else writes it (v1.5.7 LoadingWindowViewModel.SetForEmpty sets "" on every enable).
//
// v1.5.7: `internal void LoadingWindowViewModel.Update()`, called every frame from
// GauntletDefaultLoadingWindowManager.OnLateTick, on the game thread. The line is set in the frames before the
// shader compile stops the game thread, so it stays on screen through the stop. DescriptionText's setter skips an
// equal value, so the per-frame write costs a string compare. The next enable's SetForEmpty clears it.
[HarmonyPatch]
[HarmonyPatchCategory(BattleShaderNoticeModule.PatchCategory)]
[ClientOnly]
public static class LoadingWindowViewModel_Update_BattleShaderLine_Patch
{
    private static BattleShaderNoticeService? _service;
    private static IModLogger? _logger;
    private static bool _reported;

    public static void Initialize(BattleShaderNoticeService service, IModLogger logger)
    {
        _service = service;
        _logger = logger;
    }

    static MethodBase TargetMethod() => AccessTools.Method(typeof(LoadingWindowViewModel), "Update");

    [HarmonyPostfix]
    public static void Postfix(LoadingWindowViewModel __instance)
    {
        try
        {
            var line = _service?.LoadingScreenLine;
            if (line == null || __instance == null || !__instance.Enabled) return;
            __instance.DescriptionText = line;
        }
        catch (System.Exception e)
        {
            if (_reported) return;
            _reported = true;
            _logger?.LogWarning($"[BattleShaderNotice] loading-screen line failed: {e.GetType().Name}");
        }
    }
}
