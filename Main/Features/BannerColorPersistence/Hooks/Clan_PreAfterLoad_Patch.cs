using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace TAOM.Features.BannerColorPersistence.Hooks;

/// <summary>
/// Patch24: brackets <c>Clan.PreAfterLoad</c> so <see cref="Clan_UpdateBannerColorsAccordingToKingdom_Patch"/> can tell
/// the load repaint from every other one. Installed v1.5.7 <c>Clan.PreAfterLoad</c> ends with
/// <c>UpdateBannerColorsAccordingToKingdom()</c>, which repaints the clan's banner (the kingdom's banner for a ruling
/// clan) in place from <c>Kingdom.PrimaryBannerColor</c> and <c>SecondaryBannerColor</c>. The patch targets the
/// override declared on <c>Clan</c>, not the base virtual, so its finalizer runs for every clan.
/// <para>
/// The two methods are one call chain, so they share a <c>[ThreadStatic]</c> flag cleared in a finalizer. The prefix
/// opens the flag only when it was closed and records that in Harmony's <c>__state</c>; the void finalizer closes it
/// only for the call that opened it, so a nested call or a finalizer whose prefix was stripped leaves an open bracket
/// alone, and a throw still closes it. Order against other prefixes does not matter: the flag is read only by the
/// original body. BannerlordCoop also prefixes this method (<c>ClanBannerLoadRepairPatch</c>, a void prefix that
/// writes a default banner only where the saved one has no layers).
/// </para>
/// </summary>
[HarmonyPatch(typeof(Clan), "PreAfterLoad")]
[HarmonyPatchCategory("Patch24_BannerDriftGuard")]
public static class Clan_PreAfterLoad_Patch
{
    [ThreadStatic] private static bool _inClanLoad;

    internal static bool InClanLoad => _inClanLoad;

    [HarmonyPrefix]
    public static void Prefix(out bool __state)
    {
        __state = !_inClanLoad;
        _inClanLoad = true;
    }

    [HarmonyFinalizer]
    public static void Finalizer(bool __state)
    {
        if (__state)
            _inClanLoad = false;
    }
}
