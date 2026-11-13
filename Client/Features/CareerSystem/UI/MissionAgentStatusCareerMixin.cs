using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.ViewModelCollection;
using TAOM.Features.BattleHud.UI;
using TAOM.Features.CareerSystem.Abilities;

namespace TAOM.Features.CareerSystem.UI;

// Issues #382 and #1095: the career disk at the centre of the circular battle HUD (injected by
// CareerEnergyBarPrefab into the AgentStatus HUD). Hooked on the VM's per-frame Tick so the drain and
// refill animate. UIExtenderEx constructs mixins with only (parentVM); services resolve once at the
// boundary. State mapping lives in the unit-tested CareerEnergyBarStateMapper and
// BattleHudStateMapper.MapCentre; this class only moves values across the VM boundary. Without a
// career ability (custom battle, a campaign hero with no career) every flag is off and the HUD shows
// a neutral dark centre with no glyph and no key.
[ViewModelMixin("Tick")]
internal class MissionAgentStatusCareerMixin : BaseViewModelMixin<MissionAgentStatusVM>
{
    private readonly ICareerDataService _dataService;
    private readonly ICareerRegistry _registry;
    private readonly ICareerAbilityService _abilityService;
    private readonly IAbilityInputAdapter _input;

    private bool _hasCareerAbility;
    private bool _isAbilityReady;
    private bool _isAbilityActive;
    private bool _isAbilityCooldown;
    private float _abilityFill;
    private string _activationKeyText = "";
    private string _careerGlyphSprite = "";
    private bool _hasCareerGlyph;
    private bool _hasActivationKey;

    public MissionAgentStatusCareerMixin(MissionAgentStatusVM viewModel) : base(viewModel)
    {
        _dataService = IoC.Resolve<ICareerDataService>();
        _registry = IoC.Resolve<ICareerRegistry>();
        _abilityService = IoC.Resolve<ICareerAbilityService>();
        _input = IoC.Resolve<IAbilityInputAdapter>();
    }

    public override void OnRefresh()
    {
        var hero = Campaign.Current != null ? CharacterObject.PlayerCharacter?.HeroObject : null;
        // Same identity gate as CareerPerkMissionBehavior (Issue #377): the disk belongs to the career
        // hero, not to whichever soldier the player is controlling.
        var mainAgent = Mission.Current?.MainAgent;
        var ability = hero != null && mainAgent != null && CareerHeroIdentityGate.IsCareerHeroAgent(mainAgent, hero)
            && _dataService.HasCareer(hero.StringId)
                ? _abilityService.GetOrCreateAbility(hero.StringId, _registry, _dataService)
                : null;

        // Read per refresh: the key is rebindable mid-mission from the Esc menu. The adapter caches the
        // GameKey, and the setters only notify when a value moves.
        var key = ability != null ? _input?.ActivationKeyName ?? "" : "";
        var centre = ability != null
            ? BattleHudStateMapper.MapCentre(true, CareerEnergyBarStateMapper.Map(ability), key)
            : default;

        HasCareerAbility = centre.HasAbility;
        IsAbilityReady = centre.IsReady;
        IsAbilityActive = centre.IsActive;
        IsAbilityCooldown = centre.IsCooldown;
        AbilityFill = centre.Fill01;
        ActivationKeyText = key;
        HasActivationKey = centre.ShowKey;

        var careerId = ability != null ? _dataService.GetCareerStringId(hero!.StringId) : null;
        var career = careerId != null ? _registry.GetCareer(careerId) : null;
        CareerGlyphSprite = career?.KeystoneIcon ?? "";
        HasCareerGlyph = !string.IsNullOrEmpty(CareerGlyphSprite);
    }

    [DataSourceProperty]
    public bool HasCareerAbility { get => _hasCareerAbility; set { if (_hasCareerAbility != value) { _hasCareerAbility = value; OnPropertyChanged(nameof(HasCareerAbility)); } } }

    [DataSourceProperty]
    public bool IsAbilityReady { get => _isAbilityReady; set { if (_isAbilityReady != value) { _isAbilityReady = value; OnPropertyChanged(nameof(IsAbilityReady)); } } }

    [DataSourceProperty]
    public bool IsAbilityActive { get => _isAbilityActive; set { if (_isAbilityActive != value) { _isAbilityActive = value; OnPropertyChanged(nameof(IsAbilityActive)); } } }

    [DataSourceProperty]
    public bool IsAbilityCooldown { get => _isAbilityCooldown; set { if (_isAbilityCooldown != value) { _isAbilityCooldown = value; OnPropertyChanged(nameof(IsAbilityCooldown)); } } }

    [DataSourceProperty]
    public float AbilityFill { get => _abilityFill; set { if (_abilityFill != value) { _abilityFill = value; OnPropertyChanged(nameof(AbilityFill)); } } }

    [DataSourceProperty]
    public string ActivationKeyText { get => _activationKeyText; set { if (_activationKeyText != value) { _activationKeyText = value; OnPropertyChanged(nameof(ActivationKeyText)); } } }

    [DataSourceProperty]
    public string CareerGlyphSprite { get => _careerGlyphSprite; set { if (_careerGlyphSprite != value) { _careerGlyphSprite = value; OnPropertyChanged(nameof(CareerGlyphSprite)); } } }

    [DataSourceProperty]
    public bool HasCareerGlyph { get => _hasCareerGlyph; set { if (_hasCareerGlyph != value) { _hasCareerGlyph = value; OnPropertyChanged(nameof(HasCareerGlyph)); } } }

    // Clearing the binding in Options is a supported state and empties the key text; the chip then hides.
    [DataSourceProperty]
    public bool HasActivationKey { get => _hasActivationKey; set { if (_hasActivationKey != value) { _hasActivationKey = value; OnPropertyChanged(nameof(HasActivationKey)); } } }
}
