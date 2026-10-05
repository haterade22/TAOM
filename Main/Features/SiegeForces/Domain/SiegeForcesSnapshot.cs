using System;
using System.Collections.Generic;

namespace TAOM.Features.SiegeForces.Domain;

/// <summary>
/// One troop stack of one party, as the siege troop picker reads it: the engine <c>CharacterObject</c> as an opaque
/// <see cref="Source"/> token (the RosterEntry precedent, so the rules never open an engine type) and the values the
/// rules decide on. <see cref="Number"/> counts everyone in the stack, wounded included.
/// </summary>
public sealed record SiegeTroop(
    object Source,
    string CharacterId,
    bool IsPlayerCharacter,
    int RaceId,
    int Number,
    int Wounded)
{
    /// <summary>The men who can fight: wounded men never can, and an inconsistent count never goes below zero.</summary>
    public int Healthy => Math.Max(0, Number - Wounded);
}

/// <summary>
/// One party on the player's side of the wall battle. <see cref="IsInPlayerArmy"/> is true for every party in the same
/// army as the main party (the leader included); <see cref="IsGarrison"/> for the garrison party of the besieged fief.
/// The rules decide from these flags and the snapshot's, so a fake can build any shape.
/// </summary>
public sealed record SiegeParty(
    string Id,
    bool IsMainParty,
    bool IsInPlayerArmy,
    bool IsGarrison,
    IReadOnlyList<SiegeTroop> Troops);

/// <summary>
/// The player's side of one wall battle, captured before the picker opens. <see cref="MapEventToken"/> is the engine
/// <c>MapEvent</c> as an opaque token: the totals prefix only applies a pending fit to the battle it was made for, by
/// reference, so a stale record can never touch another battle.
/// </summary>
/// <param name="MapEventToken">The player's map event, opaque.</param>
/// <param name="PlayerIsAttacker">True for an assault, false for a defence.</param>
/// <param name="PlayerLeadsArmy">The main party is the leader of an army. False when it leads none or follows another lord.</param>
/// <param name="DefendingOwnFief">The player defends a fief his own clan owns; only then is the garrison his to choose.</param>
/// <param name="Parties">Every party of the player's map event side, in the engine's order.</param>
public sealed record SiegeForcesSnapshot(
    object MapEventToken,
    bool PlayerIsAttacker,
    bool PlayerLeadsArmy,
    bool DefendingOwnFief,
    IReadOnlyList<SiegeParty> Parties);
