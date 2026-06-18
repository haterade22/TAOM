using TAOM.Features.Execution;

namespace TAOM.Features.AlignmentRecruitment;

/// <summary>
/// Pure decision for whether a recruiter↔source alignment pairing blocks recruitment. Reuses the
/// existing kingdom-keyed <see cref="IAlignmentService"/> (the same lookup the Execution and
/// Diplomacy features use). Deliberately does NOT call <see cref="IAlignmentService.AreEnemyAlignments"/>,
/// whose Neutral semantics are inverted for this purpose (it treats Neutral as an enemy of everyone);
/// here Neutral on either side is a mercenary "serve/accept anyone" and never blocks.
/// </summary>
public class RecruitmentAlignmentService : IRecruitmentAlignmentService
{
    private readonly IAlignmentService _alignment;
    private readonly IRecruitmentAlignmentSettingsProvider _settings;

    public RecruitmentAlignmentService(IAlignmentService alignment, IRecruitmentAlignmentSettingsProvider settings)
    {
        _alignment = alignment;
        _settings = settings;
    }

    public bool IsRecruitmentBlocked(string recruiterKingdomId, string sourceKingdomId, bool isPlayerRecruiter)
    {
        if (!_settings.IsEnabled)
            return false;
        if (isPlayerRecruiter && !_settings.ApplyToPlayer)
            return false;
        if (!isPlayerRecruiter && !_settings.ApplyToAi)
            return false;

        var recruiterSide = _alignment.GetKingdomSide(recruiterKingdomId);
        var sourceSide = _alignment.GetKingdomSide(sourceKingdomId);

        if (recruiterSide == FactionSide.Neutral || sourceSide == FactionSide.Neutral)
            return false;

        if (_settings.GoodRejectsEvilOnly)
            return recruiterSide == FactionSide.Free && sourceSide == FactionSide.Evil;

        // Symmetric: both sides are non-Neutral here, so a difference is a Free↔Evil opposition.
        return recruiterSide != sourceSide;
    }
}
