using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;

namespace TAOM.Features.MapEventGuard.Hooks;

/// <summary>Registers the one-option menu Patch143 opens in place of a meeting nobody can speak in (#1099).</summary>
public sealed class EmptyMeetingMenuBehavior : CampaignBehaviorBase
{
    private const string MenuText = "{=taom_empty_meeting_text}There is nobody left in that party to speak for it.";
    private const string LeaveOptionId = "taom_empty_meeting_leave";
    private const string LeaveText = "{=3sRdGQou}Leave"; // Vanilla's key, translated in every language.

    private readonly EmptyMeetingGuardService _service;

    public EmptyMeetingMenuBehavior(EmptyMeetingGuardService service) => _service = service;

    public override void RegisterEvents() =>
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);

    public override void SyncData(IDataStore dataStore) { }

    private void OnSessionLaunched(CampaignGameStarter starter)
    {
        starter.AddGameMenu(EmptyMeetingGuardService.MenuId, MenuText, _ => { });
        starter.AddGameMenuOption(
            EmptyMeetingGuardService.MenuId, LeaveOptionId, LeaveText, LeaveCondition, _ => _service.Leave(), isLeave: true);
    }

    private static bool LeaveCondition(MenuCallbackArgs args)
    {
        args.optionLeaveType = GameMenuOption.LeaveType.Leave;
        return true;
    }
}
