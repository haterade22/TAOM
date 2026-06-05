using HarmonyLib;
using TAOM.Core.Logging;
using TAOM.Features.Spider;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Spider.Hooks;

/// <summary>
/// Thin prefix on the universal agent-spawn chokepoint (Patch45_SpiderTroopSpawn). For the recruitable
/// spider troop anchor it REPLACES the vanilla spawn with a detached non-humanoid spider agent
/// (<see cref="SpiderDetachedAgentSpawner"/>) and returns <c>false</c> to skip the humanoid spawn that
/// would crash. For every other character — and if the detached spawn fails for any reason — it returns
/// <c>true</c> and the vanilla spawn proceeds unchanged (fail-open: the spider then spawns as its
/// harmless humanoid anchor instead of crashing). Co-exists with Patch23_BannerColorPersistence on the
/// same method. All decision logic is in <see cref="ISpiderTroopSpawnService"/>; the engine-coupled
/// spawn is in the boundary spawner. See docs/reviews/rca-spider-troop-2026-06-04.md.
/// </summary>
[HarmonyPatch(typeof(Mission), nameof(Mission.SpawnAgent))]
[HarmonyPatchCategory("Patch45_SpiderTroopSpawn")]
public static class Mission_SpawnAgent_SpiderSwap_Patch
{
    private static ISpiderTroopSpawnService? _service;

    public static void Initialize(ISpiderTroopSpawnService service, IModLogger logger)
    {
        _service = service;
        SpiderDetachedAgentSpawner.Initialize(logger);
    }

    [HarmonyPrefix]
    public static bool Prefix(Mission __instance, AgentBuildData agentBuildData, ref Agent __result)
    {
        if (_service == null || agentBuildData == null)
            return true;
        if (!_service.IsSpiderTroop(agentBuildData.AgentCharacter?.StringId))
            return true;

        Agent? spider = SpiderDetachedAgentSpawner.TrySpawnDetachedSpider(__instance, agentBuildData);
        if (spider == null)
            return true; // fail-open → vanilla spawns the humanoid anchor (no crash)

        __result = spider;
        return false; // detached spider built — skip the humanoid vanilla spawn
    }
}
