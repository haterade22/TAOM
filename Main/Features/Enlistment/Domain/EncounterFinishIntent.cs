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
    /// The ONLY intent that treats a settlement-shaped encounter as ours: R3 exists to protect a
    /// town visit the player owns, and under this intent we are the ones who opened it
    /// (<c>EnlistmentPlayerActionService.TakeTownLeave</c>). Nothing else ever would: the parked
    /// sweep skips it by R3, and a leaked encounter blocks every main-party encounter for the rest
    /// of the term.
    /// </summary>
    ShoreLeaveEnd,
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
