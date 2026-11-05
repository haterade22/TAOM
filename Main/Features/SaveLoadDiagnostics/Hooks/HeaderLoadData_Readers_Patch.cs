using System;
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
// methods → nameof targets, main category: a rename fails the build, so no Prepare.
//
// One site class per target with a constant step name (plan 114, LOAD-TIME-03): a finalizer
// that binds __originalMethod pays MethodBase.GetMethodFromHandle on every call, and these
// run on every header of every load, mostly on parallel workers.
public static class HeaderLoadData_Readers_Patch
{
    private const string Category = "Patch61_SaveLoadDiagnostics";

    private static ISaveLoadDiagnosticsService? _service;

    public static void Initialize(ISaveLoadDiagnosticsService service) => _service = service;

    internal static void Report(Exception? exception, object? instance, string step)
    {
        var svc = _service;
        if (svc == null || exception == null) return;
        try
        {
            string detail = instance switch
            {
                ObjectHeaderLoadData o =>
                    $"kind=objectHeader step={step} id={o.Id} saveId='{o.SaveId?.GetStringId() ?? "<null>"}'",
                ContainerHeaderLoadData c =>
                    $"kind=containerHeader step={step} id={c.Id} saveId='{c.SaveId?.GetStringId() ?? "<null>"}' " +
                    $"type='{c.TypeDefinition?.Type?.FullName ?? "<unresolved>"}' containerType={c.ContainerType} elements={c.ElementCount}",
                _ => $"kind=header step={step}",
            };
            svc.LogFault(SaveLoadPhase.GraphFault, detail, exception);
        }
        catch { /* diagnostic only */ }
    }

    [HarmonyPatchCategory(Category)]
    internal static class ObjectHeaderReadersSite
    {
        internal const string Step = nameof(ObjectHeaderLoadData.InitialieReaders);
        [HarmonyTargetMethod]
        private static MethodBase TargetMethod() =>
            AccessTools.Method(typeof(ObjectHeaderLoadData), nameof(ObjectHeaderLoadData.InitialieReaders));
        [HarmonyFinalizer, HarmonyPriority(Priority.First)]
        private static void Finalizer(Exception? __exception, object __instance) => Report(__exception, __instance, Step);
    }

    [HarmonyPatchCategory(Category)]
    internal static class ContainerHeaderReadersSite
    {
        internal const string Step = nameof(ContainerHeaderLoadData.InitialieReaders);
        [HarmonyTargetMethod]
        private static MethodBase TargetMethod() =>
            AccessTools.Method(typeof(ContainerHeaderLoadData), nameof(ContainerHeaderLoadData.InitialieReaders));
        [HarmonyFinalizer, HarmonyPriority(Priority.First)]
        private static void Finalizer(Exception? __exception, object __instance) => Report(__exception, __instance, Step);
    }

    [HarmonyPatchCategory(Category)]
    internal static class ContainerHeaderCreateObjectSite
    {
        internal const string Step = nameof(ContainerHeaderLoadData.CreateObject);
        [HarmonyTargetMethod]
        private static MethodBase TargetMethod() =>
            AccessTools.Method(typeof(ContainerHeaderLoadData), nameof(ContainerHeaderLoadData.CreateObject));
        [HarmonyFinalizer, HarmonyPriority(Priority.First)]
        private static void Finalizer(Exception? __exception, object __instance) => Report(__exception, __instance, Step);
    }
}
