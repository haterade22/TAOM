using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.InputSystem;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.ScreenSystem;
using TAOM.Core.Logging;

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
    private readonly IModLogger _logger;
    private readonly string _heroStringId;
    private readonly int _heroLevel;

    public GauntletCareerScreen(CareerScreenGameState state)
    {
        var hero = Hero.MainHero;
        _dataService = IoC.Resolve<ICareerDataService>();
        _registry = IoC.Resolve<ICareerRegistry>();
        _passiveService = IoC.Resolve<ICareerPassiveService>();
        _configProvider = IoC.Resolve<ICareerConfigProvider>();
        _questService = IoC.Resolve<ICareerQuestService>();
        _logger = IoC.Resolve<IModLogger>();
        _heroStringId = hero?.StringId ?? "";
        _heroLevel = hero?.Level ?? 0;
    }

    public static void OpenCareerScreen()
    {
        var logger = IoC.Resolve<IModLogger>();
        logger?.LogInfo("CareerSystem: OpenCareerScreen called");

        var hero = Hero.MainHero;
        if (hero == null)
        {
            logger?.LogWarning("CareerSystem: OpenCareerScreen — MainHero is null");
            return;
        }

        logger?.LogInfo($"CareerSystem: Opening career screen for hero '{hero.StringId}' level={hero.Level}");
        var state = Game.Current.GameStateManager.CreateState<CareerScreenGameState>();
        Game.Current.GameStateManager.PushState(state);
    }

    protected override void OnInitialize()
    {
        base.OnInitialize();

        _gauntletLayer = new GauntletLayer("CareerScreen", 1);
        _gauntletLayer.IsFocusLayer = true;
        _viewModel = new CareerScreenVM(_dataService, _registry, _passiveService, _configProvider, _logger, _heroStringId, _heroLevel, CloseScreen, _questService);
        _movie = _gauntletLayer.LoadMovie("CareerScreen", _viewModel);
        _gauntletLayer.InputRestrictions.SetInputRestrictions();
        AddLayer(_gauntletLayer);
        ScreenManager.TrySetFocus(_gauntletLayer);
    }

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
