using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.SaveSystem.Load;
using TAOM.Features.SaveLoadDiagnostics.Domain;

namespace TAOM.Features.SaveLoadDiagnostics.Hooks;

// A null LoadResult here is the exact moment SandBoxSaveHelper.LoadGameAction shows the
// generic "A problem occured while trying to load the saved game." dialog — the engine
// discards the real error (MBSaveLoad prints only "Error: Could not load the game!").
// The GraphFault/LoadFault stamps earlier in the log carry the actual cause.
[HarmonyPatch(typeof(MBSaveLoad), nameof(MBSaveLoad.LoadSaveGameData), typeof(string))]
[HarmonyPatchCategory("Patch61_SaveLoadDiagnostics")]
public static class MBSaveLoad_LoadSaveGameData_Patch
{
    private static ISaveLoadDiagnosticsService? _service;

    public static void Initialize(ISaveLoadDiagnosticsService service) => _service = service;

    [HarmonyPostfix]
    public static void Postfix(LoadResult? __result, string saveName)
    {
        var svc = _service;
        if (svc == null) return;
        try
        {
            if (__result == null)
                svc.LogFault(SaveLoadPhase.LoadFailed,
                    $"name='{saveName}' — engine returned null LoadResult; the load-failure dialog is now on screen (see GraphFault/LoadFault stamps above for the cause)",
                    null);
            else
                svc.LogPhase(SaveLoadPhase.LoadDataOk, $"name='{saveName}'");
        }
        catch { /* diagnostic only */ }
    }
}
