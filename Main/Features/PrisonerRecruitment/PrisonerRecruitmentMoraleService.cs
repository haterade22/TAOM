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
        string recruiterKingdomId, string recruiterCultureId, string prisonerCultureId, bool isPlayerRecruiter)
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

        // Rule 2 -- own side. Neutral never waives: unaffiliated is not allied.
        var recruiterSide = ResolveRecruiterSide(recruiterKingdomId, recruiterCultureId);
        if (recruiterSide == FactionSide.Neutral) return false;

        return _alignment.GetCultureSide(prisonerCultureId) == recruiterSide;
    }

    /// <summary>
    /// Kingdom-first, culture-fallback. The fallback is load-bearing, not defensive: a kingdomless
    /// Isengard player has Clan.Kingdom == null, and a player-founded kingdom is "new_kingdom" --
    /// both unclassified, so without the fallback the feature would never fire for its primary user.
    /// Deliberately NOT IAlignmentService.AreEnemyAlignments, whose Neutral semantics are inverted
    /// (it treats Neutral as enemy-of-everyone); three other consumers route around it too.
    /// </summary>
    private FactionSide ResolveRecruiterSide(string kingdomId, string cultureId)
    {
        var side = _alignment.GetKingdomSide(kingdomId);
        if (side != FactionSide.Neutral) return side;

        return string.IsNullOrEmpty(cultureId) ? FactionSide.Neutral : _alignment.GetCultureSide(cultureId);
    }
}
