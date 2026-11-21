using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TAOM.Core.Logging;
using TAOM.Features.MapEventGuard;

namespace TAOM.Adapters;

/// <inheritdoc cref="IOrphanedBattleAdapter"/>
public sealed class OrphanedBattleAdapter : IOrphanedBattleAdapter
{
    private readonly IModLogger _logger;

    public OrphanedBattleAdapter(IModLogger logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<IOrphanedBattleEventAdapter> AttachedFinalizedBattles()
    {
        var battles = new List<IOrphanedBattleEventAdapter>();
        var parties = MobileParty.All;
        if (parties == null)
            return battles;

        // The manager has dropped these events, so the parties are the only way to reach them.
        var seen = new HashSet<MapEvent>();
        var playerBattle = PlayerEncounter.Battle;
        foreach (var party in parties)
        {
            var mapEvent = party?.Party?.MapEvent;
            if (mapEvent == null || !mapEvent.IsFinalized || !seen.Add(mapEvent))
                continue;
            if (mapEvent.IsPlayerMapEvent || mapEvent == playerBattle)
                continue;
            battles.Add(new OrphanedBattleEventAdapter(mapEvent, _logger));
        }
        return battles;
    }

    public IReadOnlyList<IStrandedPartyAdapter> StrandedParties()
    {
        var stranded = new List<IStrandedPartyAdapter>();
        var parties = MobileParty.All;
        if (parties == null)
            return stranded;

        var mainParty = Campaign.Current?.MainParty;
        var playerBattle = PlayerEncounter.Battle;
        foreach (var party in parties)
        {
            var side = party?.Party?.MapEventSide;
            if (party == null || side == null)
                continue;
            var mapEvent = side.MapEvent;
            bool inPlayerBattle = mapEvent != null && (mapEvent.IsPlayerMapEvent || mapEvent == playerBattle);
            bool sideListsIt = side.Parties.Any(p => p.Party == party.Party);
            if (OrphanedBattleService.IsStranded(true, sideListsIt, party == mainParty, inPlayerBattle))
                stranded.Add(new StrandedPartyAdapter(party, _logger));
        }
        return stranded;
    }
}
