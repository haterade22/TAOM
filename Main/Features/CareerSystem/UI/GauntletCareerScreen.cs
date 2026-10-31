using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.ScreenSystem;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem.UI;

// Uses GameStateScreen attribute so GameStateManager.PushState properly
// deactivates map bar input processing (avoids IndexOutOfRangeException
// in GauntletMapBarGlobalLayer.HandlePanelSwitchingInput).
[GameStateScreen(typeof(CareerScreenGameState))]
public class GauntletCareerScreen : ScreenBase, IGameStateListener
{
    private GauntletLayer _gauntletLayer;
    private GauntletMovieIdentifier _movie;
    private CareerScreenVM _viewModel;

    private readonly ICareerDataService _dataService;
    private readonly ICareerRegistry _registry;
    private readonly ICareerPassiveService _passiveService;
    private readonly ICareerConfigProvider _configProvider;
    private readonly ICareerQuestService _questService;
    private readonly CareerSwitchService _switchService;
    private readonly ICareerLifecycleService _lifecycle;
    private readonly ICareerPurseAdapter _purse;
    private readonly ICareerHeroAdapterFactory _adapterFactory;
    private readonly IModLogger _logger;
    private readonly string _heroStringId;
    private readonly int _heroLevel;

    // Mode + adapter are deferred to OnInitialize. Vanilla GameStateManager.CreateState<T>()
    // synchronously invokes this ctor via Activator.CreateInstance(type, state) BEFORE
    // OpenCareerScreen can set state.IsSwitchMode. Reading IsSwitchMode here would always
    // observe `false`, breaking the dialogue-driven switch picker entirely (Codex Review #32).
    private readonly CareerScreenGameState _state;
    private bool _isSwitchMode;
    private ICareerHeroAdapter _heroAdapter;

    public GauntletCareerScreen(CareerScreenGameState state)
    {
        _state = state;
        var hero = Hero.MainHero;
        _dataService = IoC.Resolve<ICareerDataService>();
        _registry = IoC.Resolve<ICareerRegistry>();
        _passiveService = IoC.Resolve<ICareerPassiveService>();
        _configProvider = IoC.Resolve<ICareerConfigProvider>();
        _questService = IoC.Resolve<ICareerQuestService>();
        _switchService = IoC.Resolve<CareerSwitchService>();
        _lifecycle = IoC.Resolve<ICareerLifecycleService>();
        _purse = IoC.Resolve<ICareerPurseAdapter>();
        _adapterFactory = IoC.Resolve<ICareerHeroAdapterFactory>();
        _logger = IoC.Resolve<IModLogger>();
        _heroStringId = hero?.StringId ?? "";
        _heroLevel = hero?.Level ?? 0;
        // DO NOT read state.IsSwitchMode here -- see Codex Review #32 comment block above.
    }

    public static void OpenCareerScreen(bool switchMode = false)
    {
        var logger = IoC.Resolve<IModLogger>();
        logger?.LogInfo($"CareerSystem: OpenCareerScreen called (switchMode={switchMode})");

        var hero = Hero.MainHero;
        if (hero == null)
        {
            logger?.LogWarning("CareerSystem: OpenCareerScreen — MainHero is null");
            return;
        }

        logger?.LogInfo($"CareerSystem: Opening career screen for hero '{hero.StringId}' level={hero.Level}");
        var state = Game.Current.GameStateManager.CreateState<CareerScreenGameState>();
        // CreateState already constructed the screen synchronously via Activator.CreateInstance.
        // Setting state.IsSwitchMode here is too late for the ctor but in time for OnInitialize,
        // which reads it when the screen activates after PushState (Codex Review #32).
        state.IsSwitchMode = switchMode;
        Game.Current.GameStateManager.PushState(state);
    }

    protected override void OnInitialize()
    {
        base.OnInitialize();

        // Mode + adapter resolution deferred from ctor to here so OpenCareerScreen has had
        // time to set state.IsSwitchMode after CreateState returned.
        _isSwitchMode = _state?.IsSwitchMode ?? false;
        var hero = Hero.MainHero;
        _heroAdapter = (_isSwitchMode && hero != null) ? _adapterFactory.Create(hero) : null;
        _logger?.LogInfo($"CareerSystem: GauntletCareerScreen.OnInitialize — switchMode={_isSwitchMode} heroAdapter={(_heroAdapter != null ? "ok" : "null")}");

        _gauntletLayer = new GauntletLayer("CareerScreen", 1);
        _gauntletLayer.IsFocusLayer = true;
        _viewModel = new CareerScreenVM(
            _dataService, _registry, _passiveService, _configProvider, _logger,
            _heroStringId, _heroLevel, CloseScreen, _questService,
            isSwitchMode: _isSwitchMode,
            heroAdapter: _heroAdapter,
            onChooseSwitchTarget: OnChooseSwitchTarget,
            onRetrainRequested: OnRetrainRequested);
        _movie = _gauntletLayer.LoadMovie("CareerScreen", _viewModel);
        _gauntletLayer.InputRestrictions.SetInputRestrictions();
        AddLayer(_gauntletLayer);
        ScreenManager.TrySetFocus(_gauntletLayer);
        Outcomes.Pass(CareerScreenChecks.InitId, CareerScreenChecks.Mode(_isSwitchMode), CareerScreenChecks.InitDetail(_isSwitchMode));
    }

