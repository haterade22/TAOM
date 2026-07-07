using System;
using HarmonyLib;
using TaleWorlds.SaveSystem.Load;
using TAOM.Features.SaveLoadDiagnostics.Domain;

namespace TAOM.Features.SaveLoadDiagnostics.Hooks;

// The money hook for OBJECT data. LoadContext.Load's catch block discards the stack and
// prints only ex.Message (usually the useless AggregateException "One or more errors
// occurred" from TWParallel) — this Finalizer fires at the interior throw site FIRST,
// naming the exact saved type whose data blew up. Runs on TWParallel worker threads:
// happy path is a single null check; the service is lock-free and fault-throttled.
[HarmonyPatch(typeof(LoadContext), "CreateLoadData")]
[HarmonyPatchCategory("Patch61_SaveLoadDiagnostics")]
public static class LoadContext_CreateLoadData_Patch
{
    private static ISaveLoadDiagnosticsService? _service;

    public static void Initialize(ISaveLoadDiagnosticsService service) => _service = service;

    // Void + Priority.First: observe the exception before any swallowing finalizer, keep
    // Harmony's true-rethrow (original stack preserved), structurally unable to swallow.
    [HarmonyFinalizer]
    [HarmonyPriority(Priority.First)]
    public static void Finalizer(Exception? __exception, int i, ObjectHeaderLoadData? header)
    {
        var svc = _service;
        if (svc == null || __exception == null) return;
        try
        {
            svc.LogFault(SaveLoadPhase.GraphFault,
                $"kind=object idx={i} saveId='{header?.SaveId?.GetStringId() ?? "<null>"}' type='{header?.TypeDefinition?.Type?.FullName ?? "<unresolved>"}'",
                __exception);
        }
        catch { /* diagnostic only */ }
    }
}
