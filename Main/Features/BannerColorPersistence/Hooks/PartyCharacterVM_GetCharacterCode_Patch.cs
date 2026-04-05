using System.Reflection;
using HarmonyLib;
using TAOM.Adapters;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Party;
using TaleWorlds.Core;

namespace TAOM.Features.BannerColorPersistence.Hooks;

[HarmonyPatchCategory("Patch23_BannerColorPersistence")]
public static class PartyCharacterVM_GetCharacterCode_Patch
{
    private static IBannerColorService? _service;
    private static IBannerHeroAdapter? _heroAdapter;

    public static void Initialize(IBannerColorService service, IBannerHeroAdapter heroAdapter)
    {
        _service = service;
        _heroAdapter = heroAdapter;
    }

    public static MethodBase TargetMethod() =>
        AccessTools.Method(typeof(PartyCharacterVM), "GetCharacterCode",
            new[]
            {
                typeof(CharacterObject),
                typeof(TaleWorlds.CampaignSystem.Party.PartyScreenLogic.TroopType),
                typeof(TaleWorlds.CampaignSystem.Party.PartyScreenLogic.PartyRosterSide)
            });

    [HarmonyPostfix]
    public static void Postfix(PartyCharacterVM __instance, ref CharacterCode __result)
    {
        if (__result == null) return;
        if (!(_service?.IsEnabled() ?? false)) return;
        if (__instance.IsPrisoner) return;
        if ((int)__instance.Side != 1) return;

        var clan = Clan.PlayerClan;
        if (clan == null) return;

        __result.Color1 = clan.Color;
        __result.Color2 = clan.Color2;
    }
}
