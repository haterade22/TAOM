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
// (review 2026-07-07 HIGH). ContainerLoadData is an INTERNAL type → TargetMethod() +
// reflection; the header property is cached once. Own category so drift here can't kill
// any other hook (Harmony aborts a category on the first failing class).
//
// One site class per target with a constant step name (plan 114, LOAD-TIME-03): a finalizer
// that binds __originalMethod pays MethodBase.GetMethodFromHandle on every call, and these
// run on every container of every load, mostly on parallel workers.
public static class ContainerLoadData_Fill_Patch
{
    internal const string Category = "Patch61_SaveLoadDiagnostics_ContainerFill";

    private static readonly HashSet<string> _warned = new HashSet<string>();

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

    // Resolves one target by name; logs the missing type, method or header property once. A missing
    // method costs that site alone, because each site's Prepare skips only itself.
    internal static MethodBase? Resolve(string name)
    {
        var type = ResolveContainerLoadDataType();
        if (type == null)
        {
            // Patch57 precedent: name the missing binding before Harmony's generic error.
            WarnOnce("[SaveLoad] ContainerLoadData type not found — container GraphFault hooks not applied (engine drift?)");
            return null;
        }
        _headerProperty ??= AccessTools.Property(type, "ContainerHeaderLoadData");
        if (_headerProperty == null)
            WarnOnce("[SaveLoad] ContainerLoadData.ContainerHeaderLoadData property not found — container GraphFault stamps will lack saveId/type attribution");
        var method = AccessTools.Method(type, name);
        if (method == null)
            WarnOnce($"[SaveLoad] ContainerLoadData.{name} not found — that container fault site is uninstrumented (engine drift?)");
        return method;
    }

    // Read the header only when the instance is really a ContainerLoadData: GetValue on another type
    // would throw and lose the stamp.
    internal static void Report(Exception? exception, object? instance, string step)
    {
        var svc = _service;
        if (svc == null || exception == null) return;
        try
        {
            var property = _headerProperty;
            var header = property != null && property.DeclaringType!.IsInstanceOfType(instance)
                ? property.GetValue(instance) as ContainerHeaderLoadData
                : null;
            svc.LogFault(SaveLoadPhase.GraphFault,
                $"kind=container step={step} saveId='{header?.SaveId?.GetStringId() ?? "<null>"}' " +
                $"type='{header?.TypeDefinition?.Type?.FullName ?? "<unresolved>"}' elements={header?.ElementCount ?? -1}",
                exception);
        }
        catch { /* diagnostic only */ }
    }

    private static void WarnOnce(string message)
    {
        if (_warned.Add(message))
            _logger?.LogWarning(message);
    }

    // Void + Priority.First on every site: observe before any swallowing finalizer; true rethrow.
    [HarmonyPatchCategory(Category)]
    internal static class InitializeReadersSite
    {
        internal const string Step = "InitializeReaders";
        [HarmonyPrepare] private static bool Prepare() => Resolve(Step) != null;
        [HarmonyTargetMethod] private static MethodBase? TargetMethod() => Resolve(Step);
        [HarmonyFinalizer, HarmonyPriority(Priority.First)]
        private static void Finalizer(Exception? __exception, object __instance) => Report(__exception, __instance, Step);
    }

    [HarmonyPatchCategory(Category)]
    internal static class FillCreatedObjectSite
    {
        internal const string Step = "FillCreatedObject";
        [HarmonyPrepare] private static bool Prepare() => Resolve(Step) != null;
        [HarmonyTargetMethod] private static MethodBase? TargetMethod() => Resolve(Step);
        [HarmonyFinalizer, HarmonyPriority(Priority.First)]
        private static void Finalizer(Exception? __exception, object __instance) => Report(__exception, __instance, Step);
    }

    [HarmonyPatchCategory(Category)]
    internal static class ReadSite
    {
        internal const string Step = "Read";
        [HarmonyPrepare] private static bool Prepare() => Resolve(Step) != null;
        [HarmonyTargetMethod] private static MethodBase? TargetMethod() => Resolve(Step);
        [HarmonyFinalizer, HarmonyPriority(Priority.First)]
        private static void Finalizer(Exception? __exception, object __instance) => Report(__exception, __instance, Step);
    }

    [HarmonyPatchCategory(Category)]
    internal static class FillObjectSite
    {
        internal const string Step = "FillObject";
        [HarmonyPrepare] private static bool Prepare() => Resolve(Step) != null;
        [HarmonyTargetMethod] private static MethodBase? TargetMethod() => Resolve(Step);
        [HarmonyFinalizer, HarmonyPriority(Priority.First)]
        private static void Finalizer(Exception? __exception, object __instance) => Report(__exception, __instance, Step);
    }
}
