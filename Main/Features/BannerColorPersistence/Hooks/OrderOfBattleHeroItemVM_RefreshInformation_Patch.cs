using HarmonyLib;
using TAOM.Adapters;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.ImageIdentifiers;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.ViewModelCollection.OrderOfBattle;

namespace TAOM.Features.BannerColorPersistence.Hooks;

[HarmonyPatch(typeof(OrderOfBattleHeroItemVM), nameof(OrderOfBattleHeroItemVM.RefreshInformation))]
[HarmonyPatchCategory("Patch23_BannerColorPersistence")]
public static class OrderOfBattleHeroItemVM_RefreshInformation_Patch
{
    private static IBannerColorService? _service;
    private static IBannerHeroAdapter? _heroAdapter;

    public static void Initialize(IBannerColorService service, IBannerHeroAdapter heroAdapter)
    {
        _service = service;
        _heroAdapter = heroAdapter;
    }

    [HarmonyPostfix]
    public static void Postfix(OrderOfBattleHeroItemVM __instance)
    {
        if (!(_service?.IsEnabled() ?? false)) return;
        if (__instance.Agent == null) return;

        var character = __instance.Agent.Character as CharacterObject;
        if (character == null) return;

        var info = _heroAdapter?.GetClanColorInfo(character);
        if (info == null || !(_service?.ShouldUseClanColor(info.Value) ?? false)) return;

        // RefreshInformation creates CharacterCode.CreateFrom(Agent.Character) — bypass CampaignUIHelper
        // Rebuild the ImageIdentifier with correct clan colors
        var code = CharacterCode.CreateFrom(__instance.Agent.Character);
        code.Color1 = info.Value.Color1;
        code.Color2 = info.Value.Color2;
        __instance.ImageIdentifier = new CharacterImageIdentifierVM(code);
    }
}
