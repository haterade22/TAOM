namespace TAOM.Features.AlignmentRecruitment;

public interface IRecruitmentAlignmentService
{
    /// <summary>
    /// True when a recruiter serving <paramref name="recruiterKingdomId"/> must be blocked from
    /// recruiting at a settlement controlled by <paramref name="sourceKingdomId"/>, per the active
    /// alignment rule. Both ids are kingdom StringIds (the keys in <c>alignment.json</c>); a null /
    /// unknown id resolves to Neutral and never blocks. <paramref name="isPlayerRecruiter"/> lets
    /// the AI half be disabled independently.
    /// </summary>
    bool IsRecruitmentBlocked(string recruiterKingdomId, string sourceKingdomId, bool isPlayerRecruiter);
}
