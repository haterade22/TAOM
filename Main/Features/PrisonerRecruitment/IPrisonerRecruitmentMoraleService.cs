namespace TAOM.Features.PrisonerRecruitment;

public interface IPrisonerRecruitmentMoraleService
{
    /// <summary>
    /// True when the prisoner-recruitment morale penalty should be waived entirely — the prisoner is
    /// of the recruiter's own culture, or on the same non-Neutral alignment side (an Isengard captor
    /// recruiting a Mordor/Gundabad/Dunland prisoner: all serve Sauron, so nothing is lost). False
    /// falls the caller through to vanilla, including the Presence/TwoFaced perk waivers and the -2
    /// bandit cost. Null/unknown ids resolve Neutral and never waive.
    /// </summary>
    bool ShouldWaiveMoralePenalty(
        string recruiterKingdomId, string recruiterCultureId, string prisonerCultureId, bool isPlayerRecruiter);
}
