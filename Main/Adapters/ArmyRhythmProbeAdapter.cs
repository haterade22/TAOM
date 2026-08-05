using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TAOM.Core.Logging;

namespace TAOM.Adapters;

public sealed class ArmyRhythmProbeAdapter : IArmyRhythmProbeAdapter
{
    private const float EnemyScanRadius = 8f;
    private const int LowTierCeiling = 2;

    private readonly IModLogger _logger;

    public ArmyRhythmProbeAdapter(IModLogger logger)
    {
        _logger = logger;
    }

    public ArmyRhythmProbe Probe(string commanderHeroId)
    {
        var probe = new ArmyRhythmProbe();
        try
        {
            var hero = string.IsNullOrEmpty(commanderHeroId)
                ? null
                : Campaign.Current?.CampaignObjectManager?.Find<Hero>(commanderHeroId);
            var party = hero?.PartyBelongedTo;
            if (party == null)
                return probe;

            probe.CommanderResolved = true;
            probe.InArmy = party.Army != null;
            probe.InSiege = party.SiegeEvent != null;
            probe.AtSea = party.IsCurrentlyAtSea;
            probe.Blockade = probe.InSiege && probe.AtSea;
            probe.InSettlement = party.CurrentSettlement != null;
            probe.Moving = !probe.InSettlement && party.DefaultBehavior != AiBehavior.Hold;
            probe.FoodAmount = (int)Math.Max(0f, party.Food);
            var foodDays = (float)party.GetNumDaysForFoodToLast();
            probe.DaysOfFoodLeft = float.IsNaN(foodDays) || float.IsInfinity(foodDays) ? 0f : Math.Max(0f, foodDays);
            probe.TroopCount = party.MemberRoster?.TotalManCount ?? 0;
            probe.WoundedCount = party.MemberRoster?.TotalWounded ?? 0;
            probe.LowTierTroopCount = CountLowTierTroops(party);
            probe.EnemyPartyNearby = HasEnemyNearby(party);
            probe.FactionAtWar = IsFactionAtWar(hero);
            return probe;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[Enlistment] ArmyRhythmProbeAdapter.Probe('{commanderHeroId}') failed: {ex.Message}");
            return probe;
        }
    }

    private static int CountLowTierTroops(MobileParty party)
    {
        var roster = party.MemberRoster;
        if (roster == null)
            return 0;
        var count = 0;
        for (var i = 0; i < roster.Count; i++)
        {
            var element = roster.GetElementCopyAtIndex(i);
            if (element.Character != null && !element.Character.IsHero && element.Character.Tier <= LowTierCeiling)
                count += element.Number;
        }
        return count;
    }

    private static bool HasEnemyNearby(MobileParty party)
    {
        var faction = party.MapFaction;
        if (faction == null)
            return false;

        // Engine locator-grid iterator — bounded radius, no global scan.
        var data = MobileParty.StartFindingLocatablesAroundPosition(party.Position.ToVec2(), EnemyScanRadius);
        for (var other = MobileParty.FindNextLocatable(ref data); other != null; other = MobileParty.FindNextLocatable(ref data))
        {
            if (other == party || !other.IsActive || other.MapFaction == null)
                continue;
            if (faction.IsAtWarWith(other.MapFaction))
                return true;
        }
        return false;
    }

    private static bool IsFactionAtWar(Hero hero)
    {
        var faction = hero?.Clan?.MapFaction;
        if (faction == null)
            return false;
        foreach (var kingdom in Kingdom.All)
        {
            if (kingdom != faction && faction.IsAtWarWith(kingdom))
                return true;
        }
        return false;
    }
}
