using TaleWorlds.CampaignSystem;
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
            () => _gate.CanRequestDischargeFrom(Hero.OneToOneConversationHero?.StringId)
                  && _assignments.CooldownRemaining(CampaignTime.Now.ToDays) <= 0.0,
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
            "taom_enlist_reassign_pick",
            "taom_enlist_reassign_done",
            text,
            () => _assignments.Current != assignment,
            () => { if (_coopSession.IsAuthority) _assignments.Swap(assignment, CampaignTime.Now.ToDays); },
            109);
    }
}
