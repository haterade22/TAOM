using System;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem.UI;

public class CareerScreenVM : ViewModel
{
    private readonly ICareerDataService _dataService;
    private readonly ICareerRegistry _registry;
    private readonly ICareerPassiveService _passiveService;
    private readonly ICareerConfigProvider _configProvider;
    private readonly IModLogger _logger;
    private readonly string _heroStringId;
    private readonly int _heroLevel;
    private readonly Action _onClose;

    private string _screenTitle;
    private string _doneLbl;
    private string _careerName;
    private string _careerDescription;
    private string _careerPortraitSprite;
    private string _abilityName;
    private string _abilitySpriteName;
    private string _abilityLabel;
    private string _freeCareerPointsText;
    private string _tier1Label;
    private string _tier2Label;
    private string _tier3Label;
    private bool _tier2Locked;
    private bool _tier3Locked;
    private string _tier2RequirementText;
    private string _tier3RequirementText;
    private int _freeCareerPoints;
    private bool _hasCareer;
    private bool _hasAbilitySprite;
    private MBBindingList<CareerChoiceGroupObjectVM> _choiceGroupsTier1;
    private MBBindingList<CareerChoiceGroupObjectVM> _choiceGroupsTier2;
    private MBBindingList<CareerChoiceGroupObjectVM> _choiceGroupsTier3;
    private MBBindingList<CareerAbilityEffectVM> _abilityEffects;

    public CareerScreenVM(
        ICareerDataService dataService,
        ICareerRegistry registry,
        ICareerPassiveService passiveService,
        ICareerConfigProvider configProvider,
        IModLogger logger,
        string heroStringId,
        int heroLevel,
        Action onClose)
    {
        _dataService = dataService;
        _registry = registry;
        _passiveService = passiveService;
        _configProvider = configProvider;
        _logger = logger;
        _heroStringId = heroStringId;
        _heroLevel = heroLevel;
        _onClose = onClose;

        _screenTitle = new TextObject("{=taom_career_screen_title}Career").ToString();
        _doneLbl = new TextObject("{=taom_career_done}Done").ToString();
        _abilityLabel = new TextObject("{=taom_career_ability_label}Career Ability").ToString();
        _tier1Label = new TextObject("{=taom_career_tier1}Tier 1").ToString();
        _tier2Label = new TextObject("{=taom_career_tier2}Tier 2").ToString();
        _tier3Label = new TextObject("{=taom_career_tier3}Tier 3").ToString();

        _choiceGroupsTier1 = new MBBindingList<CareerChoiceGroupObjectVM>();
        _choiceGroupsTier2 = new MBBindingList<CareerChoiceGroupObjectVM>();
        _choiceGroupsTier3 = new MBBindingList<CareerChoiceGroupObjectVM>();
        _abilityEffects = new MBBindingList<CareerAbilityEffectVM>();

        RefreshValues();
    }

    public override void RefreshValues()
    {
        base.RefreshValues();

        var careerId = _dataService.GetCareerStringId(_heroStringId);
        if (string.IsNullOrEmpty(careerId))
        {
            HasCareer = false;
            return;
        }

        var career = _registry.GetCareer(careerId);
        if (career == null)
        {
            HasCareer = false;
            return;
        }

        HasCareer = true;
        CareerName = new TextObject(career.DisplayName).ToString();
        CareerDescription = new TextObject(career.Description).ToString();
        CareerPortraitSprite = $"CareerSystem\\Portraits\\{career.PortraitSprite}";

        // Tier headers show the career's per-tier RANK title (e.g. "Captain of Ithilien") when
        // authored, falling back to the generic "Tier N" label. (Adopted from TOR_Core.)
        Tier1Label = !string.IsNullOrEmpty(career.Rank1Name)
            ? new TextObject(career.Rank1Name).ToString()
            : new TextObject("{=taom_career_tier1}Tier 1").ToString();
        Tier2Label = !string.IsNullOrEmpty(career.Rank2Name)
            ? new TextObject(career.Rank2Name).ToString()
            : new TextObject("{=taom_career_tier2}Tier 2").ToString();
        Tier3Label = !string.IsNullOrEmpty(career.Rank3Name)
            ? new TextObject(career.Rank3Name).ToString()
            : new TextObject("{=taom_career_tier3}Tier 3").ToString();

        var abilityTemplate = _configProvider?.GetAbilityTemplate(career.AbilityTemplateId);
        AbilityName = abilityTemplate != null
            ? new TextObject(abilityTemplate.DisplayName).ToString()
            : new TextObject(career.AbilityTemplateId).ToString();
        AbilitySpriteName = $"CareerSystem\\Abilities\\{career.AbilityTemplateId}";
        HasAbilitySprite = !string.IsNullOrEmpty(career.AbilityTemplateId);

        var maxChoices = _registry.GetMaxChoicesForHero(_heroLevel);
        var currentChoices = _dataService.GetChoiceCount(_heroStringId);
        FreeCareerPoints = System.Math.Max(0, maxChoices - currentChoices);
        FreeCareerPointsText = new TextObject("{=taom_career_free_points}Free Points: {COUNT}")
            .SetTextVariable("COUNT", FreeCareerPoints).ToString();

        Tier2Locked = !_registry.IsTierAvailable(_heroLevel, 2);
        Tier3Locked = !_registry.IsTierAvailable(_heroLevel, 3);

        // Locked tiers show a "Requires Level N" label (level sourced from the registry, the single
        // source of truth) in place of the old stretched gate art.
        Tier2RequirementText = new TextObject("{=taom_career_tier_requirement}Requires Level {LEVEL}")
            .SetTextVariable("LEVEL", _registry.GetTierUnlockLevel(2)).ToString();
        Tier3RequirementText = new TextObject("{=taom_career_tier_requirement}Requires Level {LEVEL}")
            .SetTextVariable("LEVEL", _registry.GetTierUnlockLevel(3)).ToString();

        RebuildAbilityEffects(career);
        RebuildChoiceGroups(career);
    }

