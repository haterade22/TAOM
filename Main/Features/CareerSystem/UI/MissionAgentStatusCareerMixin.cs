using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.ViewModelCollection;
using TAOM.Features.CareerSystem.Abilities;

namespace TAOM.Features.CareerSystem.UI;

// Issue #382 — data source for the career energy bar injected into the AgentStatus HUD
// (CareerEnergyBarPrefab). Hooked on the VM's per-frame Tick so the drain/refill animates.
// UIExtenderEx constructs mixins with only (parentVM); services resolve once at the
// boundary (same pattern as CharacterDeveloperCareerMixin). All state mapping lives in
// the unit-tested CareerEnergyBarStateMapper; this class only moves values across the
// VM boundary.
[ViewModelMixin("Tick")]
internal class MissionAgentStatusCareerMixin : BaseViewModelMixin<MissionAgentStatusVM>
{
    private readonly ICareerDataService _dataService;
    private readonly ICareerRegistry _registry;
    private readonly ICareerAbilityService _abilityService;
    private readonly IAbilityInputAdapter _input;

    private bool _isCareerBarVisible;
    private bool _isBarReady;
    private bool _isBarActive;
    private bool _isBarCooldown;
    private float _barFillWidth;
    private string _activationKeyText = "";
    private string _careerGlyphSprite = "";
    private bool _hasCareerGlyph;

    public MissionAgentStatusCareerMixin(MissionAgentStatusVM viewModel) : base(viewModel)
    {
        _dataService = IoC.Resolve<ICareerDataService>();
        _registry = IoC.Resolve<ICareerRegistry>();
        _abilityService = IoC.Resolve<ICareerAbilityService>();
        _input = IoC.Resolve<IAbilityInputAdapter>();
        ActivationKeyText = _input?.ActivationKeyName ?? "";
    }

    public override void OnRefresh()
    {
        var hero = Campaign.Current != null ? CharacterObject.PlayerCharacter?.HeroObject : null;
        if (hero == null)
        {
            HideBar();
            return;
        }

        // Same identity gate as CareerPerkMissionBehavior (Issue #377): the bar belongs to
        // the career hero, not to whichever soldier the player is controlling.
        var isControllingCareerHero =
            CareerHeroIdentityGate.IsCareerHeroAgent(Mission.Current?.MainAgent, hero);

        if (!isControllingCareerHero || !_dataService.HasCareer(hero.StringId))
        {
            HideBar();
            return;
        }

        var ability = _abilityService.GetOrCreateAbility(hero.StringId, _registry, _dataService);
        if (ability == null)
        {
            HideBar();
            return;
        }

        var state = CareerEnergyBarStateMapper.Map(ability);
        IsCareerBarVisible = true;
        IsBarReady = state.IsReady;
        IsBarActive = state.IsActive;
        IsBarCooldown = state.IsCooldown;
        BarFillWidth = state.Fill01 * CareerEnergyBarStateMapper.FillMaxWidth;

        var careerId = _dataService.GetCareerStringId(hero.StringId);
        var career = careerId != null ? _registry.GetCareer(careerId) : null;
        CareerGlyphSprite = career?.KeystoneIcon ?? "";
        HasCareerGlyph = !string.IsNullOrEmpty(CareerGlyphSprite);
    }

    private void HideBar()
    {
        IsCareerBarVisible = false;
        IsBarReady = false;
        IsBarActive = false;
        IsBarCooldown = false;
        BarFillWidth = 0f;
    }

    [DataSourceProperty]
    public bool IsCareerBarVisible
    {
        get => _isCareerBarVisible;
        set { if (_isCareerBarVisible != value) { _isCareerBarVisible = value; OnPropertyChanged(nameof(IsCareerBarVisible)); } }
    }

    [DataSourceProperty]
    public bool IsBarReady
    {
        get => _isBarReady;
        set { if (_isBarReady != value) { _isBarReady = value; OnPropertyChanged(nameof(IsBarReady)); } }
    }

    [DataSourceProperty]
    public bool IsBarActive
    {
        get => _isBarActive;
        set { if (_isBarActive != value) { _isBarActive = value; OnPropertyChanged(nameof(IsBarActive)); } }
    }

    [DataSourceProperty]
    public bool IsBarCooldown
    {
        get => _isBarCooldown;
        set { if (_isBarCooldown != value) { _isBarCooldown = value; OnPropertyChanged(nameof(IsBarCooldown)); } }
    }

    [DataSourceProperty]
    public float BarFillWidth
    {
        get => _barFillWidth;
        set { if (_barFillWidth != value) { _barFillWidth = value; OnPropertyChanged(nameof(BarFillWidth)); } }
    }

    [DataSourceProperty]
    public string ActivationKeyText
    {
        get => _activationKeyText;
        set { if (_activationKeyText != value) { _activationKeyText = value; OnPropertyChanged(nameof(ActivationKeyText)); } }
    }

    [DataSourceProperty]
    public string CareerGlyphSprite
    {
        get => _careerGlyphSprite;
        set { if (_careerGlyphSprite != value) { _careerGlyphSprite = value; OnPropertyChanged(nameof(CareerGlyphSprite)); } }
    }

    [DataSourceProperty]
    public bool HasCareerGlyph
    {
        get => _hasCareerGlyph;
        set { if (_hasCareerGlyph != value) { _hasCareerGlyph = value; OnPropertyChanged(nameof(HasCareerGlyph)); } }
    }
}
