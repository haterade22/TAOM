using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TAOM.Core.Logging;

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
}
