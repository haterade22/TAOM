using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;
using TAOM.Features.LoreGreetings.Domain;

namespace TAOM.Adapters;

/// <summary>
/// Reads the open conversation for Lore Greetings. The speaker is the conversation manager's one-to-one hero (what <c>Hero.OneToOneConversationHero</c>
/// reads without its null guard); the listeners are the player's
/// hero and then every other hero in the main party's member roster, read by index. A hero answers to his hero id, his character id and,
/// for a hero made from a template, the template's id.
/// </summary>
internal sealed class LoreGreetingsAdapter : ILoreGreetingsAdapter
{
    /// <summary>The variable vanilla's <c>start_default</c> line shows (<c>{=!}{VOICED_LINE}</c>), set by <c>conversations_set_voiced_line</c>.</summary>
    internal const string GreetingVariable = "VOICED_LINE";

    public LoreConversationFacts? Describe()
    {
        var campaign = Campaign.Current;
        var speaker = campaign?.ConversationManager?.OneToOneConversationHero;
        var player = CharacterObject.PlayerCharacter?.HeroObject;
        if (campaign == null || speaker == null || player == null)
            return null;

        var mainParty = campaign.MainParty;
        var listeners = new List<LoreListenerFacts> { Listener(speaker, player, isPlayer: true) };
        var roster = mainParty?.MemberRoster;
        if (roster != null)
        {
            for (var i = 0; i < roster.Count; i++)
            {
                var hero = roster.GetCharacterAtIndex(i)?.HeroObject;
                if (hero != null && hero != player && hero != speaker)
                    listeners.Add(Listener(speaker, hero, isPlayer: false));
            }
        }

        var facts = new LoreSpeakerFacts(Ids(speaker), speaker.IsFemale, speaker.HasMet, speaker.IsPrisoner,
            mainParty != null && speaker.PartyBelongedTo == mainParty, FactionSides.OfHero(speaker));
        return new LoreConversationFacts(facts, listeners);
    }

    public bool MeetSpeaker()
    {
        var speaker = Campaign.Current?.ConversationManager?.OneToOneConversationHero;
        if (speaker == null || speaker.HasMet)
            return false;
        var lastMeeting = speaker.LastMeetingTimeWithPlayer;
        speaker.SetHasMet();
        speaker.LastMeetingTimeWithPlayer = lastMeeting;
        return true;
    }

    public void SetGreeting(string key, string text) =>
        MBTextManager.SetTextVariable(GreetingVariable, new TextObject("{=" + key + "}" + text));

    private static LoreListenerFacts Listener(Hero speaker, Hero hero, bool isPlayer) =>
        new(Ids(hero), hero.IsFemale, isPlayer, AtWar(speaker.MapFaction, hero.MapFaction), FactionSides.OfHero(hero));

    private static bool AtWar(IFaction? a, IFaction? b) => a != null && b != null && FactionManager.IsAtWarAgainstFaction(a, b);

    private static IReadOnlyList<string> Ids(Hero hero)
    {
        var ids = new List<string>(3);
        Add(ids, hero.StringId);
        Add(ids, hero.CharacterObject?.StringId);
        Add(ids, hero.CharacterObject?.OriginalCharacter?.StringId);
        return ids;
    }

    private static void Add(List<string> ids, string? id)
    {
        if (!string.IsNullOrEmpty(id) && !ids.Contains(id!, StringComparer.Ordinal))
            ids.Add(id!);
    }
}
