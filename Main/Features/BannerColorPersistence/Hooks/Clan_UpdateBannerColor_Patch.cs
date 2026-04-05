using HarmonyLib;
using TAOM.Adapters;
using TaleWorlds.CampaignSystem;

namespace TAOM.Features.BannerColorPersistence.Hooks;

[HarmonyPatch(typeof(Clan), nameof(Clan.UpdateBannerColor))]
[HarmonyPatchCategory("Patch24_BannerDriftGuard")]
public static class Clan_UpdateBannerColor_Patch
{
    private static IBannerColorService? _service;
    private static IBannerHeroAdapter? _heroAdapter;

    public static void Initialize(IBannerColorService? service, IBannerHeroAdapter? heroAdapter)
    {
        _service = service;
        _heroAdapter = heroAdapter;
    }

    [HarmonyPostfix]
    public static void Postfix(Clan? __instance)
    {
        if (!(_service?.IsDriftGuardEnabled() ?? false)) return;
        if (__instance == null) return;
        if (__instance.Kingdom?.RulingClan != __instance) return;
        _heroAdapter?.SyncKingdomColors(__instance);
    }
}
