using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TAOM.Core.Logging;

namespace TAOM.Adapters;

public sealed class ArmyRhythmProbeAdapter : IArmyRhythmProbeAdapter
{
    private const float EnemyScanRadius = 8f;

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

            probe.InArmy = party.Army != null;
            probe.InSiege = party.SiegeEvent != null;
            probe.AtSea = party.IsCurrentlyAtSea;
            probe.Blockade = probe.InSiege && probe.AtSea;
            probe.InSettlement = party.CurrentSettlement != null;
            probe.Moving = !probe.InSettlement && party.DefaultBehavior != AiBehavior.Hold;
            probe.EnemyPartyNearby = HasEnemyNearby(party);
            return probe;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[Enlistment] ArmyRhythmProbeAdapter.Probe('{commanderHeroId}') failed: {ex.Message}");
            return probe;
        }
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
}
