using TAOM.Features.Execution;

namespace TAOM.Features.EncounterLines.Models;

/// <summary>
/// What Encounter Lines reads from the lord a conversation tag is asked about, as plain values (the adapter keeps no engine object).
/// </summary>
/// <param name="IsLord">The hero is a lord (<c>Hero.IsLord</c>); wanderers, companions and notables are not.</param>
/// <param name="InPlayerClan">The hero belongs to the player's clan (spouse, family, companions).</param>
/// <param name="UnderPlayerCommand">The hero's party is under the player's command (<c>HeroHelper.UnderPlayerCommand</c>).</param>
/// <param name="CultureId">The hero's culture StringId, empty when it has none.</param>
/// <param name="RaceName">The character's race name, empty for a race id the race table does not hold.</param>
/// <param name="Side">The hero's map faction and that faction's culture, for <c>IAlignmentService.ResolveSide</c>.</param>
/// <param name="AtWarWithPlayer">The hero's map faction is at war with the player's.</param>
/// <param name="PlayerRulesRealm">The hero's map faction is a kingdom the player leads.</param>
/// <param name="HeroId">The hero's StringId.</param>
/// <param name="RuledRealmId">The StringId of the kingdom the hero leads, empty when the hero leads none.</param>
public sealed record LordFacts(
    bool IsLord,
    bool InPlayerClan,
    bool UnderPlayerCommand,
    string CultureId,
    string RaceName,
    SideIds Side,
    bool AtWarWithPlayer,
    bool PlayerRulesRealm,
    string HeroId,
    string RuledRealmId = "");

/// <summary>The speaker of a one-to-one conversation, read for a notable's introduction; lords and wanderers are not notables.</summary>
/// <param name="IsNotable">The hero is a notable (<c>Hero.IsNotable</c>: artisan, gang leader, preacher, merchant, rural notable or headman).</param>
/// <param name="RaceName">The character's race name, empty for a race id the race table does not hold.</param>
/// <param name="HeroId">The hero's StringId, which picks the variant of a line so a notable repeats it.</param>
public sealed record NotableFacts(bool IsNotable, string RaceName, string HeroId);

/// <summary>The player as Encounter Lines sees them.</summary>
/// <param name="CultureId">The main hero's culture StringId, empty when it has none.</param>
/// <param name="RaceName">The main hero's race name, empty for a race id the race table does not hold.</param>
/// <param name="Side">The player clan's map faction and its culture; <see cref="SideIds.None"/> when it cannot be read.</param>
public sealed record PlayerFacts(string CultureId, string RaceName, SideIds Side);

/// <summary>The caravan or villager party the player is talking to.</summary>
/// <param name="HomeCultureId">The culture of the party's home settlement, empty when it has none.</param>
/// <param name="AtWarWithPlayer">The party's map faction is at war with the player's.</param>
/// <param name="AtSea">The party is on the water.</param>
/// <param name="OwnedByPlayer">The party is the player's own (a caravan the player owns).</param>
public sealed record PartyFacts(string HomeCultureId, bool AtWarWithPlayer, bool AtSea, bool OwnedByPlayer);
