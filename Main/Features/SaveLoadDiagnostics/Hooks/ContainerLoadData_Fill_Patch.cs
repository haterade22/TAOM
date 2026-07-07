using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.SaveSystem.Load;
using TAOM.Core.Logging;
using TAOM.Features.SaveLoadDiagnostics.Domain;

namespace TAOM.Features.SaveLoadDiagnostics.Hooks;

// The money hook for CONTAINER data — dictionaries and lists, which is where TAOM's
// growing SyncData surfaces live (_taom_heroRaceMap, _taom_specialResources, the career
// dicts). LoadContext.Load fills containers inline under TWParallel (NOT via
// CreateLoadData), so the object-side hook never sees these faults. InitializeReaders is
// included: it runs per container BEFORE the fill methods and its raw dictionary-indexer
// entry lookups are the FIRST throw site for a truncated/corrupted container chunk
// (review 2026-07-07 HIGH). ContainerLoadData is an INTERNAL type → TargetMethods() +
// reflection; the header property is cached once. Own category so drift here can't kill
// any other hook (Harmony aborts a category on the first failing class).
[HarmonyPatchCategory("Patch61_SaveLoadDiagnostics_ContainerFill")]
public static class ContainerLoadData_Fill_Patch
{
    private static ISaveLoadDiagnosticsService? _service;
    private static IModLogger? _logger;
    private static PropertyInfo? _headerProperty;

    public static void Initialize(ISaveLoadDiagnosticsService service, IModLogger logger)
    {
        _service = service;
        _logger = logger;
    }

    // Exposed internal for SaveLoadDiagnosticsBindingTests.
    internal static Type? ResolveContainerLoadDataType() =>
        AccessTools.TypeByName("TaleWorlds.SaveSystem.Load.ContainerLoadData");

    internal static readonly string[] PatchedMethodNames = { "InitializeReaders", "FillCreatedObject", "Read", "FillObject" };

    [HarmonyTargetMethods]
    public static IEnumerable<MethodBase> TargetMethods()
    {
        var type = ResolveContainerLoadDataType();
        if (type == null)
        {
            // Patch57 precedent: name the missing binding before Harmony's generic error.
            _logger?.LogWarning("[SaveLoad] ContainerLoadData type not found — container GraphFault hooks not applied (engine drift?)");
            yield break;
        }
        _headerProperty = AccessTools.Property(type, "ContainerHeaderLoadData");
        if (_headerProperty == null)
            _logger?.LogWarning("[SaveLoad] ContainerLoadData.ContainerHeaderLoadData property not found — container GraphFault stamps will lack saveId/type attribution");
        foreach (var name in PatchedMethodNames)
        {
            var method = AccessTools.Method(type, name);
            if (method == null)
                _logger?.LogWarning($"[SaveLoad] ContainerLoadData.{name} not found — that container fault site is uninstrumented (engine drift?)");
            else
                yield return method;
        }
    }

    // Void + Priority.First: observe before any swallowing finalizer; true rethrow.
    [HarmonyFinalizer]
    [HarmonyPriority(Priority.First)]
    public static void Finalizer(Exception? __exception, object __instance, MethodBase __originalMethod)
    {
        var svc = _service;
        if (svc == null || __exception == null) return;
        try
        {
            var header = _headerProperty?.GetValue(__instance) as ContainerHeaderLoadData;
            svc.LogFault(SaveLoadPhase.GraphFault,
                $"kind=container step={__originalMethod?.Name} saveId='{header?.SaveId?.GetStringId() ?? "<null>"}' " +
                $"type='{header?.TypeDefinition?.Type?.FullName ?? "<unresolved>"}' elements={header?.ElementCount ?? -1}",
                __exception);
        }
        catch { /* diagnostic only */ }
    }
}
