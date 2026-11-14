using TAOM.Features.EncounterLines.Models;

namespace TAOM.Adapters;

/// <summary>
/// What Encounter Lines (Patch149) reads from the campaign and the conversation, and the text variables it writes. Every
/// answer is a plain value: an unreadable fact is null, empty or false, never a throw out of a missing campaign.
/// </summary>
public interface IEncounterLinesAdapter
{
    /// <summary>The player's culture, race and side; null with no campaign or no main hero.</summary>
    PlayerFacts? Player();

    /// <summary>The caravan or villager party of the open conversation; null when there is none.</summary>
    PartyFacts? ConversationParty();

    /// <summary>True when vanilla's caravan behavior has no record of the conversation party yet; false when it has one or the record cannot be read.</summary>
    bool IsFirstCaravanMeeting();

    /// <summary>True when vanilla's villager behavior has no record of the encountered party yet; false when it has one or the record cannot be read.</summary>
    bool IsFirstVillagerMeeting();

    /// <summary>The map faction id of the encountered party (a bandit clan, or a hideout's clan); null with no encounter.</summary>
    string? EncounteredFactionId { get; }

    /// <summary>
    /// The clan of the character speaking in the open one-to-one conversation: the hero's clan, else the conversation
    /// party's clan, else the clan of the party the speaking agent was spawned from (the hideout boss). Null when none reads.
    /// </summary>
    string? HideoutBossClanId { get; }

    /// <summary>True when the encountered party is on the water.</summary>
    bool EncounteredAtSea { get; }

    /// <summary>True while the player is inside a settlement (<c>Settlement.CurrentSettlement</c>).</summary>
    bool InSettlement { get; }

    /// <summary>The campaign day of the year, 0 with no campaign.</summary>
    int DayOfYear { get; }

    /// <summary>True when the game text <paramref name="textId"/> has the variation <paramref name="variation"/>.</summary>
    bool HasText(string textId, string variation);

    /// <summary>
    /// Sets the conversation text variable <paramref name="variable"/> to the game text <paramref name="textId"/> under
    /// <paramref name="variation"/>; false, with nothing set, when that row does not exist.
    /// </summary>
    bool TrySetVariable(string variable, string textId, string variation);
}
