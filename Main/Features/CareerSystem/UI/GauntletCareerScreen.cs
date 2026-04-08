using TaleWorlds.CampaignSystem;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.InputSystem;
using TaleWorlds.ScreenSystem;
using TAOM.Core.Logging;

namespace TAOM.Features.CareerSystem.UI;

public class GauntletCareerScreen : ScreenBase
{
    private GauntletLayer _gauntletLayer;
    private GauntletMovieIdentifier _movie;
    private CareerScreenVM _viewModel;

    private readonly ICareerDataService _dataService;
    private readonly ICareerRegistry _registry;
    private readonly ICareerPassiveService _passiveService;
    private readonly string _heroStringId;
    private readonly int _heroLevel;

    public GauntletCareerScreen(
        ICareerDataService dataService,
        ICareerRegistry registry,
        ICareerPassiveService passiveService,
        string heroStringId,
        int heroLevel)
    {
        _dataService = dataService;
        _registry = registry;
        _passiveService = passiveService;
        _heroStringId = heroStringId;
        _heroLevel = heroLevel;
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

        var dataService = IoC.Resolve<ICareerDataService>();
        var registry = IoC.Resolve<ICareerRegistry>();
        var passiveService = IoC.Resolve<ICareerPassiveService>();
        if (dataService == null || registry == null || passiveService == null)
        {
            logger?.LogError($"CareerSystem: OpenCareerScreen — service resolution failed");
            return;
        }

        logger?.LogInfo($"CareerSystem: Opening career screen for hero '{hero.StringId}' level={hero.Level}");
        ScreenManager.PushScreen(new GauntletCareerScreen(
            dataService, registry, passiveService, hero.StringId, hero.Level));
    }

    protected override void OnInitialize()
    {
        base.OnInitialize();

        _gauntletLayer = new GauntletLayer("CareerScreen", 1);
        _gauntletLayer.IsFocusLayer = true;
        _viewModel = new CareerScreenVM(_dataService, _registry, _passiveService, _heroStringId, _heroLevel, CloseScreen);
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
        IoC.Resolve<IModLogger>()?.LogInfo("CareerSystem: Closing career screen");
        _gauntletLayer?.InputRestrictions.ResetInputRestrictions();
        if (_movie != null)
            _gauntletLayer?.ReleaseMovie(_movie);
        _viewModel?.OnFinalize();
        ScreenManager.PopScreen();
    }

    protected override void OnFinalize()
    {
        base.OnFinalize();
        _viewModel = null;
        _gauntletLayer = null;
    }
}
