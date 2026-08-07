namespace TAOM.Features.Arena;

/// <summary>
/// A tournament roster candidate, reduced to the primitives the winner panel actually reads.
/// The hook builds this from a sealed <c>CharacterObject</c> at the boundary so the guard's
/// decision logic stays unit-testable without Campaign.Current (ADR-007).
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
    public string RaceName { get; }

    /// <summary>Owning clan id, or <c>null</c> for a clanless hero (wanderer, notable) / a troop.</summary>
    public string ClanId { get; }

    /// <summary>
    /// True when <c>Hero.MapFaction</c> is non-null. Meaningful for heroes only.
    /// <c>MapFaction</c> returns null for a clanless, non-special hero with neither a home
    /// settlement nor a party (verified: <c>TaleWorlds.CampaignSystem.Hero</c> v1.4.7, MapFaction).
    /// </summary>
    public bool HasMapFaction { get; }

    /// <summary>True when <c>CharacterObject.Culture</c> is non-null.</summary>
    public bool HasCulture { get; }

    public TournamentEntrant(
        string characterId,
        string name,
        bool isHero,
        bool isFemale,
        string raceName,
        string clanId,
        bool hasMapFaction,
        bool hasCulture)
    {
        CharacterId = characterId;
        Name = name;
        IsHero = isHero;
        IsFemale = isFemale;
        RaceName = raceName;
        ClanId = clanId;
        HasMapFaction = hasMapFaction;
        HasCulture = hasCulture;
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
