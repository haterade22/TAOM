using System;
using TAOM.Features.Enlistment.Domain;

namespace TAOM.Features.Enlistment;

public enum PetitionResult
{
    Accepted = 0,
    AlreadyEnlisted = 1,
    CommanderUnavailable = 2,
    InvalidCommander = 3,
}

public enum OathResult
{
    Sworn = 0,
    NoMatchingPetition = 1,
    CommanderUnavailable = 2,
}

/// <summary>
/// Orchestration facade for the enlistment lifecycle: petition a lord, swear the oath in
/// conversation, and request discharge. Enlist-eligibility gates beyond commander fitness
/// (mercenary contract, war state) are enforced by the dialog layer before calling in.
/// </summary>
public interface IEnlistmentService
{
    PetitionResult SubmitPetition(string commanderHeroId);

    /// <summary>Withdraw a pending petition. False when no petition is pending.</summary>
    bool WithdrawPetition();

    /// <summary>
    /// Swear the oath with the petitioned commander. Sets identities + contract timers,
    /// transitions to EnlistedAttached, and parks the party (park failures are logged and
    /// left to the hourly reconciler — the oath itself is never lost).
    /// </summary>
    OathResult CompleteOath(string commanderHeroId, string enlistedHeroId, double nowDays);

    bool RequestDischarge(DischargeReason reason);

    /// <summary>Raised after a successful oath with the commander hero id.</summary>
    event Action<string> EnlistmentStarted;
}
