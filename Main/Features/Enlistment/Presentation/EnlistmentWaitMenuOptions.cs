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
            args => { args.optionLeaveType = GameMenuOption.LeaveType.Manage; return true; },
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

                // SHOWN-AND-DISABLED, never hidden. Returning false here would remove the row and
                // shuffle every option below it under the player's cursor — worst at high game
                // speed, where availability flips often. Greying it out keeps the list stable AND
                // teaches the rule, which a vanished option cannot.
                var verdict = _actions.CanTalkToCommander();
                if (verdict != TalkToCommanderResult.Opened)
                {
                    args.IsEnabled = false;
                    args.Tooltip = ServiceVocabulary.TalkUnavailableReason(verdict);
                }

                return true;
            },
            _ => _actions.TalkToCommander(),
            false, 1);

        starter.AddGameMenuOption(
            EnlistmentMenuService.ServiceWaitMenuId,
            "taom_enlist_menu_duty",
            "{=taom_enlist_menu_duty}Ask your sergeant for work",
            args => { args.optionLeaveType = GameMenuOption.LeaveType.Manage; return true; },
            _ => _presenter.ReportDutyRequest(
                _actions.RequestDutyNow(CampaignTime.Now.ToDays, CampaignTime.Now.GetHourOfDay)),
            false, 2);

        // LAST, deliberately. Leaving service is rare and terminal; it sat second in the first
        // build (seen in-game 2026-08-08) directly above the two options a serving player uses
        // constantly, and it carries the back-arrow icon, which reads as "back" rather than
        // "end my career". Vanilla puts its leave option last for the same reason.
        starter.AddGameMenuOption(
            EnlistmentMenuService.ServiceWaitMenuId,
            "taom_enlist_menu_leave",
            "{=taom_enlist_menu_leave}Ask to be released from service",
            args => { args.optionLeaveType = GameMenuOption.LeaveType.Leave; return true; },
            _ => _presenter.RequestRelease(CampaignTime.Now.ToDays),
            false, 3);
    }
}
