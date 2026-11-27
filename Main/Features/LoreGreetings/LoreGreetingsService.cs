using System;
using System.Linq;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.Execution;
using TAOM.Features.LoreGreetings.Domain;

namespace TAOM.Features.LoreGreetings;

/// <summary>
/// Lore Greetings (docs/features/lore-greetings.md): people who know each other in the books greet each other by lore. Two entry
/// points, one per Patch177 class. <see cref="OnMeetingCheck"/> runs before vanilla decides whether a hero meets the player as a
/// stranger: a speaker who knows the player's person is marked met, so the met greeting runs instead, and that one greeting gets
/// the pair's first-meeting line. <see cref="OnGreeting"/> runs after vanilla set the met greeting and puts the pair's line in its
/// place. <see cref="Choose"/> is the whole rule.
/// </summary>
internal sealed class LoreGreetingsService
{
    internal const string KeyPrefix = "taom_loregreet_";
    internal const string GreetingCheckId = "loregreetings.greeting";
    internal const string MeetingCheckId = "loregreetings.meeting";

    private readonly ILoreGreetingsConfigProvider _config;
    private readonly IAlignmentService _alignment;
    private readonly ILoreGreetingsAdapter _adapter;
    private readonly ICoopSessionProvider _coop;
    private readonly IOutcomeLog _outcomes;

    /// <summary>
    /// The hero id the meeting prefix marked met, until the next greeting reads it. Not campaign state: it lives from one condition
    /// call to the next in the same conversation. When another start line wins over the greeting, the next meeting check for another
    /// hero clears it.
    /// </summary>
    private string? _metNow;

    public LoreGreetingsService(ILoreGreetingsConfigProvider config, IAlignmentService alignment, ILoreGreetingsAdapter adapter,
        ICoopSessionProvider coop, IOutcomeLog outcomes)
    {
        _config = config;
        _alignment = alignment;
        _adapter = adapter;
        _coop = coop;
        _outcomes = outcomes;
    }

    /// <summary>The localization key of one line: <c>taom_loregreet_&lt;speaker&gt;_&lt;listener&gt;</c>, plus <c>_hostile</c> or <c>_first</c>.</summary>
    internal static string Key(string speaker, string listener, LoreGreetingVariant variant) =>
        KeyPrefix + speaker + "_" + listener + variant switch
        {
            LoreGreetingVariant.Hostile => "_hostile",
            LoreGreetingVariant.First => "_first",
            _ => string.Empty,
        };

    /// <summary>
    /// Before vanilla's meeting check: an unmet speaker is marked met when the line his first greeting to the player would show exists.
    /// Vanilla's first meeting stays in a captive context, for a hero vanilla gives no lord conversations, for a wanderer (his own
    /// introduction), when the lord is about to attack, while the player is in a siege, for a prisoner and on a co-op client. A first
    /// meeting recorded for another hero that never reached its greeting is cleared first.
    /// </summary>
    public void OnMeetingCheck()
    {
        var facts = _adapter.Describe();
        if (facts == null)
            return;
        if (_metNow != null && !facts.Speaker.Ids.Contains(_metNow, StringComparer.Ordinal))
            _metNow = null;
        var speaker = facts.Speaker;
        if (speaker.HasMet || speaker.IsPrisoner || speaker.IsWanderer || !speaker.UsesLordConversations || facts.CaptiveContext
            || facts.LordWillAttack || facts.PlayerInSiege || _coop.IsCoopClient)
            return;

        var player = facts.Listeners.FirstOrDefault(l => l.IsPlayer);
        var choice = player == null ? null : Choose(facts with { Listeners = new[] { player } }, firstMeeting: true);
        if (choice == null || !_adapter.MeetSpeaker())
            return;
        _metNow = speaker.Ids[0];
        _outcomes.Pass(MeetingCheckId, choice.Key, "speaker=" + speaker.Ids[0]);
    }

    /// <summary>After vanilla set the met greeting: shows the pair's line when one applies.</summary>
    public void OnGreeting()
    {
        var facts = _adapter.Describe();
        var metNow = _metNow;
        _metNow = null;
        if (facts == null)
            return;

        var firstMeeting = metNow != null && facts.Speaker.Ids.Contains(metNow, StringComparer.Ordinal);
        var choice = Choose(facts, firstMeeting);
        if (choice == null)
            return;
        _adapter.SetGreeting(choice.Key, choice.Text);
        _outcomes.Pass(GreetingCheckId, choice.Key, "first=" + (firstMeeting ? "true" : "false"));
    }

    /// <summary>
    /// The line for this conversation, or null. The listeners are tried in order (the player, then the companions); a companion only
    /// when the speaker does not ride in the player's party. A person whose recorded gender is not the hero's is nobody. Hostile (at
    /// war, or Free against Evil) takes the hostile line; otherwise a first meeting with the player takes the first line, or the line.
    /// A pair with no line for its case passes to the next listener.
    /// </summary>
    internal LoreGreetingChoice? Choose(LoreConversationFacts facts, bool firstMeeting)
    {
        if (facts.Speaker.IsPrisoner)
            return null;
        var table = _config.GetTable();
        var speaker = Person(table, facts.Speaker.Ids, facts.Speaker.IsFemale);
        if (speaker == null)
            return null;

        var speakerSide = _alignment.ResolveSide(facts.Speaker.Side);
        foreach (var listener in facts.Listeners)
        {
            if (!listener.IsPlayer && facts.Speaker.InPlayerParty)
                continue;
            var person = Person(table, listener.Ids, listener.IsFemale);
            if (person == null || person.Id == speaker.Id)
                continue;
            var lines = table.LinesFor(speaker.Id, person.Id);
            if (lines == null)
                continue;

            var hostile = listener.AtWarWithSpeaker || FactionSideRules.AreOpposed(speakerSide, _alignment.ResolveSide(listener.Side));
            var (variant, text) = hostile
                ? (LoreGreetingVariant.Hostile, lines.Hostile)
                : firstMeeting && listener.IsPlayer && lines.First != null
                    ? (LoreGreetingVariant.First, lines.First)
                    : (LoreGreetingVariant.Text, lines.Text);
            if (text != null)
                return new LoreGreetingChoice(Key(speaker.Id, person.Id, variant), text);
        }
        return null;
    }

    private static LorePerson? Person(LoreGreetingTable table, System.Collections.Generic.IReadOnlyList<string> ids, bool isFemale)
    {
        var person = table.PersonOf(ids);
        return person != null && person.IsFemale == isFemale ? person : null;
    }
}