    private void RebuildAbilityEffects(CareerDefinition career)
    {
        _abilityEffects.Clear();

        var abilityTemplate = _configProvider?.GetAbilityTemplate(career.AbilityTemplateId);
        if (abilityTemplate != null && !string.IsNullOrEmpty(abilityTemplate.TooltipDescription))
        {
            var description = new TextObject(abilityTemplate.TooltipDescription).ToString();
            _abilityEffects.Add(new CareerAbilityEffectVM(description));
        }
    }

    private void RebuildChoiceGroups(CareerDefinition career)
    {
        _choiceGroupsTier1.Clear();
        _choiceGroupsTier2.Clear();
        _choiceGroupsTier3.Clear();

        foreach (var groupId in career.ChoiceGroupIds)
        {
            var group = _registry.GetGroup(groupId);
            if (group == null) continue;

            var isLocked = !_registry.IsTierAvailable(_heroLevel, group.Tier);
            var groupVM = new CareerChoiceGroupObjectVM(group, isLocked, () => RefreshValues());

            var choices = _registry.GetChoicesForGroup(groupId);
            foreach (var choice in choices)
            {
                var isTaken = _dataService.GetOrCreateData(_heroStringId).HasChoice(choice.Id);
                var isFreeToTake = FreeCareerPoints > 0 && !isLocked;
                groupVM.Choices.Add(new CareerChoiceObjectVM(choice, isTaken, isFreeToTake, TrySelectChoice, TryDeselectChoice));
            }

            switch (group.Tier)
            {
                case 1: _choiceGroupsTier1.Add(groupVM); break;
                case 2: _choiceGroupsTier2.Add(groupVM); break;
                case 3: _choiceGroupsTier3.Add(groupVM); break;
            }
        }
    }

    public void ExecuteSelectChoice(string choiceId)
    {
        _logger?.LogInfo($"CareerSystem: ExecuteSelectChoice — choiceId='{choiceId}' freePoints={FreeCareerPoints}");
        if (FreeCareerPoints <= 0) return;

        var choice = _registry.GetChoice(choiceId);
        if (choice == null) return;

        if (!string.IsNullOrEmpty(choice.GroupId))
        {
            var group = _registry.GetGroup(choice.GroupId);
            if (group != null && !_registry.IsTierAvailable(_heroLevel, group.Tier))
                return;

            if (choice.Type == Domain.ChoiceType.Keystone && group != null)
            {
                var heroData = _dataService.GetOrCreateData(_heroStringId);
                var careerId = _dataService.GetCareerStringId(_heroStringId);
                var career = careerId != null ? _registry.GetCareer(careerId) : null;
                if (career != null)
                {
                    foreach (var gId in career.ChoiceGroupIds)
                    {
                        var otherGroup = _registry.GetGroup(gId);
                        if (otherGroup == null || otherGroup.Tier != group.Tier) continue;
                        var otherChoices = _registry.GetChoicesForGroup(gId);
                        foreach (var oc in otherChoices)
                        {
                            if (oc.Type == Domain.ChoiceType.Keystone && heroData.HasChoice(oc.Id))
                                return;
                        }
                    }
                }
            }
        }

        var maxChoices = _registry.GetMaxChoicesForHero(_heroLevel);
        if (_dataService.TryAddChoice(_heroStringId, choiceId, maxChoices))
        {
            _passiveService.RefreshCache(_dataService, _registry);
            RefreshValues();
        }
    }

