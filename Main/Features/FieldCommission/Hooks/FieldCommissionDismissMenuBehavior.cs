using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TAOM.Features.CoopInterop;

namespace TAOM.Features.FieldCommission.Hooks;

/// <summary>
/// Thin menu registration (ADR-002) for the conversation-free dismissal path (#540): a
/// "Discharge a promoted companion" option on the town, castle and village menus, shown only
/// while at least one promoted companion qualifies. Not gated on the MCM master switch: an
/// already-promoted companion is an ordinary companion, and turning promotions off must not
/// strand one. Exists because #415 reports promoted companions with no dialogue, and a route that
/// needs no conversation is the one that keeps working if that is ever confirmed.
/// </summary>
public class FieldCommissionDismissMenuBehavior : CampaignBehaviorBase
{
    private const string OptionId = "taom_fc_dismiss_menu";
    private const string OptionText = "{=taom_fc_dismiss_menu}Discharge a promoted companion";

    private readonly IFieldCommissionDismissService _dismiss;
    private readonly ICoopSessionProvider _coopSession;

    public FieldCommissionDismissMenuBehavior(
        IFieldCommissionDismissService dismiss,
        ICoopSessionProvider coopSession)
    {
        _dismiss = dismiss;
        _coopSession = coopSession;
    }

    public override void RegisterEvents() =>
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);

    public override void SyncData(IDataStore dataStore) { }

    private void OnSessionLaunched(CampaignGameStarter starter)
    {
        // The slots EliteEmissary uses: below the vanilla actions, above leave.
        starter.AddGameMenuOption("town", OptionId, OptionText, MenuCondition, MenuConsequence, isLeave: false, index: 5);
        starter.AddGameMenuOption("castle", OptionId, OptionText, MenuCondition, MenuConsequence, isLeave: false, index: 5);
        starter.AddGameMenuOption("village", OptionId, OptionText, MenuCondition, MenuConsequence, isLeave: false, index: 4);
    }

    private bool MenuCondition(MenuCallbackArgs args)
    {
        args.optionLeaveType = GameMenuOption.LeaveType.Manage;
        return _coopSession.IsAuthority && _dismiss.GetDismissableCompanions().Count > 0;
    }

    private void MenuConsequence(MenuCallbackArgs args)
    {
        if (!_coopSession.IsAuthority)
            return;

        _dismiss.OpenDismissPicker();
    }
}
