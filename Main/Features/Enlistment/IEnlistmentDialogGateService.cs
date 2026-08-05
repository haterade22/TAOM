namespace TAOM.Features.Enlistment;

public enum EnlistGateResult
{
    Ok = 0,
    AlreadyEnlisted = 1,
    NotALord = 2,
    UnderMercenaryContract = 3,
    CommanderUnavailable = 4,
    AtWarWithYourKingdom = 5,
}

/// <summary>
/// Eligibility gates for the enlist/discharge dialog lines. Pure decisions over the
/// store + adapters; the dialog behavior only renders the verdicts.
/// </summary>
public interface IEnlistmentDialogGateService
{
    EnlistGateResult CanEnlistWith(string partnerHeroId);

    /// <summary>True when the conversation partner is the player's current commander.</summary>
    bool CanRequestDischargeFrom(string partnerHeroId);

    /// <summary>
    /// Leaving before the contract day is desertion (arrears forfeit, relation cost);
    /// at/after it — or with no contract recorded — an honorable player request.
    /// </summary>
    Domain.DischargeReason ClassifyLeaveReason(double nowDays);
}