    // Invoked by CareerChoiceObjectVM.SelectChoice via callback (+ button in UI)
    private bool TrySelectChoice(string choiceId)
    {
        if (FreeCareerPoints <= 0) return false;

        var choice = _registry.GetChoice(choiceId);
        if (choice == null) return false;

        if (!string.IsNullOrEmpty(choice.GroupId))
        {
            var group = _registry.GetGroup(choice.GroupId);
            if (group != null && !_registry.IsTierAvailable(_heroLevel, group.Tier))
                return false;

            // Enforce one Keystone per tier
            if (choice.Type == Domain.ChoiceType.Keystone && group != null)
            {
                var heroData = _dataService.GetOrCreateData(_heroStringId);
                var careerId = _dataService.GetCareerStringId(_heroStringId);
                var career = careerId != null ? _registry.GetCareer(careerId) : null;
                if (career != null)
                {
                    foreach (var gId in career.ChoiceGroupIds)
                    {
                        var otherGroup = _registry.GetGroup(gId);
                        if (otherGroup == null || otherGroup.Tier != group.Tier) continue;
                        foreach (var oc in _registry.GetChoicesForGroup(gId))
                        {
                            if (oc.Type == Domain.ChoiceType.Keystone && heroData.HasChoice(oc.Id))
                                return false;
                        }
                    }
                }
            }
        }

        var maxChoices = _registry.GetMaxChoicesForHero(_heroLevel);
        if (_dataService.TryAddChoice(_heroStringId, choiceId, maxChoices))
        {
            _passiveService.RefreshCache(_dataService, _registry);
            RefreshValues();
            return true;
        }
        return false;
    }

    // Invoked by CareerChoiceObjectVM.DeSelectChoice via callback (- button in UI)
    private bool TryDeselectChoice(string choiceId)
    {
        var heroData = _dataService.GetOrCreateData(_heroStringId);
        if (!heroData.HasChoice(choiceId)) return false;

        _dataService.RemoveChoice(_heroStringId, choiceId);
        _passiveService.RefreshCache(_dataService, _registry);
        RefreshValues();
        return true;
    }

    public void ExecuteClose()
    {
        _onClose?.Invoke();
    }

    // ── DataSource Properties ──

    [DataSourceProperty]
    public string ScreenTitle
    {
        get => _screenTitle;
        set { if (_screenTitle != value) { _screenTitle = value; OnPropertyChangedWithValue(value, nameof(ScreenTitle)); } }
    }

    [DataSourceProperty]
    public string DoneLbl
    {
        get => _doneLbl;
        set { if (_doneLbl != value) { _doneLbl = value; OnPropertyChangedWithValue(value, nameof(DoneLbl)); } }
    }

    [DataSourceProperty]
    public bool HasCareer
    {
        get => _hasCareer;
        set { if (_hasCareer != value) { _hasCareer = value; OnPropertyChangedWithValue(value, nameof(HasCareer)); } }
    }

    [DataSourceProperty]
    public string CareerName
    {
        get => _careerName;
        set { if (_careerName != value) { _careerName = value; OnPropertyChangedWithValue(value, nameof(CareerName)); } }
    }

    [DataSourceProperty]
    public string CareerDescription
    {
        get => _careerDescription;
        set { if (_careerDescription != value) { _careerDescription = value; OnPropertyChangedWithValue(value, nameof(CareerDescription)); } }
    }

    [DataSourceProperty]
    public string CareerPortraitSprite
    {
        get => _careerPortraitSprite;
        set { if (_careerPortraitSprite != value) { _careerPortraitSprite = value; OnPropertyChangedWithValue(value, nameof(CareerPortraitSprite)); } }
    }

    [DataSourceProperty]
    public string AbilityName
    {
        get => _abilityName;
        set { if (_abilityName != value) { _abilityName = value; OnPropertyChangedWithValue(value, nameof(AbilityName)); } }
    }

