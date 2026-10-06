namespace TAOM.Features.Arena;

/// <summary>
/// A tournament roster candidate, reduced to the primitives the winner panel and the #744 alignment
/// filter read. The hook builds this from a sealed <c>CharacterObject</c> at the boundary so both
/// services' decision logic stays unit-testable without Campaign.Current (ADR-007).
/// </summary>
public readonly struct TournamentEntrant
{
    /// <summary>Character string id, e.g. <c>erebor_reg_miner</c> or <c>lord_E6_3</c>.</summary>
    public string CharacterId { get; }

    /// <summary>Display name, for the log line only.</summary>
    public string Name { get; }

    public bool IsHero { get; }

    public bool IsFemale { get; }

    /// <summary>Race name (lower-case) or <c>null</c> when it could not be resolved.</summary>
    public string? RaceName { get; }

    /// <summary>Owning clan id, or <c>null</c> for a clanless hero (wanderer, notable) / a troop.</summary>
    public string? ClanId { get; }

    /// <summary>
    /// True when <c>Hero.MapFaction</c> is non-null. Meaningful for heroes only.
    /// <c>MapFaction</c> returns null for a clanless, non-special hero with neither a home
    /// settlement nor a party (verified: <c>TaleWorlds.CampaignSystem.Hero</c> v1.4.7, MapFaction).
    /// </summary>
    public bool HasMapFaction { get; }

    /// <summary>True when <c>CharacterObject.Culture</c> is non-null.</summary>
    public bool HasCulture { get; }

    /// <summary>The hero's clan's kingdom id, or <c>null</c> for a kingdomless hero or a troop. #744.</summary>
    public string? KingdomId { get; }

    /// <summary><c>CharacterObject.Culture.StringId</c>, or <c>null</c>. #744.</summary>
    public string? CultureId { get; }

    /// <summary>The player, or a hero of the player's clan: never barred from a tournament. #744.</summary>
    public bool IsPlayerOrPlayerClan { get; }

    public TournamentEntrant(
        string characterId,
        string name,
        bool isHero,
        bool isFemale,
        string? raceName,
        string? clanId,
        bool hasMapFaction,
        bool hasCulture,
        string? kingdomId = null,
        string? cultureId = null,
        bool isPlayerOrPlayerClan = false)
    {
        CharacterId = characterId;
        Name = name;
        IsHero = isHero;
        IsFemale = isFemale;
        RaceName = raceName;
        ClanId = clanId;
        HasMapFaction = hasMapFaction;
        HasCulture = hasCulture;
        KingdomId = kingdomId;
        CultureId = cultureId;
        IsPlayerOrPlayerClan = isPlayerOrPlayerClan;
    }
}

/// <summary>
/// The host of a tournament, reduced to the ids the #744 alignment filter reads. The side comes from the
/// owner faction and that faction's own culture (the owner kingdom, or the owner clan when it has none), so
/// a captured town follows its conqueror. The town's culture and the cultures of its basic and elite
/// troops mark the town's own troops, which always compete.
/// </summary>
public readonly struct TournamentHost
{
    /// <summary><c>Settlement.MapFaction.StringId</c>: the owner kingdom, or the owner clan without one.</summary>
    public string? OwnerFactionId { get; }

    /// <summary>The owner faction's own culture id, the side's fallback when the faction is not listed.</summary>
    public string? OwnerCultureId { get; }

    /// <summary><c>Settlement.Culture.StringId</c>.</summary>
    public string? TownCultureId { get; }

    /// <summary>
    /// Culture of the town culture's <c>BasicTroop</c>: vanilla pads the roster from that troop's upgrade
    /// tree, which seven cultures borrow from another culture (Umbar's is <c>aserai</c>).
    /// </summary>
    public string? BasicTroopCultureId { get; }

    /// <summary>Culture of the town culture's <c>EliteBasicTroop</c>, the troop Patch69 fills with first.</summary>
    public string? EliteTroopCultureId { get; }

    public TournamentHost(string? ownerFactionId, string? ownerCultureId, string? townCultureId,
        string? basicTroopCultureId, string? eliteTroopCultureId)
    {
        OwnerFactionId = ownerFactionId;
        OwnerCultureId = ownerCultureId;
        TownCultureId = townCultureId;
        BasicTroopCultureId = basicTroopCultureId;
        EliteTroopCultureId = eliteTroopCultureId;
    }
}

/// <summary>Why an entrant would break <c>TournamentVM.OnTournamentEnd</c> if it won.</summary>
public enum TournamentEntrantVerdict
{
    /// <summary>Both winner-panel branches are safe for this entrant.</summary>
    Safe,

    /// <summary>Hero entrant whose <c>MapFaction</c> is null — trips the hero branch.</summary>
    UnsafeNullMapFaction,

    /// <summary>Troop entrant whose <c>Culture</c> is null — trips the non-hero branch.</summary>
    UnsafeNullCulture,

    /// <summary>The entrant itself is null — vanilla would NRE far earlier than the winner panel.</summary>
    UnsafeNullCharacter
}
