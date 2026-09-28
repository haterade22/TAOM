using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.Localization;
using TAOM.Adapters;

namespace TAOM.Features.ArmourAcquisition.Hooks;

/// <summary>
/// Thin boundary (ADR-002): the town menu's "Visit the armoury" option, shown in every town while gating is
/// active, with the town's armoury level as its tooltip; the choice opens <see cref="ArmouryPresenter"/>.
/// Registered unconditionally on session launch, like the emissary's, so the condition decides. No SyncData.
/// </summary>
public sealed class ArmouryMenuBehavior : CampaignBehaviorBase
{
    private const string OptionId = "taom_armoury_visit";

    private readonly ArmouryPresenter _presenter;
    private readonly IArmourGateService _gate;
    private readonly ArmouryLevelService _levels;
    private readonly IArmouryTownAdapter _towns;

    public ArmouryMenuBehavior(ArmouryPresenter presenter, IArmourGateService gate, ArmouryLevelService levels, IArmouryTownAdapter towns)
    {
        _presenter = presenter;
        _gate = gate;
        _levels = levels;
        _towns = towns;
    }

    public override void RegisterEvents() =>
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);

    public override void SyncData(IDataStore dataStore)
    {
    }

    private void OnSessionLaunched(CampaignGameStarter starter) =>
        starter.AddGameMenuOption("town", OptionId, "{=taom_armoury_menu}Visit the armoury", Condition, Consequence, isLeave: false, index: 6);

    private bool Condition(MenuCallbackArgs args)
    {
        var townId = _towns.CurrentSettlementId;
        if (!_gate.IsActive || townId == null || !_towns.IsTown(townId))
            return false;
        args.optionLeaveType = GameMenuOption.LeaveType.Trade;
        args.Tooltip = new TextObject("{=taom_armoury_menu_tip}This town's armoury works to level {LEVEL} of 3.")
            .SetTextVariable("LEVEL", _levels.GetTownLevel(townId));
        return true;
    }

    private void Consequence(MenuCallbackArgs args)
    {
        var townId = _towns.CurrentSettlementId;
        if (townId != null)
            _presenter.Open(townId);
    }
}
