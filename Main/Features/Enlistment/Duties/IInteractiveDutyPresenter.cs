using TAOM.Features.Enlistment.Content.Domain;

namespace TAOM.Features.Enlistment.Duties;

/// <summary>
/// One presenter for all 11 interactive-duty rows AND the 3 incident rows — both share the
/// same shape (title/body popup, two skill-check options). Routes the check through
/// <c>ISkillCheckService</c> and the reward through <c>IServiceRewardService</c>; every
/// popup/toast is drawn through <c>IInquiryAdapter</c> (ADR-007).
/// </summary>
public interface IInteractiveDutyPresenter
{
    void PresentInteractiveDuty(InteractiveDutyDefinition duty, ServiceProgressSnapshot progress, int trust);

    /// <summary>Incidents additionally apply <c>Effect == "ReleaseDeferredPay"</c> on a successful option.</summary>
    void PresentIncident(IncidentDefinition incident, ServiceProgressSnapshot progress, int trust);
}
