using HarmonyLib;
using TaleWorlds.SaveSystem;

namespace TAOM.Features.SaveLoadDiagnostics.Hooks;

// Save lifecycle origin. Serialization runs on the calling (game) thread inside this
// method; the file WRITE continues on the AsyncFileSaveDriver background task, covered by
// FileDriver_Save_Patch + SaveOutput_PrintStatus_Patch.
[HarmonyPatch(typeof(SaveManager), nameof(SaveManager.Save))]
[HarmonyPatchCategory("Patch61_SaveLoadDiagnostics")]
public static class SaveManager_Save_Patch
{
    private static ISaveLoadDiagnosticsService? _service;

    public static void Initialize(ISaveLoadDiagnosticsService service) => _service = service;

    [HarmonyPrefix]
    public static void Prefix(string saveName)
    {
        try { _service?.BeginSaveAttempt(saveName ?? "<null>"); }
        catch { /* diagnostic only */ }
    }
}
