using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TAOM.Features.FiefManagement.Models;

namespace TAOM.Features.FiefManagement.Hooks;

public class FiefHubCampaignBehavior : CampaignBehaviorBase
{
    private readonly IFiefHubMenuPresenter _presenter;
    private readonly IFiefManagementSettingsProvider _settings;

    public FiefHubCampaignBehavior(IFiefHubMenuPresenter presenter, IFiefManagementSettingsProvider settings)
    {
        _presenter = presenter;
        _settings = settings;
    }

    public override void RegisterEvents()
    {
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, OnNewGameCreated);
        CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, OnGameLoaded);
    }

    public override void SyncData(IDataStore dataStore)
    {
        // Selected-index cursor is transient — not persisted across save/load.
    }

    private void OnNewGameCreated(CampaignGameStarter starter) => _presenter.Reset();
    private void OnGameLoaded(CampaignGameStarter starter) => _presenter.Reset();

    // Register the menu UNCONDITIONALLY. The runtime EnableFiefManagement gate lives at the F6
    // patch and at the option conditions below — that way an MCM toggle mid-session takes effect
    // immediately. Codex review #36 caught: registering only when the setting was true at session
    // launch left "F6 -> ActivateGameMenu" calling into an unregistered menu after a runtime enable.
    public void OnSessionLaunched(CampaignGameStarter starter)
    {
        starter.AddGameMenu(
            "fief_hub", "{=!}{FIEF_HUB_TITLE}", new OnInitDelegate(OnMenuInit),
            GameMenu.MenuOverlayType.None);

        starter.AddGameMenuOption("fief_hub", "fief_hub_prev", "{=taom_fief_hub_prev}Previous fief",
            args => { args.optionLeaveType = GameMenuOption.LeaveType.Continue; return _settings.EnableFiefManagement && _presenter.Count > 1; },
            args => { _presenter.StepPrevious(); GameMenu.SwitchToMenu("fief_hub"); },
            isLeave: false, index: 0);

        starter.AddGameMenuOption("fief_hub", "fief_hub_next", "{=taom_fief_hub_next}Next fief",
            args => { args.optionLeaveType = GameMenuOption.LeaveType.Continue; return _settings.EnableFiefManagement && _presenter.Count > 1; },
            args => { _presenter.StepNext(); GameMenu.SwitchToMenu("fief_hub"); },
            isLeave: false, index: 1);

        starter.AddGameMenuOption("fief_hub", "fief_hub_manage", "{=taom_fief_hub_manage}Manage this fief",
            args =>
            {
                args.optionLeaveType = GameMenuOption.LeaveType.Manage;
                if (!_settings.EnableFiefManagement || _presenter.Count <= 0) return false;
                if (!_presenter.ManageOptionEnabled(out var hint))
                {
                    if (hint != null) { args.IsEnabled = false; args.Tooltip = hint; return true; }
                    return false;
                }
                return true;
            },
            args =>
            {
                var settlement = _presenter.ResolveCurrentSettlement();
                if (settlement == null) return;
                var manager = Game.Current?.GameStateManager;
                if (manager == null) return;
                // Use CreateState (not `new`) so vanilla HandleCreateState assigns GameStateManager
                // and notifies GameStateScreenManager.OnCreateState — that's what triggers our
                // Patch36 prefix to substitute GauntletFiefManagementScreen for this state.
                // Codex review #36 caught the original `new + PushState` path bypassing all of this.
                var state = manager.CreateState<FiefManagementGameState>();
                state.Initialize(settlement);
                manager.PushState(state);
            },
            isLeave: false, index: 2);

        starter.AddGameMenuOption("fief_hub", "fief_hub_leave", "{=taom_fief_hub_leave}Leave",
            args => { args.optionLeaveType = GameMenuOption.LeaveType.Leave; return true; },
            args => GameMenu.ExitToLast(),
            isLeave: true, index: 3);
    }

    private void OnMenuInit(MenuCallbackArgs args)
    {
        _presenter.Refresh();
        MBTextManager.SetTextVariable("FIEF_HUB_TITLE", _presenter.BuildTitle());
    }
}
