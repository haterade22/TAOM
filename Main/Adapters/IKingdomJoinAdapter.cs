namespace TAOM.Adapters;

/// <summary>
/// The kingdom-join offer made after an adoption. Narrow on purpose: one question and one action.
/// </summary>
public interface IKingdomJoinAdapter
{
    /// <summary>
    /// The id of a kingdom the player's clan could join, or empty when there is none worth
    /// offering. Empty covers every ordinary case: the clan already has a kingdom, the culture
    /// fields no kingdom, or the player already rules one.
    /// </summary>
    string FindJoinableKingdomForPlayerCulture();

    /// <summary>Display name of a kingdom, for the prompt.</summary>
    string GetKingdomName(string kingdomId);

    /// <summary>Puts the player's clan into the kingdom.</summary>
    void JoinPlayerClanToKingdom(string kingdomId);
}
