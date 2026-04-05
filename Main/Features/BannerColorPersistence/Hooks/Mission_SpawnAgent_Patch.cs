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
    private static IAgentColorStore? _colorStore;

    public static void Initialize(IBannerColorService service, IBannerHeroAdapter heroAdapter, IAgentColorStore colorStore)
    {
        _service = service;
        _heroAdapter = heroAdapter;
        _colorStore = colorStore;
    }

    [HarmonyPrefix]
    public static bool Prefix(AgentBuildData agentBuildData)
    {
        if (!(_service?.IsEnabled() ?? false)) return true;
        if (agentBuildData.AgentOverridenSpawnEquipment != null) return true;

        var origin = agentBuildData.AgentOrigin;
        if (origin == null) return true;

        TaleWorlds.CampaignSystem.Hero? leaderHero = null;

        if (origin is PartyAgentOrigin partyOrigin)
            leaderHero = partyOrigin.Party?.LeaderHero;
        else if (origin is PartyGroupAgentOrigin partyGroupOrigin)
            leaderHero = partyGroupOrigin.Party?.LeaderHero;
        else if (origin is SimpleAgentOrigin simpleOrigin)
            leaderHero = simpleOrigin.Party?.LeaderHero;

        if (leaderHero == null) return true;

        var info = _heroAdapter?.GetClanColorInfoFromHero(leaderHero);
        if (info == null) return true;
        if (!(_service?.ShouldUseClanColor(info.Value) ?? false)) return true;

        agentBuildData.ClothingColor1(info.Value.Color1).ClothingColor2(info.Value.Color2);
        return true;
    }

    [HarmonyPostfix]
    public static void Postfix(Agent __result, AgentBuildData agentBuildData)
    {
        if (__result == null) return;
        if (!(_service?.IsAgentVisualColorsEnabled() ?? false)) return;

        var origin = agentBuildData.AgentOrigin;
        if (origin == null) return;

        TaleWorlds.CampaignSystem.Hero? leaderHero = null;

        if (origin is PartyAgentOrigin partyOrigin)
            leaderHero = partyOrigin.Party?.LeaderHero;
        else if (origin is PartyGroupAgentOrigin partyGroupOrigin)
            leaderHero = partyGroupOrigin.Party?.LeaderHero;
        else if (origin is SimpleAgentOrigin simpleOrigin)
            leaderHero = simpleOrigin.Party?.LeaderHero;

        if (leaderHero == null) return;

        var info = _heroAdapter?.GetClanColorInfoFromHero(leaderHero);
        if (info == null) return;
        if (!(_service?.ShouldUseClanColor(info.Value) ?? false)) return;

        _colorStore?.Register(__result.Index, info.Value);
    }
}
