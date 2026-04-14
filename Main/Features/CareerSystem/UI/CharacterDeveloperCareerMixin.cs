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
    private bool _hasCareer;

    public CharacterDeveloperCareerMixin(CharacterDeveloperVM viewModel) : base(viewModel)
    {
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

        var dataService = IoC.Resolve<ICareerDataService>();
        HasCareer = dataService?.HasCareer(hero.StringId) ?? false;
        IoC.Resolve<IModLogger>()?.LogDebug($"CareerSystem: RefreshCareerState — hero='{hero.StringId}' HasCareer={HasCareer}");
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
        IoC.Resolve<IModLogger>()?.LogInfo("CareerSystem: ExecuteOpenCareerScreen triggered from character developer");

        // Close CharacterDeveloper first (TOR pattern) — prevents
        // GauntletMapBarGlobalLayer from ticking with invalid input context
        if (ViewModel is CharacterDeveloperVM charDevVM)
            charDevVM.ExecuteDone();

        GauntletCareerScreen.OpenCareerScreen();
    }
}
