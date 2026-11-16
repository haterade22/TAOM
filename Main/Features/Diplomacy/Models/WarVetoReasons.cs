using System;

namespace TAOM.Features.Diplomacy.Models;

// Player-facing refusal text per veto, as a {=key}default string for the caller to wrap in a TextObject.
public static class WarVetoReasons
{
    // A callToWarRefusals row (#1283). The caller sets CALLED_KINGDOM and KINGDOM_TO_CALL_TO_WAR_AGAINST.
    public const string CallToWarRefused =
        "{=taom_call_to_war_refused}{CALLED_KINGDOM} will not go to war against {KINGDOM_TO_CALL_TO_WAR_AGAINST}.";

    public static string Text(WarVeto veto)
    {
        switch (veto)
        {
            case WarVeto.Permanent:
                return "{=taom_war_blocked}These kingdoms are bound by an unbreakable alliance.";
            case WarVeto.SameAlignment:
                return "{=taom_war_blocked_alignment}These kingdoms stand on the same side of the War of the Ring.";
            case WarVeto.PhasePeace:
                return "{=taom_war_blocked_peace}The War of the Ring has not yet begun. No kingdom may declare war.";
            case WarVeto.PhaseNotScripted:
                return "{=taom_war_blocked_unscripted}Only the first wars of the age may be declared now.";
            default:
                throw new ArgumentOutOfRangeException(nameof(veto), veto, "None has no refusal text");
        }
    }
}
