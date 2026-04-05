using HarmonyLib;
using TAOM.Adapters;
using TaleWorlds.CampaignSystem;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.BannerColorPersistence.Hooks;

[HarmonyPatch(typeof(Agent), nameof(Agent.EquipItemsFromSpawnEquipment))]
[HarmonyPatchCategory("Patch23_BannerColorPersistence")]
public static class Agent_EquipItemsFromSpawnEquipment_Patch
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
    public static void Prefix(Agent __instance)
    {
        if (!(_service?.IsAgentVisualColorsEnabled() ?? false)) return;

        var character = __instance.Character as CharacterObject;
        if (character == null) return;

        var info = _heroAdapter?.GetClanColorInfo(character);
        if (info == null) return;
        if (!(_service?.ShouldUseClanColor(info.Value) ?? false)) return;

        _colorStore?.Register(__instance.Index, info.Value);
    }
}
