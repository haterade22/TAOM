using TaleWorlds.Localization;
using TAOM.Core.Validation;
using TAOM.Features.Enlistment.Content.Domain;

namespace TAOM.Features.Enlistment.Presentation;

/// <summary>
/// The single place a service enum becomes words a player reads.
///
/// EXISTS BECAUSE IT DRIFTED: the section names were written out twice — once in the reassignment
/// dialog, once in the status board — as identical four-case ladders over the same localization
/// keys. Two copies of one key set do not stay in sync, and the failure is silent: the dialog says
/// "baggage train" while the board says something else, and nothing errors.
///
/// The enum's own <c>ToString()</c> must never reach a player. It is an internal identifier: it
/// cannot be translated, and "Support" is not what a soldier calls the baggage train.
/// </summary>
public static class ServiceVocabulary
{
    public static TextObject SectionName(ServiceAssignment assignment)
    {
        switch (assignment)
        {
            case ServiceAssignment.Archer: return new TextObject("{=taom_enlist_section_arc}bowmen");
            case ServiceAssignment.Cavalry: return new TextObject("{=taom_enlist_section_cav}horse");
            case ServiceAssignment.Support: return new TextObject("{=taom_enlist_section_sup}baggage train");
            default: return new TextObject("{=taom_enlist_section_inf}shield line");
        }
    }

    public static TextObject RankName(ServiceRank rank)
    {
        switch (rank)
        {
            case ServiceRank.Soldier: return new TextObject("{=taom_enlist_rank_soldier}Soldier");
            case ServiceRank.Veteran: return new TextObject("{=taom_enlist_rank_veteran}Veteran");
            case ServiceRank.Sergeant: return new TextObject("{=taom_enlist_rank_sergeant}Sergeant");
            default: return new TextObject("{=taom_enlist_rank_recruit}Recruit");
        }
    }

    /// <summary>
    /// Merit bands carry a GradeKey that is an INTERNAL id ("distinguished", "rough"). It was
    /// substituted into the after-battle toast raw, so the player read "your conduct:
    /// distinguished" — engine data dropped mid-sentence, and untranslatable in all 12 languages
    /// even once the toast itself was localized. An unknown key falls back to the mildest real
    /// grade rather than echoing the id back at the player.
    /// </summary>
    public static TextObject GradeName(string gradeKey)
    {
        switch (gradeKey)
        {
            case "distinguished": return new TextObject("{=taom_enlist_grade_distinguished}distinguished");
            case "strong": return new TextObject("{=taom_enlist_grade_strong}strong");
            case "solid": return new TextObject("{=taom_enlist_grade_solid}steady");
            default: return new TextObject("{=taom_enlist_grade_rough}rough, but you held");
        }
    }

    /// <summary>
    /// Why "speak with your commander" is unavailable, as words for the greyed-out tooltip.
    ///
    /// The option is SHOWN and disabled rather than hidden. A hidden option makes the list shuffle
    /// under the player's cursor — the reference mod shipped that, hit it in play, and recorded the
    /// fix in its changelog: "changed the travelling menu options to always display, but become
    /// greyed out if they can't be performed; should stop aggravation from menu options constantly
    /// shifting position, particularly at higher game speeds." A disabled option that says WHY also
    /// teaches the rule; a vanished one just looks broken.
    /// </summary>
    public static TextObject TalkUnavailableReason(TalkToCommanderResult verdict)
    {
        switch (verdict)
        {
            case TalkToCommanderResult.InBattle:
                return new TextObject("{=taom_enlist_talk_no_battle}Not in the middle of a battle.");
            case TalkToCommanderResult.OnDuty:
                return new TextObject("{=taom_enlist_talk_no_duty}You are away on orders — ride to him yourself.");
            case TalkToCommanderResult.CommanderUnavailable:
                return new TextObject("{=taom_enlist_talk_no_commander}Your commander cannot be reached.");
            case TalkToCommanderResult.NotOnMap:
                return new TextObject("{=taom_enlist_talk_no_map}Not from here.");
            default:
                return new TextObject("{=taom_enlist_talk_no_generic}You cannot speak with him just now.");
        }
    }

