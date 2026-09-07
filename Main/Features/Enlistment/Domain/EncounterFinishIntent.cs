namespace TAOM.Features.Enlistment.Domain;

/// <summary>
/// Why enlistment wants to finish a PlayerEncounter. The intent changes the answer: a discharge
/// must hand the player back even from an encounter it does not own, while the oath must never
/// touch one that is not the commander's.
/// </summary>
public enum EncounterFinishIntent
{
    /// <summary>The oath was just sworn in conversation; close the encounter it happened in.</summary>
    OathHandoff,

    /// <summary>A stale encounter is in the way of seeding the commander's battle.</summary>
    StaleBeforeCommanderBattle,

    /// <summary>A join attempt failed; clean up whatever it created.</summary>
    JoinRollback,

    /// <summary>The hourly sweep found an encounter open while parked with no battle in progress.</summary>
    ParkedSweep,

    /// <summary>Service is ending; the player must be handed back interactable no matter what.</summary>
    Discharge,

    /// <summary>
    /// A shore-leave pass has been revoked and the settlement encounter it opened must come down.
    /// One of the two intents that treat a settlement-shaped encounter as ours (the other is
    /// <see cref="StrandedOutsideSettlement"/>, added 2026-09-04): R3 exists to protect a town visit
    /// the player owns, and under this intent we are the ones who opened it
    /// (<c>EnlistmentPlayerActionService.TakeTownLeave</c>). <c>ParkedSweep</c> still skips it by R3,
    /// which is what keeps this intent necessary, and a leaked encounter blocks every main-party
    /// encounter for the rest of the term.
    /// </summary>
    ShoreLeaveEnd,

    /// <summary>
    /// The hourly sweep found a settlement-shaped encounter while the player is NOT in a settlement.
    /// The second intent that treats such an encounter as ours, and for the same reason as
    /// <see cref="ShoreLeaveEnd"/>: R3 protects a town visit the player owns, and there is no town
    /// visit to protect once the party has left the settlement.
    ///
    /// It exists because the engine leaves exactly this behind.
    /// <c>LeaveSettlementAction.ApplyForParty</c> (installed v1.4.8) calls
    /// <c>PlayerEncounter.Finish()</c> in one branch only, when the LEAVING party leads its army and
    /// the main party is attached to it. An enlisted player is the main party and leads nothing, so
    /// every settlement exit since #510 (which opens an encounter on entry, deliberately) can strand
    /// one. Left open it blocks map movement and every future encounter, and it also blocks
    /// <c>ServiceMaintenanceService.TryBreakBattleLatch</c>, which is how a siege turned it into a
    /// permanent latch in <c>EnlistedBattle</c>.
    ///
    /// The precondition is ENFORCED BY THE POLICY, not owed by the caller: R2c also requires
    /// <c>!snapshot.PlayerInsideSettlement</c>, read fresh by the adapter. The first version of this
    /// intent trusted the caller, and the caller read that fact from a different snapshot taken
    /// earlier in the tick. Passing this intent while inside a settlement now yields a skip rather
    /// than the destroyed town visit R3 was written to prevent.
    /// </summary>
    StrandedOutsideSettlement,

    /// <summary>
    /// The player has been latched in <c>EnlistedBattle</c> with an open battle-shaped encounter and
    /// no map event of his own for longer than any loot screen lasts. The ONLY intent that gets past
    /// R1b, and the reason it can is duration.
    ///
    /// Issue #551. A join that lands and is then torn down in the same second leaves exactly this
    /// shape, and three guards each refuse to move it, each of them correctly:
    /// <c>ServiceMaintenanceService.TryBreakBattleLatch</c> returns on <c>HasPlayerEncounter</c>,
    /// <c>EnlistmentReconciler.SweepStrandedEncounter</c> returns on <c>commanderInMapEvent</c>, and
    /// R1b defers every intent, <see cref="Discharge"/> included, because the encounter reads as a
    /// battle. The player cannot move, cannot open any encounter, has no service menu, and nothing
    /// in the mod will ever release him. In the reported session that ended in a CTD when the map
    /// event he had been pulled out of ticked its own simulation.
    ///
    /// R1b justifies itself on the loot window being short and every caller retrying. That is what
    /// licenses this intent rather than contradicting it: the caller only reaches for it once the
    /// shape has persisted past <c>EnlistmentCoreConfig.StaleBattleLatchDays</c>, so a real loot
    /// screen is never in scope.
    ///
    /// Like <see cref="StrandedOutsideSettlement"/>, its preconditions are ENFORCED BY THE POLICY
    /// rather than owed by the caller: R1 still outranks it, so a player genuinely in a map event is
    /// untouchable, and R1c additionally requires <c>!snapshot.PlayerInsideSettlement</c> so a town
    /// visit cannot be destroyed by a latch that resolved into one.
    /// </summary>
    StaleBattleLatch,
}

/// <summary>The decision, and the reason, so the log says which rule fired.</summary>
public enum EncounterFinishVerdict
{
    /// <summary>Nothing live to finish.</summary>
    NothingToFinish,

    /// <summary>Finish it.</summary>
    Finish,

    /// <summary>Not ours — a settlement visit, someone else's business. Leave it alone.</summary>
    SkipNotOurs,

    /// <summary>A conversation is running; finishing would drop the player out of their own dialogue.</summary>
    SkipConversationInProgress,

    /// <summary>The player is in their OWN map event. Never tear down a battle they are fighting.</summary>
    DeferPlayerOwnBattle,
}
