using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TAOM.Core.Logging;

namespace TAOM.Features.BannerColorPersistence.Hooks;

[HarmonyPatchCategory("Patch24_BannerDriftGuard")]
public static class Clan_UpdateBannerColorsAccordingToKingdom_Patch
{
    private static IBannerColorService? _service;
    private static IModLogger? _logger;

    public static void Initialize(IBannerColorService? service) => _service = service;
    public static void Initialize(IModLogger logger) => _logger = logger;

    public static MethodBase TargetMethod()
    {
        // Phase 9b #172 F3 — TaleWorlds private method. If a future patch renames the method,
        // AccessTools.Method returns null and Harmony silently skips the patch — no warning, no
        // log, drift guard goes inert. Surface the null case loudly.
        var m = AccessTools.Method(typeof(Clan), "UpdateBannerColorsAccordingToKingdom");
        if (m == null)
            _logger?.LogWarning("[BannerDriftGuard] Clan.UpdateBannerColorsAccordingToKingdom not found — drift guard inactive. TaleWorlds may have renamed the method.");
        return m;
    }

    [HarmonyPrefix]
    public static bool Prefix(Clan __instance)
    {
        // Phase 9b #172 F2 — was `Prefix() => !_service.IsDriftGuardEnabled()`. That blocked the
        // method for ALL clans, including NPC clans changing kingdoms mid-campaign whose banner
        // colors then never synced to the new kingdom palette. The DriftGuard is specifically
        // designed to protect the PLAYER clan's banner from forced sync; NPC clans should be
        // allowed to follow vanilla behavior.
        if (!(_service?.IsDriftGuardEnabled() ?? false))
            return true; // DriftGuard disabled — vanilla runs normally
        return __instance != Clan.PlayerClan; // DriftGuard enabled — block ONLY for player clan
    }
}
