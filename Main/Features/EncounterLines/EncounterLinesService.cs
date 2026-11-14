using TAOM.Adapters;
using TAOM.Features.CoopInterop;
using TAOM.Features.EncounterLines.Models;
using TAOM.Features.Execution;
using TAOM.Features.InitialChildGeneration;
using TAOM.Features.Messengers;

namespace TAOM.Features.EncounterLines;

/// <summary>
/// The engine-free choices behind Encounter Lines (Patch149, docs/features/encounter-lines.md): which lord speaks for a
/// group and in which bucket, and which keyed line a caravan, villager party, bandit party or hideout boss says. Every
/// answer is "leave vanilla's text" unless the switch is on, this is not a dedicated server and no messenger conversation
/// is open. It holds no per-campaign state (the group map is per process), so no session reset is needed.
/// </summary>
internal sealed class EncounterLinesService : IEncounterBucketSource
{
    /// <summary>The chance a first meeting with a caravan or villager party opens with a TAOM greeting.</summary>
    internal const double GreetingChance = 0.5;

    /// <summary>The chance of a TAOM robbery scene for a clan that also has legacy Bandit Voices robbery rows.</summary>
    internal const double RobberyChanceWithLegacy = 0.5;

    /// <summary>The chance of a TAOM robbery scene for a clan with no legacy rows (Feral Orcs, looters).</summary>
    internal const double RobberyChanceWithoutLegacy = 0.8;

    internal const string CaravanGreetingVariable = "CARAVAN_GREETING";
    internal const string VillagerGreetingVariable = "VILLAGER_GREETING";
    internal const string SentenceLineVariable = "TAOM_ENC_LINE";

    internal const string CaravanGreetText = "str_taom_enc_caravan_greet";
    internal const string CaravanDefyText = "str_taom_enc_caravan_defy";
    internal const string VillagerGreetText = "str_taom_enc_villager_greet";
    internal const string VillagerDefyText = "str_taom_enc_villager_defy";
    internal const string HideoutBossText = "str_taom_enc_hideout_boss";

    internal const string CaravanDefySentence = "caravan_accepted_to_give_everything";
    internal const string VillagerDefySentence = "village_farmer_accepted_to_give_everything";
    internal const string HideoutBossSentence = "bandit_hideout_start_defender";

    /// <summary>Vanilla's four robbery variables and the game texts each is read from, in the order vanilla sets them.</summary>
    internal static readonly (string Variable, string TextId)[] RobberyLines =
    {
        ("ROBBERY_THREAT", "str_robbery_threat"),
        ("ROBBERY_PAY_AGREEMENT", "str_robbery_pay_agreement"),
        ("ROBBERY_CONCLUSION", "str_robbery_conclusion"),
        ("ROBBERY_START_FIGHT", "str_robbery_start_fight"),
    };

    private readonly IEncounterLinesSettingsProvider _settings;
    private readonly IEncounterLinesAdapter _adapter;
    private readonly IEncounterGroupsConfigProvider _groups;
    private readonly IAlignmentService _alignment;
    private readonly IRandomSource _random;
    private readonly IDedicatedServerProvider _dedicated;
    private readonly IMessengerConversationState _messenger;

    public EncounterLinesService(
        IEncounterLinesSettingsProvider settings,
        IEncounterLinesAdapter adapter,
        IEncounterGroupsConfigProvider groups,
        IAlignmentService alignment,
        IRandomSource random,
        IDedicatedServerProvider dedicated,
        IMessengerConversationState messenger)
    {
        _settings = settings;
        _adapter = adapter;
        _groups = groups;
        _alignment = alignment;
        _random = random;
        _dedicated = dedicated;
        _messenger = messenger;
    }

    private bool Active => _settings.Enabled && !_dedicated.IsDedicatedServer && !_messenger.IsOpen;

    /// <summary>
    /// True when <paramref name="lord"/> speaks for <paramref name="group"/>: a lord outside the player's clan, met on the
    /// map, whose culture is in the group and whose race family is the group's. A lord under the player's command is
    /// left to vanilla unless the player rules the lord's realm, where the liege rows apply.
    /// </summary>
    internal bool IsSpeaker(LordFacts? lord, string group)
    {
        if (lord == null || !Active || !lord.IsLord || lord.InPlayerClan || (lord.UnderPlayerCommand && !lord.PlayerRulesRealm))
            return false;
        if (_adapter.InSettlement)
            return false;

        var map = _groups.GetMap();
        var family = map.FamilyOfGroup(group);
        return family != null && map.GroupOfCulture(lord.CultureId) == group && map.FamilyOfRace(lord.RaceName) == family;
    }

    /// <summary>True when the player stands in <paramref name="bucket"/> towards <paramref name="lord"/>.</summary>
    internal bool IsLordBucket(LordFacts? lord, string bucket) => lord != null && LordBucket(lord) == bucket;

    /// <summary>True when today's surrender demand from <paramref name="lord"/> is the <c>_2</c> row.</summary>
    internal bool IsAltLine(LordFacts? lord) => lord != null && EncounterBuckets.IsAltLine(lord.HeroId, _adapter.DayOfYear);

