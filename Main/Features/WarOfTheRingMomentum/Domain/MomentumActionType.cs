namespace TAOM.Features.WarOfTheRingMomentum.Domain;

/// <summary>
/// Momentum-generating event categories. Member names match LOTRAOM's enum for
/// behavioral parity and are persisted by name in the SyncData store — renaming
/// a member breaks save round-trip.
/// </summary>
public enum MomentumActionType
{
    ArmyGathered,
    BattleWon,
    VillageRaided,
    Sieges,
    RelativeStrength
}
