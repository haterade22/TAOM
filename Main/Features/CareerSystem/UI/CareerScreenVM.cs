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
    private bool _tier1Locked;
    private bool _tier2Locked;
    private bool _tier3Locked;
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
        string heroStringId,
        int heroLevel,
        Action onClose)
    {
        _dataService = dataService;
        _registry = registry;
        _passiveService = passiveService;
        try { _logger = IoC.Resolve<IModLogger>(); } catch { _logger = null; }
        _heroStringId = heroStringId;
        _heroLevel = heroLevel;
        _onClose = onClose;

        _screenTitle = "Career";
        _doneLbl = "Done";
        _abilityLabel = "Career Ability";
        _tier1Label = "Tier 1";
        _tier2Label = "Tier 2";
        _tier3Label = "Tier 3";

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

        AbilityName = new TextObject(career.AbilityTemplateId).ToString();
        AbilitySpriteName = $"CareerSystem\\Abilities\\{career.AbilityTemplateId}";
        HasAbilitySprite = !string.IsNullOrEmpty(career.AbilityTemplateId);

        var maxChoices = _registry.GetMaxChoicesForHero(_heroLevel);
        var currentChoices = _dataService.GetChoiceCount(_heroStringId);
        FreeCareerPoints = maxChoices - currentChoices;
        FreeCareerPointsText = $"Free Points: {FreeCareerPoints}";

        Tier1Locked = !_registry.IsTierAvailable(_heroLevel, 1);
        Tier2Locked = !_registry.IsTierAvailable(_heroLevel, 2);
        Tier3Locked = !_registry.IsTierAvailable(_heroLevel, 3);

        RebuildAbilityEffects(career);
        RebuildChoiceGroups(career);
    }

    private void RebuildAbilityEffects(CareerDefinition career)
    {
        _abilityEffects.Clear();
        if (!string.IsNullOrEmpty(career.Description))
        {
            // Root choice effects would go here if we had ability effect lines
            // For now this is a placeholder for the ability effects list
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
                groupVM.Choices.Add(new CareerChoiceObjectVM(choice, isTaken, isFreeToTake));
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
    public bool Tier1Locked
    {
        get => _tier1Locked;
        set { if (_tier1Locked != value) { _tier1Locked = value; OnPropertyChangedWithValue(value, nameof(Tier1Locked)); } }
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
