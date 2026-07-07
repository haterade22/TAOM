using System;
using System.Threading.Tasks;
using HarmonyLib;
using TaleWorlds.Library;
using TaleWorlds.SaveSystem;
using TAOM.Features.SaveLoadDiagnostics.Domain;

namespace TAOM.Features.SaveLoadDiagnostics.Hooks;

// Catches a bad WRITE at write time. For campaign saves this method runs inside the
// AsyncFileSaveDriver's Task.Run (AsyncFileSaveDriver delegates to its wrapped FileDriver),
// so the Finalizer fires ON the background writer thread at the original throw site — the
// #292 class (GameData.Write NRE over a null buffer) that previously produced a corrupt
// .sav discovered only at the next load. Also logs the write RESULT: an antivirus or
// OneDrive lock blocking the file write surfaces here as a non-Success SaveResult.
[HarmonyPatch(typeof(FileDriver), nameof(FileDriver.Save))]
[HarmonyPatchCategory("Patch61_SaveLoadDiagnostics")]
public static class FileDriver_Save_Patch
{
    private static ISaveLoadDiagnosticsService? _service;

    public static void Initialize(ISaveLoadDiagnosticsService service) => _service = service;

    [HarmonyFinalizer]
    [HarmonyPriority(Priority.First)]
    public static void Finalizer(Exception? __exception, string saveName)
    {
        var svc = _service;
        if (svc == null || __exception == null) return;
        try
        {
            svc.LogFault(SaveLoadPhase.SaveWriteFault,
                $"name='{saveName}' — the save WRITE itself threw; the .sav on disk is now suspect (do not trust the next load of it)",
                __exception);
        }
        catch { /* diagnostic only */ }
    }

    [HarmonyPostfix]
    public static void Postfix(Task<SaveResultWithMessage>? __result, string saveName)
    {
        var svc = _service;
        if (svc == null || __result == null || !__result.IsCompleted || __result.IsFaulted) return;
        try
        {
            var value = __result.Result; // FileDriver returns Task.FromResult — already completed
            if (value.SaveResult != SaveResult.Success)
                svc.LogFault(SaveLoadPhase.SaveWriteFault,
                    $"name='{saveName}' result={value.SaveResult} message='{value.Message}' — file write failed (disk full / antivirus / sync lock?)",
                    null);
        }
        catch { /* diagnostic only */ }
    }
}
