using System.Collections.Generic;
using TAOM.Features.EncounterLines.Models;
using TAOM.Features.IncidentVoices.Models;

namespace TAOM.Adapters;

/// <summary>
/// What Incident Voices (Patch176) reads from the campaign, and the one console action it takes. Every answer is a plain value:
/// an unreadable fact is null or empty, never a throw out of a missing campaign.
/// </summary>
public interface IIncidentVoiceAdapter
{
    /// <summary>The player's culture, race and side; null with no campaign or no main hero.</summary>
    PlayerFacts? Player();

    /// <summary>
    /// The raw text of the game-text row <c>str_taom_inc.&lt;variation&gt;</c> (<c>{=taom_inc_...}English</c>), or null when no such row
    /// exists, so a missing text leaves vanilla's.
    /// </summary>
    string? Text(string variation);

    /// <summary>The towns (<c>town</c>) or vineyard villages (<c>vineyard</c>) of the world, the player's own settlement excluded.</summary>
    IReadOnlyList<PlaceFacts> Places(string kind);

    /// <summary>The engine's seed for the incident being rolled, the same in the check and the pre-popup check; 0 with no campaign.</summary>
    long IncidentSeed { get; }

    /// <summary>
    /// Console test: queues the incident for the next campaign tick (as vanilla does) and answers one line saying what will
    /// happen. <paramref name="closedReason"/> is TAOM's reason the incident is closed, or null.
    /// </summary>
    string Queue(string incidentId, string? closedReason);
}