    private void OnChooseSwitchTarget(string newCareerId)
    {
        if (string.IsNullOrEmpty(newCareerId) || _heroAdapter == null)
        {
            _logger?.LogWarning($"CareerSystem: OnChooseSwitchTarget skipped — careerId='{newCareerId}' heroAdapter={_heroAdapter != null}");
            CloseScreen();
            return;
        }

        // Leaving a career that holds fork picks costs gold (#769 E7): ask first. Cancel stays in the picker.
        var cost = _switchService.GetSwitchCost(_heroStringId, _heroAdapter.Level);
        if (cost <= 0)
        {
            SwitchAndClose(newCareerId, cost);
            return;
        }

        ShowConfirmation(
            new TextObject("{=taom_career_switch_cost_title}Switch career").ToString(),
            new TextObject("{=taom_career_switch_cost_body}Switching careers refunds all your career choices and costs {GOLD} gold.")
                .SetTextVariable("GOLD", cost).ToString(),
            new TextObject("{=taom_career_switch_confirm}Switch").ToString(),
            () => SwitchAndClose(newCareerId, cost),
            CanAfford(cost));
    }

    private void SwitchAndClose(string newCareerId, int cost)
    {
        var result = _switchService.SwitchCareer(_heroStringId, _heroAdapter, newCareerId);
        switch (result)
        {
            case CareerSwitchResult.Switched:
                _logger?.LogInfo($"CareerSystem: Player switched career to '{newCareerId}'");
                break;
            case CareerSwitchResult.NotEnoughGold:
                _logger?.LogWarning($"CareerSystem: SwitchCareer to '{newCareerId}' refused for hero '{_heroStringId}': not enough gold");
                ShowMessage(new TextObject("{=taom_career_switch_no_gold}You need {GOLD} gold to switch careers.")
                    .SetTextVariable("GOLD", cost).ToString());
                break;
            default:
                _logger?.LogWarning($"CareerSystem: SwitchCareer rejected '{newCareerId}' for hero '{_heroStringId}'");
                break;
        }

        CloseScreen();
    }

    private void OnRetrainRequested(int cost)
    {
        ShowConfirmation(
            new TextObject("{=taom_career_retrain_title}Retrain career").ToString(),
            new TextObject("{=taom_career_retrain_body}Refund every career choice except your first for {GOLD} gold? You keep your career and the tiers you unlocked.")
                .SetTextVariable("GOLD", cost).ToString(),
            new TextObject("{=taom_career_retrain_confirm}Retrain").ToString(),
            () => ConfirmRetrain(cost),
            CanAfford(cost));
    }

    private void ConfirmRetrain(int cost)
    {
        var result = _lifecycle.TryRetrain(_heroStringId, _heroLevel);
        _logger?.LogInfo($"CareerSystem: Retrain for hero '{_heroStringId}' -> {result}");
        switch (result)
        {
            case CareerRetrainResult.Done:
                _viewModel?.OnRetrained();
                break;
            case CareerRetrainResult.NotEnoughGold:
                ShowMessage(new TextObject("{=taom_career_retrain_no_gold}You need {GOLD} gold to retrain.")
                    .SetTextVariable("GOLD", cost).ToString());
                break;
        }
    }

    // Evaluated by the inquiry each time it draws, so the Confirm button dims while the purse is short.
    // The reason is vanilla's own string (PerkResetCampaignBehavior uses it), which ships translated.
    // The affordable branch returns no reason: SingleQueryPopUpVM.UpdateButtonEnabledStates shows any
    // non-empty reason as Confirm's hover hint, enabled or not.
    private Func<(bool, string)> CanAfford(int cost)
        => () => _purse.Gold >= cost
            ? (true, string.Empty)
            : (false, new TextObject("{=QOWyEJrm}You don't have enough denars.").ToString());

    private static void ShowConfirmation(
        string title, string body, string confirmText, Action onConfirm,
        Func<(bool, string)> isConfirmEnabled = null)
    {
        InformationManager.ShowInquiry(new InquiryData(
            title, body, true, true, confirmText, GameTexts.FindText("str_cancel").ToString(),
            onConfirm, null, isAffirmativeOptionEnabled: isConfirmEnabled), pauseGameActiveState: true);
    }

    private static void ShowMessage(string text)
        => InformationManager.DisplayMessage(new InformationMessage(text));

    protected override void OnFrameTick(float dt)
    {
        base.OnFrameTick(dt);

        if (_gauntletLayer.Input.IsKeyPressed(InputKey.Escape))
        {
            CloseScreen();
        }
    }

    private void CloseScreen()
    {
        // A late callback (an inquiry answered after Escape, say) must never pop whatever state is
        // on top now: PopState pops by level, not by identity.
        if (Game.Current?.GameStateManager?.ActiveState != _state) return;

        _logger?.LogInfo("CareerSystem: Closing career screen");
        _gauntletLayer?.InputRestrictions.ResetInputRestrictions();
        if (_movie != null)
            _gauntletLayer?.ReleaseMovie(_movie);
        _viewModel?.OnFinalize();
        Game.Current.GameStateManager.PopState();
    }

    protected override void OnFinalize()
    {
        base.OnFinalize();
        _viewModel = null;
        _gauntletLayer = null;
    }

    // IGameStateListener — required by GameStateScreenManager.OnCreateState
    // which registers null if the screen doesn't implement this interface.
    void IGameStateListener.OnInitialize() { }
    void IGameStateListener.OnFinalize() { }
    void IGameStateListener.OnActivate() { }
    void IGameStateListener.OnDeactivate() { }
}
