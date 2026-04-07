using TaleWorlds.Library;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem.UI;

public class CareerChoiceGroupObjectVM : ViewModel
{
    private readonly CareerChoiceGroupDefinition _group;
    private bool _isExpanded;
    private bool _isLocked;
    private MBBindingList<CareerChoiceObjectVM> _choices;

    public CareerChoiceGroupObjectVM(CareerChoiceGroupDefinition group, bool isLocked)
    {
        _group = group;
        _isLocked = isLocked;
        _choices = new MBBindingList<CareerChoiceObjectVM>();
    }

    [DataSourceProperty]
    public string GroupName => _group.Id;

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
