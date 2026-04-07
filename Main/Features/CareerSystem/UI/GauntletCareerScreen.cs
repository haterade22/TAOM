using TaleWorlds.CampaignSystem;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.ScreenSystem;

namespace TAOM.Features.CareerSystem.UI;

public class GauntletCareerScreen : GlobalLayer
{
    private GauntletLayer _gauntletLayer;
    private GauntletMovieIdentifier _movie;
    private CareerScreenVM _viewModel;

    public static void OpenCareerScreen()
    {
        var hero = Hero.MainHero;
        if (hero == null) return;

        var dataService = IoC.Resolve<ICareerDataService>();
        var registry = IoC.Resolve<ICareerRegistry>();
        var passiveService = IoC.Resolve<ICareerPassiveService>();
        if (dataService == null || registry == null || passiveService == null) return;

        var screen = new GauntletCareerScreen();
        screen.Initialize(dataService, registry, passiveService, hero.StringId, hero.Level);
        ScreenManager.AddGlobalLayer(screen, true);
    }

    private void Initialize(
        ICareerDataService dataService,
        ICareerRegistry registry,
        ICareerPassiveService passiveService,
        string heroStringId,
        int heroLevel)
    {
        _gauntletLayer = new GauntletLayer("CareerScreen", 100);
        _viewModel = new CareerScreenVM(dataService, registry, passiveService, heroStringId, heroLevel, Close);
        _movie = _gauntletLayer.LoadMovie("CareerScreen", _viewModel);
        _gauntletLayer.InputRestrictions.SetInputRestrictions();
        Layer = _gauntletLayer;
    }

    private void Close()
    {
        _gauntletLayer?.InputRestrictions.ResetInputRestrictions();
        if (_movie != null)
            _gauntletLayer?.ReleaseMovie(_movie);
        _viewModel?.OnFinalize();
        ScreenManager.RemoveGlobalLayer(this);
    }
}
