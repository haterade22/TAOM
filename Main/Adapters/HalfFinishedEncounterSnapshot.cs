namespace TAOM.Adapters;

/// <summary>What <c>PlayerEncounter.Battle</c> looks like to the half-finished encounter repair (Patch170).</summary>
public sealed class HalfFinishedEncounterSnapshot
{
    public HalfFinishedEncounterSnapshot(bool hasBattle, bool isFinalized, bool backReferenceMissing, int defenderParties, int attackerParties)
    {
        HasBattle = hasBattle;
        IsFinalized = isFinalized;
        BackReferenceMissing = backReferenceMissing;
        DefenderParties = defenderParties;
        AttackerParties = attackerParties;
    }

    /// <summary>The encounter holds a map event.</summary>
    public bool HasBattle { get; }

    /// <summary>The event reached <c>WaitingRemoval</c> (<c>MapEvent.IsFinalized</c>).</summary>
    public bool IsFinalized { get; }

    /// <summary>The event's component has no <c>MapEvent</c> back-reference (a save loaded it outside the event manager).</summary>
    public bool BackReferenceMissing { get; }

    public int DefenderParties { get; }

    public int AttackerParties { get; }
}
