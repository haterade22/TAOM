using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TAOM.Adapters;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.ImageIdentifiers;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.ViewModelCollection.OrderOfBattle;

namespace TAOM.Features.BannerColorPersistence.Hooks;

[HarmonyPatch]
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

    // OrderOfBattleVM builds every hero with `new OrderOfBattleHeroItemVM(Agent)`, whose constructor
    // sets ImageIdentifier itself and never calls RefreshInformation, so both are targets (#993).
    public static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(OrderOfBattleHeroItemVM), nameof(OrderOfBattleHeroItemVM.RefreshInformation));
        yield return AccessTools.Constructor(typeof(OrderOfBattleHeroItemVM), new[] { typeof(Agent) });
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

        // Both targets build CharacterCode.CreateFrom(Agent.Character), bypassing CampaignUIHelper:
        // rebuild the ImageIdentifier with the clan colors
        var code = ClanColoredCharacterCode.Recolor(
            CharacterCode.CreateFrom(__instance.Agent.Character), info.Value.Color1, info.Value.Color2);
        __instance.ImageIdentifier = new CharacterImageIdentifierVM(code);
    }
}
