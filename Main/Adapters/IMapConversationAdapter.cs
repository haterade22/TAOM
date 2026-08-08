namespace TAOM.Adapters;

/// <summary>
/// Opening a map conversation with a hero, behind ADR-007 so services never touch
/// <c>ConversationManager</c> / <c>CampaignMapConversation</c> / <c>MapState</c> directly.
///
/// The adapter carries the game-state guard ITSELF rather than leaving it to callers: the engine's
/// map-conversation entry point casts the active game state to <c>MapState</c> and dereferences the
/// result, so calling it from anywhere that is not the campaign map is an immediate NRE. One guard
/// in one place is the only version of that check that cannot be forgotten at a new call site.
/// </summary>
public interface IMapConversationAdapter
{
    /// <summary>
    /// True when a map conversation could be opened right now — campaign map is the active state
    /// and no conversation is already running.
    /// </summary>
    bool CanOpenConversation { get; }

    /// <summary>
    /// Open a conversation with the given hero. Returns false (without throwing) when the hero is
    /// unresolvable, has no party, or the game state will not accept a map conversation.
    /// </summary>
    bool OpenWithHero(string heroId);
}
