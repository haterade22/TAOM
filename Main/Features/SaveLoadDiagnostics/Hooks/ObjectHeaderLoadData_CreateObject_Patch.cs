using System;
using HarmonyLib;
using TaleWorlds.SaveSystem.Load;
using TAOM.Features.SaveLoadDiagnostics.Domain;

namespace TAOM.Features.SaveLoadDiagnostics.Hooks;

// CreateObject SILENTLY leaves LoadedObject null when the save carries a SaveId this build
// has no TypeDefinition for (a save written by a build whose SaveableTypeDefiner set
// differs). The engine never reports it — downstream references just load as null. Runs
// once per saved object (~10^5), so the happy path is one null check and the service
// dedups per (kind, saveId).
[HarmonyPatch(typeof(ObjectHeaderLoadData), nameof(ObjectHeaderLoadData.CreateObject))]
[HarmonyPatchCategory("Patch61_SaveLoadDiagnostics")]
public static class ObjectHeaderLoadData_CreateObject_Patch
{
    private static ISaveLoadDiagnosticsService? _service;

    public static void Initialize(ISaveLoadDiagnosticsService service) => _service = service;

    [HarmonyPostfix]
    public static void Postfix(ObjectHeaderLoadData __instance)
    {
        var svc = _service;
        if (svc == null || __instance.TypeDefinition != null) return;
        try { svc.MarkUnknownSaveId("object", __instance.SaveId?.GetStringId() ?? "<null>"); }
        catch { /* diagnostic only */ }
    }

    [HarmonyFinalizer]
    [HarmonyPriority(Priority.First)]
    public static void Finalizer(Exception? __exception, ObjectHeaderLoadData __instance)
    {
        var svc = _service;
        if (svc == null || __exception == null) return;
        try
        {
            svc.LogFault(SaveLoadPhase.GraphFault,
                $"kind=objectCreate saveId='{__instance?.SaveId?.GetStringId() ?? "<null>"}'",
                __exception);
        }
        catch { /* diagnostic only */ }
    }
}