    /// <summary>The lord's bucket, or null with no player.</summary>
    internal string? LordBucket(LordFacts lord)
    {
        if (!Active)
            return null;
        var player = _adapter.Player();
        if (player == null)
            return null;

        var map = _groups.GetMap();
        return EncounterBuckets.Lord(
            lord.PlayerRulesRealm,
            lord.AtWarWithPlayer,
            _alignment.ResolveSide(lord.Side),
            PlayerSide(player),
            map.FamilyOfRace(lord.RaceName),
            map.FamilyOfRace(player.RaceName),
            map.GroupOfCulture(lord.CultureId),
            map.GroupOfCulture(player.CultureId));
    }

    public string? BanditBucket(string? clanId)
    {
        if (!Active)
            return null;
        var player = _adapter.Player();
        var clan = _groups.GetMap().Bandit(clanId);
        if (player == null || clan == null)
            return null;

        var map = _groups.GetMap();
        return EncounterBuckets.Bandit(clan, map.GroupOfCulture(player.CultureId), map.FamilyOfRace(player.RaceName), PlayerSide(player));
    }

    /// <summary>Read by the greeting prefixes before vanilla records the meeting; false with the feature off.</summary>
    internal bool IsFirstCaravanMeeting() => Active && _adapter.IsFirstCaravanMeeting();

    /// <summary>Read by the greeting prefixes before vanilla records the meeting; false with the feature off.</summary>
    internal bool IsFirstVillagerMeeting() => Active && _adapter.IsFirstVillagerMeeting();

    /// <summary>On a first meeting on land with someone else's caravan, a roll under <see cref="GreetingChance"/> sets its greeting.</summary>
    internal bool TryCaravanGreeting(bool firstMeeting) =>
        firstMeeting && TryTradeLine(CaravanGreetingVariable, CaravanGreetText, GreetingChance, skipPlayersOwn: true);

    /// <summary>On a first meeting on land with villagers, a roll under <see cref="GreetingChance"/> sets their greeting.</summary>
    internal bool TryVillagerGreeting(bool firstMeeting) =>
        firstMeeting && TryTradeLine(VillagerGreetingVariable, VillagerGreetText, GreetingChance, skipPlayersOwn: false);

    /// <summary>
    /// After vanilla picked its robbery scene on land, replaces all four lines with one TAOM variation
    /// <c>&lt;clan&gt;_&lt;bucket&gt;_&lt;n&gt;</c>, or none of them when any of the four rows is missing.
    /// </summary>
    internal bool TryRobberySet()
    {
        if (!Active || _adapter.EncounteredAtSea)
            return false;
        var clan = _adapter.EncounteredFactionId;
        var bucket = BanditBucket(clan);
        if (bucket == null)
            return false;

        var chance = _adapter.HasText(RobberyLines[0].TextId, clan + "_1") ? RobberyChanceWithLegacy : RobberyChanceWithoutLegacy;
        var roll = _random.NextDouble();
        if (roll >= chance)
            return false;

        var variation = clan + "_" + bucket + "_" + (roll < chance / 2 ? 1 : 2);
        foreach (var (_, textId) in RobberyLines)
        {
            if (!_adapter.HasText(textId, variation))
                return false;
        }
        foreach (var (variable, textId) in RobberyLines)
            _adapter.TrySetVariable(variable, textId, variation);
        return true;
    }

    /// <summary>
    /// For the three sentences the text seam handles (caravan and villager surrender, the hideout boss's challenge), sets
    /// <see cref="SentenceLineVariable"/> to the TAOM line and answers true; any other sentence, or a missing row, false.
    /// </summary>
    internal bool TrySetSentenceLine(string? sentenceId)
    {
        switch (sentenceId)
        {
            case CaravanDefySentence:
                return TryTradeLine(SentenceLineVariable, CaravanDefyText, 1.0, skipPlayersOwn: true);
            case VillagerDefySentence:
                return TryTradeLine(SentenceLineVariable, VillagerDefyText, 1.0, skipPlayersOwn: false);
            case HideoutBossSentence:
                return TryHideoutLine();
            default:
                return false;
        }
    }

    /// <summary>
    /// The boss speaks for the clan of his own party (<see cref="IEncounterLinesAdapter.HideoutBossClanId"/>); only when that
    /// reads nothing does the hideout's map faction stand in, which for a mixed or emptied hideout can be another clan.
    /// </summary>
    private bool TryHideoutLine()
    {
        if (!Active)
            return false;
        var boss = _adapter.HideoutBossClanId;
        var clan = string.IsNullOrEmpty(boss) ? _adapter.EncounteredFactionId : boss;
        var bucket = BanditBucket(clan);
        return bucket != null && _adapter.TrySetVariable(SentenceLineVariable, HideoutBossText, clan + "_" + bucket);
    }

    private bool TryTradeLine(string variable, string textId, double chance, bool skipPlayersOwn)
    {
        if (!Active)
            return false;
        var party = _adapter.ConversationParty();
        var player = _adapter.Player();
        if (party == null || player == null || party.AtSea || (skipPlayersOwn && party.OwnedByPlayer))
            return false;

        var map = _groups.GetMap();
        var group = map.GroupOfCulture(party.HomeCultureId);
        if (group == null)
            return false;
        if (chance < 1.0 && _random.NextDouble() >= chance)
            return false;

        var bucket = EncounterBuckets.Trade(party.AtWarWithPlayer, group, map.GroupOfCulture(player.CultureId));
        return _adapter.TrySetVariable(variable, textId, group + "_" + bucket);
    }

    private FactionSide? PlayerSide(PlayerFacts player) =>
        player.Side.FactionId.Length == 0 && player.Side.CultureId.Length == 0
            ? null
            : _alignment.ResolveSide(player.Side);
}
