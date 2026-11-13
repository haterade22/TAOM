using TAOM.Features.CoopInterop;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;

namespace TAOM.Features.SupplyLines.Hooks;

/// <summary>
/// The one-option menu Patch73 opens when the player meets a supply caravan. Its Leave option ends the encounter, the
/// one place that is safe: the meeting itself runs inside <c>PlayerEncounter.Init</c> (#1104). A co-op guest's caravans
/// are the host's, so a guest reads a line that does not call the caravan theirs (#1086).
/// </summary>
internal sealed class SupplyCaravanMeetingMenu
{
    internal const string MenuId = "taom_sl_caravan_meeting";

    private const string LineVariable = "TAOM_SL_CARAVAN_LINE";
    private const string MenuText = "{=!}{TAOM_SL_CARAVAN_LINE}";
    private const string LeaveOptionId = "taom_sl_caravan_meeting_leave";
    private const string LeaveText = "{=3sRdGQou}Leave"; // Vanilla's key, translated in every language.

    private readonly ICoopSessionProvider _coop;

    internal SupplyCaravanMeetingMenu(ICoopSessionProvider coop) => _coop = coop;

    internal void AddTo(CampaignGameStarter starter)
    {
        starter.AddGameMenu(MenuId, MenuText, OnInit);
        starter.AddGameMenuOption(MenuId, LeaveOptionId, LeaveText, LeaveCondition, _ => Leave(), isLeave: true);
    }

    private void OnInit(MenuCallbackArgs args) => MBTextManager.SetTextVariable(
        LineVariable, new TextObject(SupplyCaravanEncounterPatch.MeetingLine(_coop.ShouldDeferToHost)));

    private static bool LeaveCondition(MenuCallbackArgs args)
    {
        args.optionLeaveType = GameMenuOption.LeaveType.Leave;
        return true;
    }

    private static void Leave()
    {
        PlayerEncounter.Finish();
        MobileParty.MainParty?.SetMoveModeHold();
    }
}