    /// <summary>
    /// Why the enlist offer is greyed out, as words for the disabled-option hint.
    ///
    /// Exactly ONE rejection reaches this method in practice, and the split is deliberate:
    ///
    /// * <see cref="EnlistGateResult.AtWarWithYourKingdom"/> is SHOWN and greyed, because a player
    ///   cannot work it out unaided. The lord is standing there, alive, with a column behind him;
    ///   nothing on screen connects his war to your crown's. A vanished line teaches "broken".
    /// * <see cref="EnlistGateResult.CommanderUnavailable"/> is HIDDEN, and that is not an
    ///   oversight. It fires for every governor and every garrison-sitting lord — a large fraction
    ///   of the nobility, for the entire campaign. Greying it would park a permanently-dead line
    ///   near the top of most noble conversations, and unlike the war case the reason is already
    ///   on screen: a lord with no column is visibly not leading one.
    /// * AlreadyEnlisted / NotALord / UnderMercenaryContract / FeatureDisabled are self-evident,
    ///   permanent, or not about this lord at all.
    ///
    /// The default arm is unreachable while that split holds. It exists so a verdict added to the
    /// enum later surfaces as a vague hint instead of an unhandled case.
    /// </summary>
    public static TextObject EnlistUnavailableReason(EnlistGateResult verdict)
    {
        switch (verdict)
        {
            case EnlistGateResult.AtWarWithYourKingdom:
                return new TextObject("{=taom_enlist_no_at_war}Your kingdom is at war with his. You cannot take his coin and keep your own oath.");
            default:
                return new TextObject("{=taom_enlist_no_generic}You cannot enlist under him just now.");
        }
    }

    /// <summary>
    /// Why the reassignment line is greyed out, with the wait spelled out in days.
    ///
    /// The number binds to THIS TextObject rather than through MBTextManager.SetTextVariable, and
    /// that is a correctness requirement rather than a style choice. A conversation hint renders
    /// LATE: ConversationManager.GetSentenceOptions stores the TextObject by reference,
    /// ConversationItemVM.RefreshValues wraps it in a HintViewModel, and
    /// HintViewModel.ExecuteBeginHint only calls ToString() when the player hovers the row. A
    /// process-global text variable set inside our condition would be overwritten by any later
    /// sentence's condition in the same option sweep, and the hint would render someone else's
    /// number. Vanilla binds hint variables the same way — see
    /// LordConversationsCampaignBehavior.conversation_player_is_offering_mercenary_on_clickable_condition.
    /// Verified against installed v1.4.7.
    /// </summary>
    public static TextObject ReassignCooldownReason(double daysRemaining)
    {
        // Positive requirement, per the NaN-gate rule: a day count is printed only when one can be
        // PROVEN. NaN fails `> 0` and lands on the generic arm rather than rendering as garbage.
        if (!FiniteFloatValidator.IsFinite(daysRemaining) || !(daysRemaining > 0.0))
            return new TextObject("{=taom_enlist_reassign_no_generic}You cannot change section just now.");

        // Clamped BEFORE the narrowing conversion. A merely huge finite value overflows the cast
        // and would surface as a NEGATIVE wait — the float-to-int trap, distinct from NaN.
        var ceiling = System.Math.Ceiling(daysRemaining);
        var days = ceiling >= int.MaxValue ? int.MaxValue : (int)ceiling;

        // Two keys instead of one plural token: Bannerlord's plural forms need a per-language
        // functions file, and "1 more days" reads to a player as a bug in the mod.
        if (days == 1)
            return new TextObject("{=taom_enlist_reassign_no_cooldown_one}You changed section too recently. Ask again tomorrow.");

        return new TextObject("{=taom_enlist_reassign_no_cooldown}You changed section too recently. Ask again in {DAYS} days.")
            .SetTextVariable("DAYS", days);
    }
}
