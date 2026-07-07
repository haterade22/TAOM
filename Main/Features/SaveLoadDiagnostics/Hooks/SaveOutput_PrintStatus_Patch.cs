using System;
using System.Text;
using HarmonyLib;
using TaleWorlds.SaveSystem.Save;
using TAOM.Features.SaveLoadDiagnostics.Domain;

namespace TAOM.Features.SaveLoadDiagnostics.Hooks;

// Game-thread completion report. Game.OnSaveCompleted calls PrintStatus first; touching a
// FAULTED async writer task's .Result here rethrows as AggregateException — the exact #292
// game-thread signature — which the Finalizer names before rethrowing. The Postfix logs
// the terminal SaveResult + serialization errors (SaveContext.Save failures surface here).
[HarmonyPatch(typeof(SaveOutput), nameof(SaveOutput.PrintStatus))]
[HarmonyPatchCategory("Patch61_SaveLoadDiagnostics")]
public static class SaveOutput_PrintStatus_Patch
{
    private static ISaveLoadDiagnosticsService? _service;

    public static void Initialize(ISaveLoadDiagnosticsService service) => _service = service;

    [HarmonyFinalizer]
    [HarmonyPriority(Priority.First)]
    public static void Finalizer(Exception? __exception)
    {
        var svc = _service;
        if (svc == null || __exception == null) return;
        try
        {
            svc.LogFault(SaveLoadPhase.SaveStatusFault,
                "async save-writer task faulted (surfaced at Game.OnSaveCompleted — the #292 signature); the just-written .sav is suspect",
                __exception);
        }
        catch { /* diagnostic only */ }
    }

    [HarmonyPostfix]
    public static void Postfix(SaveOutput __instance)
    {
        var svc = _service;
        if (svc == null || __instance == null) return;
        try
        {
            var errors = __instance.Errors;
            if (__instance.Result == TaleWorlds.Library.SaveResult.Success && (errors == null || errors.Length == 0))
            {
                svc.LogPhase(SaveLoadPhase.SaveCompleted, "result=Success");
                return;
            }

            var sb = new StringBuilder();
            if (errors != null)
            {
                foreach (var error in errors)
                {
                    if (sb.Length > 0) sb.Append(" | ");
                    sb.Append(error.Message);
                }
            }
            svc.LogFault(SaveLoadPhase.SaveCompleted,
                $"result={__instance.Result} errors=[{sb}]", null);
        }
        catch { /* diagnostic only */ }
    }
}
