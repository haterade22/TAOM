using System.Collections.Generic;

namespace TAOM.Features.MapEventGuard;

/// <summary>
/// One live map event read at the boundary by <c>IStuckBattleEventAdapter</c>, with no engine type in it (ADR-007).
/// Healthy counts are the engine's own <c>MapEventSide.TroopCount</c> measure: the sum of
/// <c>NumberOfHealthyMembers</c> over the side's parties.
/// </summary>
public sealed class StuckBattleSnapshot
{
    public StuckBattleSnapshot(
        double ageHours,
        int attackerHealthy,
        int defenderHealthy,
        int destroyedParties,
        bool isVillageHostileAction,
        bool involvesPlayer,
        bool hasPendingOutcome,
        IReadOnlyList<string> partyIds)
    {
        AgeHours = ageHours;
        AttackerHealthy = attackerHealthy;
        DefenderHealthy = defenderHealthy;
        DestroyedParties = destroyedParties;
        IsVillageHostileAction = isVillageHostileAction;
        InvolvesPlayer = involvesPlayer;
        HasPendingOutcome = hasPendingOutcome;
        PartyIds = partyIds;
    }

    /// <summary>In-game hours since <c>MapEvent.BattleStartTime</c>.</summary>
    public double AgeHours { get; }

    public int AttackerHealthy { get; }

    public int DefenderHealthy { get; }

    /// <summary>Destroyed (inactive) mobile parties still attached, other than the player's and a quest's.</summary>
    public int DestroyedParties { get; }

    /// <summary>A raid, forced supplies or forced volunteers: 0 healthy defenders is normal there.</summary>
    public bool IsVillageHostileAction { get; }

    /// <summary>The player's party is in it, or the player stands in its encounter.</summary>
    public bool InvolvesPlayer { get; }

    /// <summary>A winner is already set or the event is already diplomatically finished; the next update ends it.</summary>
    public bool HasPendingOutcome { get; }

    /// <summary>Ids of every involved party, for the enlisted-commander check.</summary>
    public IReadOnlyList<string> PartyIds { get; }
}
