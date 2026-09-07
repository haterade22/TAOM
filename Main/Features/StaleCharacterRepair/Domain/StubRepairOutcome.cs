namespace TAOM.Features.StaleCharacterRepair.Domain;

/// <summary>
/// What happened to one stale character. A bare <c>bool</c> collapsed five distinct meanings into
/// "failed", two of them badly: an already-healthy character (the sweep's own re-check winning a
/// benign race) and a lost engine binding (catastrophic, and every character fails) both reported
/// as "could NOT repair, a skill read on one of these can still crash the campaign". The first is
/// the opposite of true and the second names the wrong cause at maximum volume.
/// </summary>
public enum StubRepairOutcome
{
    /// <summary>At least one null field was filled in.</summary>
    Repaired,

    /// <summary>
    /// Nothing was null by the time the write ran. Benign: something else (another mod's load
    /// hook, or a re-entrant sweep) filled the fields between the scan and here. NOT a failure,
    /// and must never be reported as a live crash risk.
    /// </summary>
    AlreadyHealthy,

    /// <summary>
    /// A reflected engine member did not resolve, so no character can be repaired this session.
    /// One cause, reported once, naming the binding rather than the player's save data.
    /// </summary>
    BindingUnavailable,

    /// <summary>The character could not be resolved from its id, or carried no usable id.</summary>
    NotResolved,

    /// <summary>The write itself threw. The character is still dangerous.</summary>
    Failed,
}
