using TAOM.Features.Execution;

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
    /// <param name="recruiterSide">
    /// The recruiter's id and culture pair for the side rule (<c>FactionSides.OfHero</c>): the clan's map faction
    /// with that faction's culture, or a clanless hero's own culture (#957).
    /// </param>
    /// <param name="recruiterCultureId">
    /// The recruiter hero's OWN culture, for the same-culture rule: it can differ from the side pair's culture
    /// (a Gondor-cultured lord of a Neutral realm).
    /// </param>
    bool ShouldWaiveMoralePenalty(
        SideIds recruiterSide, string recruiterCultureId, string prisonerCultureId, bool isPlayerRecruiter);
}
