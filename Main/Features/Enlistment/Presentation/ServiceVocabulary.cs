using TaleWorlds.Localization;
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
}
