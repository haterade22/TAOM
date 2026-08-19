using HarmonyLib;
using TAOM.Adapters;
using TaleWorlds.CampaignSystem.AgentOrigins;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.BannerColorPersistence.Hooks;

[HarmonyPatch(typeof(Mission), nameof(Mission.SpawnAgent))]
[HarmonyPatchCategory("Patch23_BannerColorPersistence")]
public static class Mission_SpawnAgent_Patch
{
    private static IBannerColorService? _service;
    private static IBannerHeroAdapter? _heroAdapter;
    private static IAgentColorStore? _colorStore;

    public static void Initialize(IBannerColorService service, IBannerHeroAdapter heroAdapter, IAgentColorStore colorStore)
    {
        _service = service;
        _heroAdapter = heroAdapter;
        _colorStore = colorStore;
    }

    private static ClanColorInfo? ResolveColors(AgentBuildData agentBuildData)
    {
        var origin = agentBuildData.AgentOrigin;
        if (origin == null) return null;

        TaleWorlds.CampaignSystem.Hero? leaderHero = null;

        if (origin is PartyAgentOrigin partyOrigin)
            leaderHero = partyOrigin.Party?.LeaderHero;
        else if (origin is PartyGroupAgentOrigin partyGroupOrigin)
            leaderHero = partyGroupOrigin.Party?.LeaderHero;
        else if (origin is SimpleAgentOrigin simpleOrigin)
            leaderHero = simpleOrigin.Party?.LeaderHero;

        if (leaderHero == null) return null;

        var info = _heroAdapter?.GetClanColorInfoFromHero(leaderHero);
        if (info == null) return null;

        return (_service?.ShouldUseClanColor(info.Value) ?? false) ? info.Value : (ClanColorInfo?)null;
    }

    // v1.5.0 note on the equipment guard below. In v1.4.8 SpawnAgent decided spawn equipment inline
    // from agentBuildData.AgentOverridenSpawnEquipment, so that one check covered every
    // caller-supplied-equipment case. v1.5.0 extracted the decision into DecideAgentSpawnEquipment
    // and added an agentSpawnEquipment PARAMETER, so a caller can now supply equipment directly with
    // AgentOverridenSpawnEquipment still null (see Mission.SpawnTroopWithAgentBuildDataAndEquipment,
    // new in v1.5.0). Harmony binds prefix parameters by name, so naming the new parameter here
    // restores the guard's original meaning: do not recolour when the caller already chose the kit.
    // No shipped vanilla code calls that overload yet, so this is defensive rather than a live fix.
    [HarmonyPrefix]
    public static bool Prefix(AgentBuildData agentBuildData, Equipment agentSpawnEquipment)
    {
        if (!(_service?.IsEnabled() ?? false)) return true;
        if (agentBuildData.AgentOverridenSpawnEquipment != null) return true;
        if (agentSpawnEquipment != null) return true;

        var info = ResolveColors(agentBuildData);
        if (info == null) return true;

        agentBuildData.ClothingColor1(info.Value.Color1).ClothingColor2(info.Value.Color2);
        return true;
    }

    [HarmonyPostfix]
    public static void Postfix(Agent __result, AgentBuildData agentBuildData)
    {
        if (__result == null) return;
        if (!(_service?.IsAgentVisualColorsEnabled() ?? false)) return;

        var info = ResolveColors(agentBuildData);
        if (info == null) return;

        _colorStore?.Register(__result.Index, info.Value);
    }
}
