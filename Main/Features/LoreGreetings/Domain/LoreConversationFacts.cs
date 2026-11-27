using System.Collections.Generic;
using TAOM.Features.Execution;

namespace TAOM.Features.LoreGreetings.Domain;

/// <summary>The conversation's speaker as plain facts: his ids (hero id first, then character ids), gender, whether he has met the player, is a prisoner or rides in the player's party, and his side.</summary>
internal sealed record LoreSpeakerFacts(IReadOnlyList<string> Ids, bool IsFemale, bool HasMet, bool IsPrisoner, bool InPlayerParty, SideIds Side);

/// <summary>One who may be greeted: the player, or a companion in the player's party. At war means the speaker's realm is at war with the listener's.</summary>
internal sealed record LoreListenerFacts(IReadOnlyList<string> Ids, bool IsFemale, bool IsPlayer, bool AtWarWithSpeaker, SideIds Side);

/// <summary>The speaker and the listeners in the order they are tried: the player first, then the companions in roster order.</summary>
internal sealed record LoreConversationFacts(LoreSpeakerFacts Speaker, IReadOnlyList<LoreListenerFacts> Listeners);

/// <summary>The line to show and its localization key.</summary>
internal sealed record LoreGreetingChoice(string Key, string Text);

/// <summary>Which of a pair's three lines was taken; each has its own key suffix.</summary>
internal enum LoreGreetingVariant
{
    Text,
    Hostile,
    First,
}
