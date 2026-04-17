using System;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem.UI;

public class CareerChoiceGroupObjectVM : ViewModel
{
    private readonly CareerChoiceGroupDefinition _group;
    private readonly Action _choiceChangedAction;
    private bool _isExpanded;
    private bool _isLocked;
    private bool _isActive;
    private bool _buttonsVisible;
    private MBBindingList<CareerChoiceObjectVM> _choices;

    public CareerChoiceGroupObjectVM(CareerChoiceGroupDefinition group, bool isLocked, Action choiceChangedAction = null)
    {
        _group = group;
        _isLocked = isLocked;
        _isActive = !isLocked;
        _buttonsVisible = false;
        _choiceChangedAction = choiceChangedAction;
        _choices = new MBBindingList<CareerChoiceObjectVM>();
    }

    public void ExecuteBeginHover()
    {
        ButtonsVisible = true;
    }

    public void ExecuteEndHover()
    {
        ButtonsVisible = false;
    }

    public void ExecuteClickIncrease()
    {
        if (!_isActive) return;

        for (int i = 0; i < _choices.Count; i++)
        {
            if (!_choices[i].IsTaken)
            {
                _choices[i].SelectChoice();
                _choiceChangedAction?.Invoke();
                return;
            }
        }
    }

    public void ExecuteClickDecrease()
    {
        if (!_isActive) return;

        for (int i = _choices.Count - 1; i >= 0; i--)
        {
            if (_choices[i].IsTaken)
            {
                _choices[i].DeSelectChoice();
                _choiceChangedAction?.Invoke();
                return;
            }
        }
    }

    [DataSourceProperty]
    public string GroupName => new TextObject(_group.Id).ToString();

    [DataSourceProperty]
    public int Tier => _group.Tier;

    [DataSourceProperty]
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded != value)
            {
                _isExpanded = value;
                OnPropertyChangedWithValue(value, nameof(IsExpanded));
            }
        }
    }

    [DataSourceProperty]
    public bool IsLocked
    {
        get => _isLocked;
        set
        {
            if (_isLocked != value)
            {
                _isLocked = value;
                OnPropertyChangedWithValue(value, nameof(IsLocked));
            }
        }
    }

    [DataSourceProperty]
    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive != value)
            {
                _isActive = value;
                OnPropertyChangedWithValue(value, nameof(IsActive));
            }
        }
    }

    [DataSourceProperty]
    public bool ButtonsVisible
    {
        get => _buttonsVisible;
        set
        {
            if (_buttonsVisible != value)
            {
                _buttonsVisible = value;
                OnPropertyChangedWithValue(value, nameof(ButtonsVisible));
            }
        }
    }

    [DataSourceProperty]
    public MBBindingList<CareerChoiceObjectVM> Choices
    {
        get => _choices;
        set
        {
            if (_choices != value)
            {
                _choices = value;
                OnPropertyChangedWithValue(value, nameof(Choices));
            }
        }
    }

    public void ExecuteToggleExpand()
    {
        if (!_isLocked)
            IsExpanded = !IsExpanded;
    }

    public string GroupId => _group.Id;
}
