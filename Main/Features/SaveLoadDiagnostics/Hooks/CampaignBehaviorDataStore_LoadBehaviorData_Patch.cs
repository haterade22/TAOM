using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TAOM.Core.Logging;
using TAOM.Features.SaveLoadDiagnostics.Domain;

namespace TAOM.Features.SaveLoadDiagnostics.Hooks;

// Per-behavior SyncData attribution, BOTH directions. Load: the engine reads every
// behavior's data via a raw (T)value cast (BehaviorSaveData.SyncData) with NO per-behavior
// try/catch — an InvalidCastException (a SyncData type changed between the writing and
// loading builds) aborts campaign start with zero context about WHICH behavior. Save: the
// collection pass (SaveBehaviorData, fired from OnBeforeSave BEFORE SaveManager.Save) has
// the same shape — a duplicate-record or serializer fault names no behavior. The Finalizer
// names it, then Harmony rethrows (void finalizer). Internal engine type → TargetMethods()
// + AccessTools.TypeByName, isolated in its own category.
[HarmonyPatchCategory("Patch61_SaveLoadDiagnostics_BehaviorData")]
public static class CampaignBehaviorDataStore_LoadBehaviorData_Patch
{
    private const string StoreTypeName = "TaleWorlds.CampaignSystem.CampaignBehaviorDataStore";

    private static ISaveLoadDiagnosticsService? _service;
    private static IModLogger? _logger;

    public static void Initialize(ISaveLoadDiagnosticsService service, IModLogger logger)
    {
        _service = service;
        _logger = logger;
    }

    internal static Type? ResolveStoreType() => AccessTools.TypeByName(StoreTypeName);

    internal static readonly string[] PatchedMethodNames = { "LoadBehaviorData", "SaveBehaviorData" };

    [HarmonyTargetMethods]
    public static IEnumerable<MethodBase> TargetMethods()
    {
        var type = ResolveStoreType();
        if (type == null)
        {
            _logger?.LogWarning("[SaveLoad] CampaignBehaviorDataStore type not found — per-behavior SyncData attribution not applied (engine drift?)");
            yield break;
        }
        foreach (var name in PatchedMethodNames)
        {
            var method = AccessTools.Method(type, name);
            if (method == null)
                _logger?.LogWarning($"[SaveLoad] CampaignBehaviorDataStore.{name} not found — that direction is uninstrumented (engine drift?)");
            else
                yield return method;
        }
    }

    [HarmonyFinalizer]
    [HarmonyPriority(Priority.First)]
    public static void Finalizer(Exception? __exception, CampaignBehaviorBase campaignBehavior, MethodBase __originalMethod)
    {
        var svc = _service;
        if (svc == null || __exception == null) return;
        try
        {
            var direction = __originalMethod?.Name == "SaveBehaviorData" ? "save" : "load";
            svc.LogFault(SaveLoadPhase.BehaviorSyncFault,
                $"dir={direction} behavior='{campaignBehavior?.GetType().FullName ?? "<null>"}' — this behavior's SyncData failed to {direction} (type changed between builds? duplicate record?)",
                __exception);
        }
        catch { /* diagnostic only */ }
    }
}
