namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>
/// Which branch decided a blow. The damage table returns every member but <c>Weaponless</c>: the hit patch decides that one,
/// for a blow with no weapon to read, before the table is asked.
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
}
