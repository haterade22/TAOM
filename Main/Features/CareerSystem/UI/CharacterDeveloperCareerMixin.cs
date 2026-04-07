using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.CharacterDeveloper;
using TaleWorlds.Library;

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

    [DataSourceMethod]
    public void ExecuteOpenCareerScreen()
    {
        GauntletCareerScreen.OpenCareerScreen();
    }
}
