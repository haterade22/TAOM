using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

namespace TAOM.Features.SpecialResources;

/// <summary>
/// Reads the two inputs of the daily breakdown off engine objects, once, for every caller that needs
/// them: the daily tick, the map-bar tooltip and the console dump. The map-bar tooltip used to build
/// its own EMPTY troop list here, which is why it never showed an upkeep line (#558).
///
/// A dumb collector on purpose: it returns every troop that has a cost row at all and lets the
/// service decide which of those carry upkeep, so that rule lives where it is unit-tested. Engine
/// types stop at this boundary (ADR-007).
/// </summary>
internal static class PartyUpkeepReader
{
    private static readonly IReadOnlyList<TroopUpkeepInfo> None = new List<TroopUpkeepInfo>();

    public static IReadOnlyList<TroopUpkeepInfo> Collect(MobileParty party, ISpecialResourceConfigProvider config)
    {
        if (party?.MemberRoster == null || config == null) return None;

        var result = new List<TroopUpkeepInfo>(8);
        foreach (var element in party.MemberRoster.GetTroopRoster())
        {
            if (element.Character != null && config.GetTroopCost(element.Character.StringId) != null)
                result.Add(new TroopUpkeepInfo(element.Character.StringId, element.Number));
        }

        return result;
    }

    /// <summary>
    /// The roster GetTotalWage was handed (the party's MemberRoster, or the desertion model's
    /// troopsToDesert) as upkeep inputs, one element at a time and only while the caller walks it
    /// (#1109): the wage model hands this to the gold-mode surcharge, which does not enumerate in
    /// resource mode. Heroes carry no cost row and are skipped.
    /// </summary>
    public static IEnumerable<TroopUpkeepInfo> Enumerate(TroopRoster roster)
    {
        if (roster == null) yield break;

        foreach (var element in roster.GetTroopRoster())
        {
            if (element.Character != null && !element.Character.IsHero)
                yield return new TroopUpkeepInfo(element.Character.StringId, element.Number);
        }
    }

    public static void GetHeroIds(Hero hero, out string kingdomId, out string cultureId)
    {
        kingdomId = hero?.Clan?.Kingdom?.StringId;
        cultureId = hero?.Culture?.StringId;
    }

    public static int CountOwnedTowns(Hero hero)
    {
        var settlements = hero?.Clan?.Settlements;
        if (settlements == null) return 0;

        var count = 0;
        foreach (var settlement in settlements)
            if (settlement.IsTown)
                count++;
        return count;
    }
}
