using System;
using System.Text;
using HarmonyLib;
using TaleWorlds.SaveSystem;
using TaleWorlds.SaveSystem.Load;
using TAOM.Features.SaveLoadDiagnostics.Domain;

namespace TAOM.Features.SaveLoadDiagnostics.Hooks;

// Two failure shapes surface here:
// - Finalizer: an exception escaping SaveManager.Load uncaught — most commonly FileDriver.Load
//   returned null for an unreadable/corrupt file and the method NREs dereferencing
//   loadData.MetaData, but definer registration (FillWithCurrentTypes) and metadata version
//   parsing also throw through here — the logged exception chain names the real one.
//   VOID finalizer: reads __exception without returning it, so Harmony keeps true-rethrow
//   semantics (original stack preserved). Priority.First: SaveShield (TAOM.Dependencies)
//   finalizes this same method at default priority and SWALLOWS — we must observe first.
// - Postfix: LoadContext.Load caught the graph exception internally and returned false —
//   LoadResult.CreateFailed carries only the hardcoded "Not implemented" error, so the real
//   cause is in the GraphFault stamps logged by the interior hooks (when zero were captured,
//   the message says so instead of pointing at stamps that don't exist).
[HarmonyPatch(typeof(SaveManager), nameof(SaveManager.Load), typeof(string), typeof(ISaveDriver), typeof(bool))]
[HarmonyPatchCategory("Patch61_SaveLoadDiagnostics")]
public static class SaveManager_Load_Patch
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
            svc.LogFault(SaveLoadPhase.LoadFault,
                $"name='{saveName}' uncaught in SaveManager.Load — most commonly an unreadable/corrupt file (FileDriver returned null); the exception chain below names the real cause",
                __exception);
        }
        catch { /* diagnostic only */ }
    }

    [HarmonyPostfix]
    public static void Postfix(LoadResult? __result, string saveName)
    {
        var svc = _service;
        if (svc == null || __result == null || __result.Successful) return;
        try
        {
            var sb = new StringBuilder();
            var errors = __result.Errors;
            if (errors != null)
            {
                foreach (var error in errors)
                {
                    if (sb.Length > 0) sb.Append(" | ");
                    sb.Append(error.Message);
                }
            }
            var pointer = svc.FaultCount > 0
                ? "see GraphFault/LoadFault stamps above for the cause"
                : "NO interior stamp was captured — the fault was in an unhooked parse phase (header config / string entries / object resolve)";
            svc.LogFault(SaveLoadPhase.LoadFault,
                $"name='{saveName}' LoadResult.Successful=false errors=[{sb}] — graph deserialization failed; {pointer}",
                null);
        }
        catch { /* diagnostic only */ }
    }
}
