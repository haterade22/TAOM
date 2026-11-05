using System.Collections.Generic;

namespace TAOM.Features.BanditManagement;

/// <summary>
/// The two pairing checks behind Patch125 (#1063). A bandit clan and the hideouts of its culture ship in different
/// modules (the clan in TAOM, the dens in TAOM_Map), so either can be present without the other:
///
/// - a clan whose culture owns no hideout, when TAOM runs with an older TAOM_Map. Vanilla
///   <c>BanditSpawnCampaignBehavior.GetInfestedHideoutCount</c> indexes its hideout dictionary by the clan's culture
///   without a guard and throws <c>KeyNotFoundException</c> on a new game.
/// - a hideout whose culture has no clan, in a save made before the clan existed: the save loads the XML settlements
///   but not the XML clans. Vanilla <c>AddBanditToHideout</c> then dereferences a null clan.
///
/// Generic so the tests need no engine types: the patch passes the engine's own dictionary and culture objects.
/// </summary>
public static class HideoutClanGuard
{
    /// <summary>True when the hideout dictionary holds an entry for this culture, so vanilla's indexer is safe.</summary>
    public static bool HasHideoutList<TCulture, TList>(IDictionary<TCulture, TList> hideouts, TCulture culture)
        where TCulture : class =>
        hideouts != null && culture != null && hideouts.ContainsKey(culture);

    /// <summary>True when some bandit clan carries this culture, compared by reference as vanilla does.</summary>
    public static bool HasClanOfCulture<TCulture>(IEnumerable<TCulture> clanCultures, TCulture culture)
        where TCulture : class
    {
        if (clanCultures == null || culture == null) return false;
        foreach (var clanCulture in clanCultures)
            if (ReferenceEquals(clanCulture, culture)) return true;
        return false;
    }
}
