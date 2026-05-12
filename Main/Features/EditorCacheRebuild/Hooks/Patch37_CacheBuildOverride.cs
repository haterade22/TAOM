using System;
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
    private const string SettlementRecordTypeName = "SandBox.View.Map.SettlementPositionScript+SettlementRecord, SandBox.View";

    public static MethodBase? TargetMethod()
    {
        var settlementRecordType = Type.GetType(SettlementRecordTypeName);
        if (settlementRecordType == null)
            return null;

        var closedType = typeof(NavigationCache<>).MakeGenericType(settlementRecordType);
        return AccessTools.Method(closedType, "GenerateCacheData");
    }

    public static bool Prepare()
    {
        return Type.GetType(SettlementRecordTypeName) != null;
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
