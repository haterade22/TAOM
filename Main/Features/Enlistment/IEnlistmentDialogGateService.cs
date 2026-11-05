namespace TAOM.Features.Enlistment;

public enum EnlistGateResult
{
    Ok = 0,
    AlreadyEnlisted = 1,
    NotALord = 2,
    UnderMercenaryContract = 3,
    CommanderUnavailable = 4,
    AtWarWithYourKingdom = 5,

    /// <summary>The whole feature is switched off in MCM.</summary>
    FeatureDisabled = 6,
}

/// <summary>
/// Eligibility gates for the enlistment dialog lines (enlist, kit, reassignment, release) and the
/// service menu's release option. Pure decisions over the store and adapters; callers only render
/// the verdicts.
/// </summary>
public interface IEnlistmentDialogGateService
{
    EnlistGateResult CanEnlistWith(string partnerHeroId);

    /// <summary>True when the player serves under this conversation partner.</summary>
    bool IsServingUnder(string partnerHeroId);

    /// <summary>
    /// The reason recorded for a leave the commander GRANTS. Always an honourable player
    /// request — see the implementation for why this is not a classification any more.
    /// </summary>
    Domain.DischargeReason ClassifyLeaveReason(double nowDays);

    /// <summary>
    /// Decide what happens when the player asks to be released, and how many days are owed if
    /// the answer is "not yet". Every degenerate input falls open to
    /// <see cref="Domain.ReleaseVerdict.Granted"/> — the failure mode of this gate is a player
    /// trapped in service with no exit, so it is built to let people go, not to hold them.
    /// </summary>
    Domain.ReleaseRequest EvaluateReleaseRequest(double nowDays);
}
