using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;
using TAOM.Core.Validation;
using TAOM.Features.CoopInterop;
using TAOM.Features.Enlistment.Content;
using TAOM.Features.Enlistment.Content.Domain;

namespace TAOM.Features.Enlistment.Hooks;

/// <summary>
/// Thin dialog registration (ADR-002) for changing your place in the line. Split from the
/// enlist/discharge chain to keep both entry points under the ADR-002 ceiling. Swap policy
/// (cooldown + trust cost) lives entirely in <see cref="IAssignmentService"/>.
/// </summary>
public class EnlistmentAssignmentDialogBehavior : CampaignBehaviorBase
{
    private readonly IEnlistmentDialogGateService _gate;
    private readonly IAssignmentService _assignments;
    private readonly ICoopSessionProvider _coopSession;

    public EnlistmentAssignmentDialogBehavior(
        IEnlistmentDialogGateService gate,
        IAssignmentService assignments,
        ICoopSessionProvider coopSession)
    {
        _gate = gate;
        _assignments = assignments;
        _coopSession = coopSession;
    }

    public override void RegisterEvents()
    {
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
    }

    public override void SyncData(IDataStore dataStore) { }

    private void OnSessionLaunched(CampaignGameStarter starter)
    {
        starter.AddPlayerLine(
            "taom_enlist_reassign",
            "hero_main_options",
            "taom_enlist_reassign_pick",
            "{=taom_enlist_reassign}I would take a different place in the line.",
            () => _gate.CanRequestDischargeFrom(Hero.OneToOneConversationHero?.StringId),
            null,
            109,
            ReassignIsClickable);

        // The commander answers before the options are listed. Two reasons, both real:
        //
        // 1. It names your CURRENT section. Each option below is hidden when it IS your current
        //    role, so a player already in the horse asks to change section, sees no cavalry line,
        //    and concludes cavalry is unavailable. Reported in-game 2026-08-07 as exactly that.
        //    Hiding the redundant option is right; hiding it with no explanation was not.
        // 2. Every dialog chain in this feature that works alternates player -> NPC -> player
        //    (see the oath chain). Reassignment went player -> player with nothing between.
        starter.AddDialogLine(
            "taom_enlist_reassign_pick",
            "taom_enlist_reassign_pick",
            "taom_enlist_reassign_choose",
            "{=taom_enlist_reassign_pick}You march with the {CURRENT_SECTION} at present. Where would you rather stand?",
            SetCurrentSectionName,
            null,
            109);

        AddOption(starter, "inf", ServiceAssignment.Infantry, "{=taom_enlist_reassign_inf}Put me in the shield line.");
        AddOption(starter, "arc", ServiceAssignment.Archer, "{=taom_enlist_reassign_arc}I shoot better than I stab.");
        AddOption(starter, "cav", ServiceAssignment.Cavalry, "{=taom_enlist_reassign_cav}I can ride. Give me a horse.");
        AddOption(starter, "sup", ServiceAssignment.Support, "{=taom_enlist_reassign_sup}The baggage and the wounded need hands.");

        starter.AddDialogLine(
            "taom_enlist_reassign_done",
            "taom_enlist_reassign_done",
            "close_window",
            "{=taom_enlist_reassign_done}Report to your new section, then. Don't make me regret it.",
            null,
            null,
            109);
    }

    private void AddOption(CampaignGameStarter starter, string suffix, ServiceAssignment assignment, string text)
    {
        starter.AddPlayerLine(
            "taom_enlist_reassign_" + suffix,
            "taom_enlist_reassign_choose",
            "taom_enlist_reassign_done",
            text,
            () => _assignments.Current != assignment,
            () => { if (_coopSession.IsAuthority) _assignments.Swap(assignment, CampaignTime.Now.ToDays); },
            109);
    }

    /// <summary>
    /// Condition AND text setup — the condition is the only hook that runs before the line
    /// renders, so the section name has to be pushed here or it renders as a raw token.
    /// </summary>
    private bool SetCurrentSectionName()
    {
        MBTextManager.SetTextVariable("CURRENT_SECTION", Presentation.ServiceVocabulary.SectionName(_assignments.Current));
        return true;
    }

    /// <summary>
    /// The swap cooldown greys this line; it no longer hides it. Hiding is what shipped first, and
    /// it produced the same failure as the missing CURRENT_SECTION line above, one level up: for
    /// seven days the option was simply absent, with nothing to say the player was waiting rather
    /// than locked out.
    /// </summary>
    private bool ReassignIsClickable(out TextObject? explanation)
    {
        var remaining = _assignments.CooldownRemaining(CampaignTime.Now.ToDays);
        if (ReassignIsTakeable(remaining))
        {
            explanation = null;
            return true;
        }

        explanation = Presentation.ServiceVocabulary.ReassignCooldownReason(remaining);
        return false;
    }

    /// <summary>
    /// Positive requirement, per the NaN-gate rule: the line unlocks only for a PROVABLY finite,
    /// spent cooldown. <c>CooldownRemaining</c> returns NaN for a NaN clock by design, and a
    /// <c>remaining &lt;= 0</c> test reads NaN as "ready" — re-opening the very swap the cooldown
    /// exists to prevent. Static so the degenerate inputs can be pinned without a live conversation.
    /// </summary>
    public static bool ReassignIsTakeable(double cooldownRemaining) =>
        FiniteFloatValidator.IsFinite(cooldownRemaining) && !(cooldownRemaining > 0.0);
}
