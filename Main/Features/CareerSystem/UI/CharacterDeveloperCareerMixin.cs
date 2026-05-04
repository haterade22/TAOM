using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.CharacterDeveloper;
using TaleWorlds.Library;
using TAOM.Core.Logging;

namespace TAOM.Features.CareerSystem.UI;

[ViewModelMixin("RefreshValues")]
internal class CharacterDeveloperCareerMixin : BaseViewModelMixin<CharacterDeveloperVM>
{
    // UIExtenderEx constructs mixins with only (parentVM); resolve services once at the boundary.
    private readonly ICareerDataService _dataService;
    private readonly IModLogger _logger;

    private bool _hasCareer;

    public CharacterDeveloperCareerMixin(CharacterDeveloperVM viewModel) : base(viewModel)
    {
        _dataService = IoC.Resolve<ICareerDataService>();
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
            return;
        }

        HasCareer = _dataService?.HasCareer(hero.StringId) ?? false;
        _logger?.LogDebug($"CareerSystem: RefreshCareerState — hero='{hero.StringId}' HasCareer={HasCareer}");
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

    public void ExecuteOpenCareerScreen()
    {
        _logger?.LogInfo("CareerSystem: ExecuteOpenCareerScreen triggered from character developer");

        // Close CharacterDeveloper first (TOR pattern) — prevents
        // GauntletMapBarGlobalLayer from ticking with invalid input context
        if (ViewModel is CharacterDeveloperVM charDevVM)
            charDevVM.ExecuteDone();

        GauntletCareerScreen.OpenCareerScreen();
    }
}
