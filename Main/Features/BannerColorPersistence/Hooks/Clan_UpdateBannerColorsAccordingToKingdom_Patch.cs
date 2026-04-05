using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace TAOM.Features.BannerColorPersistence.Hooks;

[HarmonyPatchCategory("Patch24_BannerDriftGuard")]
public static class Clan_UpdateBannerColorsAccordingToKingdom_Patch
{
    private static IBannerColorService? _service;

    public static void Initialize(IBannerColorService? service) => _service = service;

    public static MethodBase TargetMethod() =>
        AccessTools.Method(typeof(Clan), "UpdateBannerColorsAccordingToKingdom");

    [HarmonyPrefix]
    public static bool Prefix() => !(_service?.IsDriftGuardEnabled() ?? true);
}
