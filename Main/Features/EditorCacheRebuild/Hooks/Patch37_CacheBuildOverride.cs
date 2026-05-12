using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Map.DistanceCache;
using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.EditorCacheRebuild.Hooks;

[HarmonyPatch]
[HarmonyPatchCategory("Patch37_EditorCacheRebuild")]
public static class Patch37_CacheBuildOverride
{
    private const string SandBoxViewAssemblyName = "SandBox.View";
    private const string SettlementRecordTypeName = "SandBox.View.Map.SettlementPositionScript+SettlementRecord";
    private const string AssemblyQualifiedName = SettlementRecordTypeName + ", " + SandBoxViewAssemblyName;

    /// <summary>
    /// Resolves the private-nested SettlementRecord type.
    /// Bannerlord uses Assembly.LoadFrom which puts mod assemblies in a load context that
    /// Type.GetType doesn't search by default. AppDomain.GetAssemblies() fallback is required.
    /// </summary>
    private static Type? FindSettlementRecordType()
    {
        var direct = Type.GetType(AssemblyQualifiedName);
        if (direct != null) return direct;

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.GetName().Name != SandBoxViewAssemblyName) continue;
            var fromLoaded = assembly.GetType(SettlementRecordTypeName);
            if (fromLoaded != null) return fromLoaded;
        }
        return null;
    }

    private static void TryLog(string message, bool error = false)
    {
        try
        {
            var logger = IoC.Resolve<IModLogger>();
            if (error) logger.LogError(message);
            else logger.LogInfo(message);
        }
        catch
        {
            // logger may not yet be wired at Prepare/TargetMethod time
        }
    }

    public static MethodBase? TargetMethod()
    {
        var settlementRecordType = FindSettlementRecordType();
        if (settlementRecordType == null)
        {
            var loaded = string.Join(", ", AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetName().Name)
                .Where(n => n != null && n.Contains("SandBox"))
                .Take(10));
            TryLog($"[Patch37] TargetMethod: SettlementRecord NOT FOUND. SandBox-related assemblies loaded: [{loaded}]", error: true);
            return null;
        }

        var closedType = typeof(NavigationCache<>).MakeGenericType(settlementRecordType);
        var method = AccessTools.Method(closedType, "GenerateCacheData");
        TryLog($"[Patch37] TargetMethod: resolved {closedType.FullName}.GenerateCacheData (method={method?.Name})");
        return method;
    }

    public static bool Prepare()
    {
        var type = FindSettlementRecordType();
        TryLog($"[Patch37] Prepare: SettlementRecord {(type != null ? "FOUND" : "NOT FOUND")}");
        return type != null;
    }

    public static bool Prefix(object __instance)
    {
        if (__instance == null) return true;

        IModLogger? logger = null;
        try
        {
            logger = IoC.Resolve<IModLogger>();
            var configProvider = IoC.Resolve<ICacheRebuildConfigProvider>();
            var config = configProvider.GetConfig();

            logger.LogInfo($"[Patch37] cache build hook fired on instance type: {__instance.GetType().FullName}");

            if (!config.Enabled || config.ForceVanilla)
            {
                logger.LogInfo($"[Patch37] feature disabled (enabled={config.Enabled}, forceVanilla={config.ForceVanilla}); running vanilla cache build");
                return true;
            }

            logger.LogInfo("[Patch37] intercepting vanilla cache build — routing to TAOM CacheBuilderService");

            var adapter = new NavigationCacheAdapter(__instance, logger);
            var service = IoC.Resolve<IDistanceCacheBuilderService>();
            var result = service.Build(adapter, CancellationToken.None);

            logger.LogInfo(
                $"[Patch37] build returned: cancelled={result.Cancelled}, " +
                $"phase1={result.Phase1.PairsComputed}pairs/{result.Phase1.ElapsedSeconds:F1}s, " +
                $"phase2={result.Phase2.NeighborPairsAdded}neighbors/{result.Phase2.ElapsedSeconds:F1}s, " +
                $"smokeTest={result.SmokeTest.Outcome}, total={result.TotalSeconds:F1}s");

            return false;
        }
        catch (Exception ex)
        {
            // Codex Finding 3 (P2): by the time we catch, the service may have run Phase 0 and/or
            // mutated the distance/neighbor dicts. Vanilla `GenerateCacheData` is NOT safe to rerun
            // on a partially populated instance — `SetClosestSettlementToFaceIndex` uses
            // `Dictionary.Add` and would throw on duplicate face ids. Returning false skips vanilla;
            // the user gets no cache update for this NavigationType this iteration, but the cache
            // state is at least internally consistent for whatever the service already wrote.
            logger?.LogError(
                $"[Patch37] EXCEPTION — service failed AFTER mutating cache; SKIPPING vanilla fallback to avoid corruption. " +
                $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            return false;
        }
    }
}
