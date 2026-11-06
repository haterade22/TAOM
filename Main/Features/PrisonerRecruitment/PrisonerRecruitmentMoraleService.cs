using System;
using TAOM.Features.Execution;

namespace TAOM.Features.PrisonerRecruitment;

/// <summary>
/// Pure waiver decision (ADR-002/007) for the prisoner-recruitment morale cost. Two rules, in order:
/// own faction (same culture), then own side (same non-Neutral alignment). Mirrors
/// <see cref="AlignmentDesertion.AlignmentDesertionService"/>, the opposite-direction feature —
/// desertion sheds troops whose culture opposes the owner's side; this waives the cost of taking on
/// troops whose culture shares it.
/// </summary>
public class PrisonerRecruitmentMoraleService : IPrisonerRecruitmentMoraleService
{
    private readonly IAlignmentService _alignment;
    private readonly IPrisonerRecruitmentSettingsProvider _settings;

    public PrisonerRecruitmentMoraleService(
        IAlignmentService alignment, IPrisonerRecruitmentSettingsProvider settings)
    {
        _alignment = alignment;
        _settings = settings;
    }

    public bool ShouldWaiveMoralePenalty(
        SideIds recruiterSide, string recruiterCultureId, string prisonerCultureId, bool isPlayerRecruiter)
    {
        if (!_settings.IsEnabled) return false;

        // Owner gate (player / AI), mirroring AlignmentDesertion + AlignmentRecruitment.
        if (isPlayerRecruiter && !_settings.ApplyToPlayer) return false;
        if (!isPlayerRecruiter && !_settings.ApplyToAi) return false;

        if (string.IsNullOrEmpty(prisonerCultureId)) return false;

        // Rule 1 -- own faction. Keyed on culture because a prisoner CharacterObject carries no
        // kingdom link at all (no Clan/Kingdom/IFaction -- culture is the only faction signal).
        // This rule is what covers the Neutral factions (Khand/Umbar/Shaghana/Abanissa): rule 2
        // deliberately never fires for them, so without this a Khand captor would lose morale
        // recruiting Khand troops.
        if (!string.IsNullOrEmpty(recruiterCultureId)
            && string.Equals(recruiterCultureId, prisonerCultureId, StringComparison.OrdinalIgnoreCase))
            return true;

        // Rule 2 -- own side. Neutral never waives: unaffiliated is not allied. The recruiter's pair is the
        // clan map faction with that faction's culture (FactionSides.OfHero, #957), resolved faction-first with
        // the culture fallback; the fallback is load-bearing, not defensive: a kingdomless Isengard player's
        // faction is the player clan itself and a player-founded kingdom is "new_kingdom", both unclassified.
        // The hero's own culture only feeds rule 1. Deliberately NOT IAlignmentService.AreEnemyAlignments,
        // whose Neutral semantics are inverted (it treats Neutral as enemy-of-everyone).
        var side = _alignment.ResolveSide(recruiterSide);
        if (side == FactionSide.Neutral) return false;

        return _alignment.GetCultureSide(prisonerCultureId) == side;
    }
}
