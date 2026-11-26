using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Incidents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TAOM.Features.EncounterLines.Models;
using TAOM.Features.IncidentVoices;
using TAOM.Features.IncidentVoices.Models;

namespace TAOM.Adapters;

/// <summary>
/// Incident Voices' window on the campaign (Patch176). Every read starts from <c>Campaign.Current</c>, so no call throws
/// outside a campaign, and nothing engine-owned is kept between calls. The player's people come from
/// <see cref="IEncounterLinesAdapter.Player"/> (culture and race of the main hero, side of the clan's realm).
/// </summary>
public sealed class IncidentVoiceAdapter : IIncidentVoiceAdapter
{
    private readonly IEncounterLinesAdapter _player;

    public IncidentVoiceAdapter(IEncounterLinesAdapter player) => _player = player;

    public PlayerFacts? Player() => _player.Player();

    public string? Text(string variation) =>
        GameTexts.TryGetText(IncidentTextKeys.GameTextId, out var text, variation) ? text?.Value : null;

    public long IncidentSeed => Campaign.Current?.IncidentManager?.ActiveIncidentSeed ?? 0;

    public IReadOnlyList<PlaceFacts> Places(string kind)
    {
        if (Campaign.Current == null)
            return Array.Empty<PlaceFacts>();

        var here = MobileParty.MainParty?.CurrentSettlement;
        IEnumerable<Settlement> settlements = kind == "vineyard"
            ? Village.All.Where(v => v.VillageType == DefaultVillageTypes.VineYard).Select(v => v.Settlement)
            : Town.AllTowns.Select(t => t.Settlement);
        return settlements
            .Where(s => s != null && s != here)
            .Select(s => new PlaceFacts(s.Name?.ToString() ?? string.Empty, s.MapFaction != null ? FactionSides.Of(s.MapFaction) : FactionSides.OfCulture(s.Culture), s.Culture?.StringId ?? string.Empty))
            .Where(p => p.Name.Length > 0)
            .ToList();
    }

    public string Queue(string incidentId, string? closedReason)
    {
        if (Campaign.Current == null || Game.Current == null)
            return "No campaign is running.";
        var incident = Game.Current.ObjectManager.GetObject<Incident>(incidentId);
        if (incident == null)
            return "The game has no incident '" + incidentId + "'.";
        if (!(Game.Current.GameStateManager.ActiveState is MapState map))
            return "Go back to the campaign map first (the game must be showing the map state).";
        if (closedReason != null)
            return incidentId + " is closed for you: " + closedReason;
        if (!incident.CanIncidentBeInvoked())
            return incidentId + " is open for your people, but vanilla's own condition for it is false here and now (a town, a battle, a fief or a skill it needs).";

        map.NextIncident = incident;
        return incidentId + " is queued and shows at the next campaign tick.";
    }
}
