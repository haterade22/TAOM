using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.CharacterDeveloper;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade.View;
using TAOM.Core.Logging;

namespace TAOM.Features.CareerSystem.UI;

[ViewModelMixin("RefreshValues")]
internal class CharacterDeveloperCareerMixin : BaseViewModelMixin<CharacterDeveloperVM>
{
    // UIExtenderEx constructs mixins with only (parentVM); resolve services once at the boundary.
    private readonly ICareerDataService _dataService;
    private readonly ICareerRegistry _registry;
    private readonly IModLogger _logger;

    private bool _hasCareer;
    private bool _hasUnspentPoints;
    private string _unspentPointsText = "";

    public CharacterDeveloperCareerMixin(CharacterDeveloperVM viewModel) : base(viewModel)
    {
        _dataService = IoC.Resolve<ICareerDataService>();
        _registry = IoC.Resolve<ICareerRegistry>();
        _logger = IoC.Resolve<IModLogger>();
        RefreshCareerState();
    }

    public override void OnRefresh()
    {
        RefreshCareerState();
    }

    private void RefreshCareerState()
    {
        var hero = Hero.MainHero;
        if (hero == null)
        {
            HasCareer = false;
            HasUnspentPoints = false;
            return;
        }

        HasCareer = _dataService?.HasCareer(hero.StringId) ?? false;

        // Issue #379 — badge state comes from the services, NOT career-screen state: the
        // badge exists to prompt a player who has never opened that screen.
        var unspent = HasCareer && _registry != null && _dataService != null
            ? _registry.GetUnspentPoints(hero.Level, _dataService.GetChoiceCount(hero.StringId))
            : 0;
        HasUnspentPoints = unspent > 0;
        UnspentPointsText = unspent > 0 ? unspent.ToString() : "";

        _logger?.LogDebug($"CareerSystem: RefreshCareerState — hero='{hero.StringId}' HasCareer={HasCareer} unspent={unspent}");
    }

    [DataSourceProperty]
    public bool HasCareer
    {
        get => _hasCareer;
        set
        {
            if (_hasCareer != value)
            {
                _hasCareer = value;
                OnPropertyChanged(nameof(HasCareer));
            }
        }
    }

    [DataSourceProperty]
    public bool HasUnspentPoints
    {
        get => _hasUnspentPoints;
        set
        {
            if (_hasUnspentPoints != value)
            {
                _hasUnspentPoints = value;
                OnPropertyChanged(nameof(HasUnspentPoints));
            }
        }
    }

    [DataSourceProperty]
    public string UnspentPointsText
    {
        get => _unspentPointsText;
        set
        {
            if (_unspentPointsText != value)
            {
                _unspentPointsText = value;
                OnPropertyChanged(nameof(UnspentPointsText));
            }
        }
    }

    public void ExecuteOpenCareerScreen()
    {
        _logger?.LogInfo("CareerSystem: ExecuteOpenCareerScreen triggered from character developer");

        // Issue #378 — audible click. Same idiom as GauntletFiefManagementScreen:111;
        // UISoundsHelper lives in TaleWorlds.MountAndBlade.View (Native module DLL).
        UISoundsHelper.PlayUISound("event:/ui/default");

        // Close CharacterDeveloper first — prevents
        // GauntletMapBarGlobalLayer from ticking with invalid input context
        if (ViewModel is CharacterDeveloperVM charDevVM)
            charDevVM.ExecuteDone();

        GauntletCareerScreen.OpenCareerScreen();
    }
}
