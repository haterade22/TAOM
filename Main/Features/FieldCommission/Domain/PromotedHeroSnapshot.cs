namespace TAOM.Features.FieldCommission.Domain;

/// <summary>
/// What the dismissal flow needs to know about a living hero, read once per evaluation (each
/// lookup is a scan of <c>Hero.AllAliveHeroes</c>, the only registry that resolves a hero built
/// at runtime). No TaleWorlds types cross the boundary (ADR-007). <see cref="Missing"/> is the
/// answer for an id no living hero carries, the same shape as <see cref="TroopInfo.Missing"/>.
/// </summary>
public readonly struct PromotedHeroSnapshot
{
    private readonly bool _exists;

    public PromotedHeroSnapshot(string name, string originTroopId, bool isPlayerCompanion, bool isInMainParty, bool isPartyInBattle, bool isWounded)
    {
        _exists = true;
        Name = name;
        OriginTroopId = originTroopId;
        IsPlayerCompanion = isPlayerCompanion;
        IsInMainParty = isInMainParty;
        IsPartyInBattle = isPartyInBattle;
        IsWounded = isWounded;
    }

    public string Name { get; }

    /// <summary>StringId of the troop template the hero was created from (the engine's
    /// <c>CharacterObject.OriginalCharacter</c>, a saveable field), or null when there is none.</summary>
    public string OriginTroopId { get; }

    public bool IsPlayerCompanion { get; }

    public bool IsInMainParty { get; }

    /// <summary>True when the hero's party is in a map event or a siege: the exact predicate the
    /// engine defers a removal on.</summary>
    public bool IsPartyInBattle { get; }

    public bool IsWounded { get; }

    public bool IsMissing => !_exists;

    public static PromotedHeroSnapshot Missing => default;
}
