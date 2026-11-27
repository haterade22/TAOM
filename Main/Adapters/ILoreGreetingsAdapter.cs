using TAOM.Features.LoreGreetings.Domain;

namespace TAOM.Adapters;

/// <summary>
/// Engine boundary for Lore Greetings (docs/features/lore-greetings.md): the open conversation as plain facts, marking its speaker met,
/// and the greeting text. Nothing here throws on a missing campaign or conversation.
/// </summary>
internal interface ILoreGreetingsAdapter
{
    /// <summary>The conversation hero, the player and the heroes in the player's party; null with no campaign, no conversation hero or no player hero.</summary>
    LoreConversationFacts? Describe();

    /// <summary>
    /// Marks the conversation hero met with the player, keeping his last meeting time as it was, so vanilla's 24-hour salutation does
    /// not take over. False when there is no conversation hero or he was already met.
    /// </summary>
    bool MeetSpeaker();

    /// <summary>Sets the met greeting's text variable to the line, under its localization key.</summary>
    void SetGreeting(string key, string text);
}
