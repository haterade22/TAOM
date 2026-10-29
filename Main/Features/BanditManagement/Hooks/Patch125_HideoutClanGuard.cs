using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TAOM.Core.Logging;

namespace TAOM.Features.BanditManagement.Hooks;

/// <summary>
/// Patch125 — keeps bandit spawning alive when a bandit clan and the hideouts of its culture do not arrive together
/// (#1063). See <see cref="HideoutClanGuard"/> for the two states and why they happen.
///
/// - <see cref="InfestedCountPrefix"/> on the private <c>GetInfestedHideoutCount(Clan)</c>: a clan whose culture
///   owns no hideout counts 0 infested hideouts, so it spawns nothing and fills no den, instead of throwing out of
///   new-game stage 11 (<c>SpawnBanditsAroundHideoutAtNewGame</c>) and the hourly clan tick.
/// - <see cref="AddBanditPrefix"/> on <c>AddBanditToHideout</c>: a bandit hideout whose culture has no clan gets no
///   party and the method returns null, the value vanilla already returns for a non-bandit hideout. The callers that
///   can reach such a den (the new-hideout fill and the Gold Rush incident's <c>InfestNearbyHideout</c>) ignore the
///   result; the one caller that dereferences it (the new-game boss party) only runs for a den a clan just took.
///
/// Each state is reported once per culture: the fix is the matching TAOM_Map, and the log is how a player finds that.
/// </summary>
[HarmonyPatch]
[HarmonyPatchCategory("Patch125_HideoutClanGuard")]
public static class Patch125_HideoutClanGuard
{
    private static IModLogger _logger;
    private static readonly HashSet<string> _reported = new HashSet<string>();

    [HarmonyPatch(typeof(BanditSpawnCampaignBehavior), "GetInfestedHideoutCount")]
    [HarmonyPrefix]
    public static bool InfestedCountPrefix(Clan banditFaction, Dictionary<CultureObject, List<Hideout>> ____hideouts, ref int __result)
    {
        if (HideoutClanGuard.HasHideoutList(____hideouts, banditFaction?.Culture)) return true;

        __result = 0;
        Report("clan:" + banditFaction?.StringId,
            $"Patch125: bandit clan '{banditFaction?.StringId}' has no hideout of its culture " +
            $"'{banditFaction?.Culture?.StringId}' on this map, so it raids nothing. Install the TAOM_Map that matches " +
            "this TAOM (docs/features/bandit-management.md).");
        return false;
    }

    [HarmonyPatch(typeof(BanditSpawnCampaignBehavior), nameof(BanditSpawnCampaignBehavior.AddBanditToHideout))]
    [HarmonyPrefix]
    public static bool AddBanditPrefix(Hideout hideoutComponent, ref MobileParty __result)
    {
        var culture = hideoutComponent?.Owner?.Settlement?.Culture;
        if (culture == null || !culture.IsBandit) return true;
        if (HideoutClanGuard.HasClanOfCulture(Clan.BanditFactions.Select(c => c.Culture), culture)) return true;

        __result = null;
        Report("hideout:" + culture.StringId,
            $"Patch125: hideout '{hideoutComponent.Owner.Settlement.StringId}' has culture '{culture.StringId}' but no " +
            "bandit clan carries it (a save made before that clan existed), so it stays empty.");
        return false;
    }

    private static void Report(string key, string message)
    {
        try
        {
            if (!_reported.Add(key)) return;
            _logger ??= TAOM.IoC.Resolve<IModLogger>();
            _logger?.LogWarning(message);
        }
        catch
        {
            // Reporting must never bring back the crash the guard just prevented.
        }
    }
}
