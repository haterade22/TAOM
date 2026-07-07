using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.SaveSystem.Load;
using TAOM.Features.SaveLoadDiagnostics.Domain;

namespace TAOM.Features.SaveLoadDiagnostics.Hooks;

// The deferred [LoadInitializationCallback] phase (review 2026-07-07 MED). Campaign loads
// run with loadAsLateInitialize:true, so object OnLoad callbacks execute from
// Game.LoadSaveGame AFTER SaveManager.Load returned success — past LoadDataOk. SaveShield
// (TAOM.Dependencies) finalizes both methods and SWALLOWS any exception there, converting
// a callback failure into a silently half-initialized campaign with a clean-looking log.
// This Finalizer (Priority.First → runs before SaveShield's swallow) stamps the fault; the
// Postfix stamps the clean-completion milestone.
[HarmonyPatchCategory("Patch61_SaveLoadDiagnostics")]
public static class LoadResult_InitializeCallbacks_Patch
{
    private static ISaveLoadDiagnosticsService? _service;

    public static void Initialize(ISaveLoadDiagnosticsService service) => _service = service;

    [HarmonyTargetMethods]
    public static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(LoadResult), nameof(LoadResult.InitializeObjects));
        yield return AccessTools.Method(typeof(LoadResult), nameof(LoadResult.AfterInitializeObjects));
    }

    [HarmonyFinalizer]
    [HarmonyPriority(Priority.First)]
    public static void Finalizer(Exception? __exception, MethodBase __originalMethod)
    {
        var svc = _service;
        if (svc == null || __exception == null) return;
        try
        {
            svc.LogFault(SaveLoadPhase.LoadFault,
                $"step={__originalMethod?.Name} — a [LoadInitializationCallback] threw AFTER deserialization; if no crash follows, SaveShield swallowed it and the campaign is half-initialized",
                __exception);
        }
        catch { /* diagnostic only */ }
    }

    [HarmonyPostfix]
    public static void Postfix(MethodBase __originalMethod)
    {
        var svc = _service;
        if (svc == null || __originalMethod?.Name != nameof(LoadResult.AfterInitializeObjects)) return;
        try { svc.LogPhase(SaveLoadPhase.ObjectsInitialized, string.Empty); }
        catch { /* diagnostic only */ }
    }
}
