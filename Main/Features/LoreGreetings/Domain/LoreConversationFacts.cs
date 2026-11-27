using System.Collections.Generic;
using TAOM.Features.Execution;

namespace TAOM.Features.LoreGreetings.Domain;

/// <summary>
/// The conversation's speaker as plain facts: his ids (hero id first, then character ids), gender, whether he has met the player, is
/// a prisoner or rides in the player's party, his side, whether he is a wanderer, and whether vanilla gives him lord conversations
/// (<c>LordConversationsCampaignBehavior.UsesLordConversations</c>).
/// </summary>
internal sealed record LoreSpeakerFacts(IReadOnlyList<string> Ids, bool IsFemale, bool HasMet, bool IsPrisoner, bool InPlayerParty, SideIds Side,
    bool IsWanderer = false, bool UsesLordConversations = true);

/// <summary>One who may be greeted: the player, or a companion in the player's party. At war means the speaker's realm is at war with the listener's.</summary>
internal sealed record LoreListenerFacts(IReadOnlyList<string> Ids, bool IsFemale, bool IsPlayer, bool AtWarWithSpeaker, SideIds Side);

/// <summary>
/// The speaker and the listeners in the order they are tried (the player first, then the companions in roster order), and the
/// conversation's state that keeps vanilla's first meeting: a captive context (<c>CapturedLord</c>, <c>FreeOrCapturePrisonerHero</c>), a
/// lord about to attack (<c>HeroHelper.WillLordAttack</c>), or the player in a siege.
/// </summary>
internal sealed record LoreConversationFacts(LoreSpeakerFacts Speaker, IReadOnlyList<LoreListenerFacts> Listeners,
    bool CaptiveContext = false, bool LordWillAttack = false, bool PlayerInSiege = false);

/// <summary>The line to show and its localization key.</summary>
internal sealed record LoreGreetingChoice(string Key, string Text);

/// <summary>Which of a pair's three lines was taken; each has its own key suffix.</summary>
internal enum LoreGreetingVariant
{
    Text,
    Hostile,
    First,
}
