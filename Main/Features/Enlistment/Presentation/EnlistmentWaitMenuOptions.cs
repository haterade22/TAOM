using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;

namespace TAOM.Features.Enlistment.Presentation;

/// <summary>
/// Registers the service wait menu's options. Split out of <c>EnlistmentMenuBehavior</c> (ADR-003
/// decomposition) because the behavior's job is lifecycle — register the menu, pump the tick,
/// re-assert on conversation end — and the option list is a separate, growing concern that had
/// pushed that behavior past the ADR-002 ceiling.
/// </summary>
public interface IEnlistmentWaitMenuOptions
{
    void Register(CampaignGameStarter starter);
}

public sealed class EnlistmentWaitMenuOptions : IEnlistmentWaitMenuOptions
{
    private readonly IEnlistmentWaitMenuPresenter _presenter;
    private readonly IEnlistmentPlayerActionService _actions;

    public EnlistmentWaitMenuOptions(
        IEnlistmentWaitMenuPresenter presenter,
        IEnlistmentPlayerActionService actions)
    {
        _presenter = presenter;
        _actions = actions;
    }

    public void Register(CampaignGameStarter starter)
    {
        starter.AddGameMenuOption(
            EnlistmentMenuService.ServiceWaitMenuId,
            "taom_enlist_menu_status",
            "{=taom_enlist_menu_status}Review your service",
            args => { args.optionLeaveType = GameMenuOption.LeaveType.Manage; return _actions.IsServing; },
            _ => _presenter.ShowServiceStatus(),
            false, 0);

        // isLeave stays FALSE (the default) on every option here, and that is load-bearing rather
        // than tidy: GameMenu.RunMenuOptionConsequence calls EndWait() BEFORE the consequence when
        // an option is marked isLeave on a wait menu. EndWait sets IsWaitActive = false and
        // TimeControlMode = Stop — so the wait-menu tick that drives our position sync would be
        // torn down before the conversation ever opened. It also makes the option a candidate for
        // the Escape/back slot. Verified against installed v1.4.7.
        starter.AddGameMenuOption(
            EnlistmentMenuService.ServiceWaitMenuId,
            "taom_enlist_menu_talk",
            "{=taom_enlist_menu_talk}Speak with your commander",
            args =>
            {
                args.optionLeaveType = GameMenuOption.LeaveType.Conversation;

                // SHOWN-AND-DISABLED, never hidden while serving. Returning false here would remove the row and
                // shuffle every option below it under the player's cursor — worst at high game
                // speed, where availability flips often. Greying it out keeps the list stable AND
                // teaches the rule, which a vanished option cannot.
                var verdict = _actions.CanTalkToCommander();
                if (verdict != TalkToCommanderResult.Opened)
                {
                    args.IsEnabled = false;
                    args.Tooltip = ServiceVocabulary.TalkUnavailableReason(verdict);
                }

                return _actions.IsServing;
            },
            _ => _actions.TalkToCommander(),
            false, 1);

        starter.AddGameMenuOption(
            EnlistmentMenuService.ServiceWaitMenuId,
            "taom_enlist_menu_duty",
            "{=taom_enlist_menu_duty}Ask your sergeant for work",
            args => { args.optionLeaveType = GameMenuOption.LeaveType.Manage; return _actions.IsServing; },
            _ => _presenter.ReportDutyRequest(
                _actions.RequestDutyNow(CampaignTime.Now.ToDays, CampaignTime.Now.GetHourOfDay)),
            false, 2);

        // Shore leave (field report 1). Shown only where there is a town to walk into, and this one
        // IS hidden rather than greyed — unlike "speak with your commander", whose unavailability is
        // a rule worth teaching, a settlement the column is not in is not a rule, it is just absent.
        // A permanently-greyed row on every march would be noise.
        starter.AddGameMenuOption(
            EnlistmentMenuService.ServiceWaitMenuId,
            "taom_enlist_menu_leave_pass",
            "{=taom_enlist_menu_leave_pass}Take leave in the settlement",
            args =>
            {
                args.optionLeaveType = GameMenuOption.LeaveType.Continue;
                return _actions.IsServing && _actions.CanTakeTownLeave();
            },
            _ => _presenter.TakeTownLeave(),
            false, 3);

        // Asking to leave is a DECISION with a cost the player must see first, so the presenter
        // owns verdict, popup and discharge; the menu exit is DischargeService's job (INV-D1).
        //
        // LAST, deliberately. Leaving service is rare and terminal; it sat second in the first
        // build (seen in-game 2026-08-08) directly above the two options a serving player uses
        // constantly, and it carries the back-arrow icon, which reads as "back" rather than
        // "end my career". Vanilla puts its leave option last for the same reason.
        starter.AddGameMenuOption(
            EnlistmentMenuService.ServiceWaitMenuId,
            "taom_enlist_menu_leave",
            "{=taom_enlist_menu_leave}Ask to be released from service",
            args => { args.optionLeaveType = GameMenuOption.LeaveType.Leave; return _actions.IsServing; },
            _ => _presenter.RequestRelease(CampaignTime.Now.ToDays),
            false, 4);

        // The one deliberate exception to the "isLeave stays FALSE" note above (#828). Shown only
        // when nobody is serving, which is what a discharge during load leaves behind: the saved
        // menu comes back with the five options above hidden. EndWait has already run by the time
        // this consequence does (GameMenu.cs:274-278, v1.4.8): the still-serving path re-enters the
        // menu, and a failed walk-out only makes sure this menu is on screen. isLeave is wanted for the leave hotkey and for ending the wait. Its
        // condition is false while serving, so it never becomes the Escape slot then. Never set
        // IsEnabled = false on it: it is the only way out of the menu.
        starter.AddGameMenuOption(
            EnlistmentMenuService.ServiceWaitMenuId,
            "taom_enlist_menu_discharged_leave",
            "{=taom_enlist_menu_discharged_leave}Go on your way",
            args => { args.optionLeaveType = GameMenuOption.LeaveType.Leave; return !_actions.IsServing; },
            _ => _actions.LeaveAfterDischarge(),
            true, 5);
    }
}
