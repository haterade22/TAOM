using System;
using TaleWorlds.Library;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem.UI;

public class CareerScreenVM : ViewModel
{
    private readonly ICareerDataService _dataService;
    private readonly ICareerRegistry _registry;
    private readonly ICareerPassiveService _passiveService;
    private readonly string _heroStringId;
    private readonly int _heroLevel;
    private readonly Action _onClose;

    private string _careerName;
    private string _careerDescription;
    private string _careerPortraitSprite;
    private string _abilityName;
    private int _freeCareerPoints;
    private bool _hasCareer;
    private MBBindingList<CareerChoiceGroupObjectVM> _choiceGroupsTier1;
    private MBBindingList<CareerChoiceGroupObjectVM> _choiceGroupsTier2;
    private MBBindingList<CareerChoiceGroupObjectVM> _choiceGroupsTier3;

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
        _heroStringId = heroStringId;
        _heroLevel = heroLevel;
        _onClose = onClose;

        _choiceGroupsTier1 = new MBBindingList<CareerChoiceGroupObjectVM>();
        _choiceGroupsTier2 = new MBBindingList<CareerChoiceGroupObjectVM>();
        _choiceGroupsTier3 = new MBBindingList<CareerChoiceGroupObjectVM>();

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
        CareerName = career.DisplayName;
        CareerDescription = career.Description;
        CareerPortraitSprite = career.PortraitSprite;
        AbilityName = career.AbilityTemplateId;

        var maxChoices = _registry.GetMaxChoicesForHero(_heroLevel);
        var currentChoices = _dataService.GetChoiceCount(_heroStringId);
        FreeCareerPoints = maxChoices - currentChoices;

        RebuildChoiceGroups(career);
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
            var groupVM = new CareerChoiceGroupObjectVM(group, isLocked);

            var choices = _registry.GetChoicesForGroup(groupId);
            foreach (var choice in choices)
            {
                var isTaken = _dataService.GetOrCreateData(_heroStringId).HasChoice(choice.Id);
                var isFreeToTake = FreeCareerPoints > 0;
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
        if (FreeCareerPoints <= 0) return;

        var choice = _registry.GetChoice(choiceId);
        if (choice == null) return;

        // Tier gating: check hero level meets tier requirement
        if (!string.IsNullOrEmpty(choice.GroupId))
        {
            var group = _registry.GetGroup(choice.GroupId);
            if (group != null && !_registry.IsTierAvailable(_heroLevel, group.Tier))
                return;

            // Keystone exclusivity: only one keystone per tier
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
                                return; // Already has a keystone in this tier
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
    public int FreeCareerPoints
    {
        get => _freeCareerPoints;
        set { if (_freeCareerPoints != value) { _freeCareerPoints = value; OnPropertyChangedWithValue(value, nameof(FreeCareerPoints)); } }
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
}
