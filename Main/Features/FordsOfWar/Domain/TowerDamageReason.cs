namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>
/// Which branch decided a blow. The damage table returns every member but <c>Weaponless</c> and <c>Crowded</c>: the hit patch decides the first,
/// for a blow with no weapon to read, before the table is asked, and <see cref="TowerAssaultLimit"/> decides the second, once the table has passed a blow.
/// </summary>
internal enum TowerDamageReason
{
    Applied,
    RawZero,
    UnknownTower,
    Destroyed,
    Locked,
    OwnSide,
    Weaponless,
    FactionFeat,
    Crowded,
}
