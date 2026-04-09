using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TAOM.Core.Logging;

namespace TAOM.Features.SpecialResources;

internal static class SpecialResourcesBehaviorHelpers
{
    public static void GetHeroIds(Hero hero, out string kingdomId, out string cultureId)
    {
        kingdomId = hero?.Clan?.Kingdom?.StringId;
        cultureId = hero?.Culture?.StringId;
    }

    public static int CountOwnedTowns(Hero hero)
    {
        var settlements = hero.Clan?.Settlements;
        if (settlements == null) return 0;

        var count = 0;
        foreach (var settlement in settlements)
            if (settlement.IsTown)
                count++;
        return count;
    }

    public static void FillTroopUpkeep(
        MobileParty party, ISpecialResourceConfigProvider config, List<TroopUpkeepInfo> buffer)
    {
        if (party?.MemberRoster == null) return;

        foreach (var element in party.MemberRoster.GetTroopRoster())
        {
            if (element.Character != null && config.GetTroopCost(element.Character.StringId) != null)
                buffer.Add(new TroopUpkeepInfo(element.Character.StringId, element.Number));
        }
    }

    public static int ApplyDesertion(
        MobileParty party, IReadOnlyList<TroopDesertionEntry> desertions, IModLogger logger)
    {
        if (party?.MemberRoster == null || desertions == null || desertions.Count == 0)
            return 0;

        var totalDeserted = 0;
        foreach (var entry in desertions)
        {
            var character = CharacterObject.Find(entry.TroopId);
            if (character == null) continue;

            var index = party.MemberRoster.FindIndexOfTroop(character);
            if (index < 0) continue;

            var currentCount = party.MemberRoster.GetElementNumber(index);
            var toRemove = System.Math.Min(entry.DesertCount, currentCount);
            if (toRemove <= 0) continue;

            party.MemberRoster.AddToCounts(character, -toRemove);
            totalDeserted += toRemove;
            logger.LogInfo($"[SpecRes] Deserted: {entry.TroopId} x{toRemove}");
        }

        return totalDeserted;
    }
}
