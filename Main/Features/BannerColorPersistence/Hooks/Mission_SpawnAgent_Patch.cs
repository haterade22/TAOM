using HarmonyLib;
using TAOM.Adapters;
using TaleWorlds.CampaignSystem.AgentOrigins;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.BannerColorPersistence.Hooks;

[HarmonyPatch(typeof(Mission), nameof(Mission.SpawnAgent))]
[HarmonyPatchCategory("Patch23_BannerColorPersistence")]
public static class Mission_SpawnAgent_Patch
{
    private static IBannerColorService? _service;
    private static IBannerHeroAdapter? _heroAdapter;

    public static void Initialize(IBannerColorService service, IBannerHeroAdapter heroAdapter)
    {
        _service = service;
        _heroAdapter = heroAdapter;
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

    [HarmonyPrefix]
    public static bool Prefix(AgentBuildData agentBuildData)
    {
        if (!(_service?.IsEnabled() ?? false)) return true;
        if (agentBuildData.AgentOverridenSpawnEquipment != null) return true;

        var info = ResolveColors(agentBuildData);
        if (info == null) return true;

        agentBuildData.ClothingColor1(info.Value.Color1).ClothingColor2(info.Value.Color2);
        return true;
    }
}
