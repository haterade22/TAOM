using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.SaveSystem.Load;
using TAOM.Features.SaveLoadDiagnostics.Domain;

namespace TAOM.Features.SaveLoadDiagnostics.Hooks;

// Header-phase attribution (review 2026-07-07 MED). LoadContext.Load's header block parses
// every object/container header under TWParallel (InitialieReaders — the engine's typo) and
// then instantiates each container via raw Activator over the save-file-supplied
// ElementCount (ContainerHeaderLoadData.CreateObject — a corrupt negative count throws).
// None of these sites go through CreateLoadData/ContainerLoadData, so without this hook a
// header-phase fault reached the engine's swallow with zero interior stamps. Public types +
// methods → typeof-based TargetMethods, main category.
[HarmonyPatchCategory("Patch61_SaveLoadDiagnostics")]
public static class HeaderLoadData_Readers_Patch
{
    private static ISaveLoadDiagnosticsService? _service;

    public static void Initialize(ISaveLoadDiagnosticsService service) => _service = service;

    [HarmonyTargetMethods]
    public static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(ObjectHeaderLoadData), nameof(ObjectHeaderLoadData.InitialieReaders));
        yield return AccessTools.Method(typeof(ContainerHeaderLoadData), nameof(ContainerHeaderLoadData.InitialieReaders));
        yield return AccessTools.Method(typeof(ContainerHeaderLoadData), nameof(ContainerHeaderLoadData.CreateObject));
    }

    [HarmonyFinalizer]
    [HarmonyPriority(Priority.First)]
    public static void Finalizer(Exception? __exception, object __instance, MethodBase __originalMethod)
    {
        var svc = _service;
        if (svc == null || __exception == null) return;
        try
        {
            string detail = __instance switch
            {
                ObjectHeaderLoadData o =>
                    $"kind=objectHeader step={__originalMethod?.Name} id={o.Id} saveId='{o.SaveId?.GetStringId() ?? "<null>"}'",
                ContainerHeaderLoadData c =>
                    $"kind=containerHeader step={__originalMethod?.Name} id={c.Id} saveId='{c.SaveId?.GetStringId() ?? "<null>"}' " +
                    $"type='{c.TypeDefinition?.Type?.FullName ?? "<unresolved>"}' containerType={c.ContainerType} elements={c.ElementCount}",
                _ => $"kind=header step={__originalMethod?.Name}",
            };
            svc.LogFault(SaveLoadPhase.GraphFault, detail, __exception);
        }
        catch { /* diagnostic only */ }
    }
}