    [DataSourceProperty]
    public string AbilitySpriteName
    {
        get => _abilitySpriteName;
        set { if (_abilitySpriteName != value) { _abilitySpriteName = value; OnPropertyChangedWithValue(value, nameof(AbilitySpriteName)); } }
    }

    [DataSourceProperty]
    public string AbilityLabel
    {
        get => _abilityLabel;
        set { if (_abilityLabel != value) { _abilityLabel = value; OnPropertyChangedWithValue(value, nameof(AbilityLabel)); } }
    }

    [DataSourceProperty]
    public bool HasAbilitySprite
    {
        get => _hasAbilitySprite;
        set { if (_hasAbilitySprite != value) { _hasAbilitySprite = value; OnPropertyChangedWithValue(value, nameof(HasAbilitySprite)); } }
    }

    [DataSourceProperty]
    public int FreeCareerPoints
    {
        get => _freeCareerPoints;
        set { if (_freeCareerPoints != value) { _freeCareerPoints = value; OnPropertyChangedWithValue(value, nameof(FreeCareerPoints)); } }
    }

    [DataSourceProperty]
    public string FreeCareerPointsText
    {
        get => _freeCareerPointsText;
        set { if (_freeCareerPointsText != value) { _freeCareerPointsText = value; OnPropertyChangedWithValue(value, nameof(FreeCareerPointsText)); } }
    }

    [DataSourceProperty]
    public string Tier1Label
    {
        get => _tier1Label;
        set { if (_tier1Label != value) { _tier1Label = value; OnPropertyChangedWithValue(value, nameof(Tier1Label)); } }
    }

    [DataSourceProperty]
    public string Tier2Label
    {
        get => _tier2Label;
        set { if (_tier2Label != value) { _tier2Label = value; OnPropertyChangedWithValue(value, nameof(Tier2Label)); } }
    }

    [DataSourceProperty]
    public string Tier3Label
    {
        get => _tier3Label;
        set { if (_tier3Label != value) { _tier3Label = value; OnPropertyChangedWithValue(value, nameof(Tier3Label)); } }
    }

    [DataSourceProperty]
    public bool Tier2Locked
    {
        get => _tier2Locked;
        set { if (_tier2Locked != value) { _tier2Locked = value; OnPropertyChangedWithValue(value, nameof(Tier2Locked)); } }
    }

    [DataSourceProperty]
    public bool Tier3Locked
    {
        get => _tier3Locked;
        set { if (_tier3Locked != value) { _tier3Locked = value; OnPropertyChangedWithValue(value, nameof(Tier3Locked)); } }
    }

    [DataSourceProperty]
    public string Tier2RequirementText
    {
        get => _tier2RequirementText;
        set { if (_tier2RequirementText != value) { _tier2RequirementText = value; OnPropertyChangedWithValue(value, nameof(Tier2RequirementText)); } }
    }

    [DataSourceProperty]
    public string Tier3RequirementText
    {
        get => _tier3RequirementText;
        set { if (_tier3RequirementText != value) { _tier3RequirementText = value; OnPropertyChangedWithValue(value, nameof(Tier3RequirementText)); } }
    }

    [DataSourceProperty]
    public MBBindingList<CareerChoiceGroupObjectVM> ChoiceGroupsTier1
    {
        get => _choiceGroupsTier1;
        set { if (_choiceGroupsTier1 != value) { _choiceGroupsTier1 = value; OnPropertyChangedWithValue(value, nameof(ChoiceGroupsTier1)); } }
    }

    [DataSourceProperty]
    public MBBindingList<CareerChoiceGroupObjectVM> ChoiceGroupsTier2
    {
        get => _choiceGroupsTier2;
        set { if (_choiceGroupsTier2 != value) { _choiceGroupsTier2 = value; OnPropertyChangedWithValue(value, nameof(ChoiceGroupsTier2)); } }
    }

    [DataSourceProperty]
    public MBBindingList<CareerChoiceGroupObjectVM> ChoiceGroupsTier3
    {
        get => _choiceGroupsTier3;
        set { if (_choiceGroupsTier3 != value) { _choiceGroupsTier3 = value; OnPropertyChangedWithValue(value, nameof(ChoiceGroupsTier3)); } }
    }

    [DataSourceProperty]
    public MBBindingList<CareerAbilityEffectVM> AbilityEffects
    {
        get => _abilityEffects;
        set { if (_abilityEffects != value) { _abilityEffects = value; OnPropertyChangedWithValue(value, nameof(AbilityEffects)); } }
    }
}
