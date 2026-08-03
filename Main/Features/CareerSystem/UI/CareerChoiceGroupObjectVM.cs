using System.Linq;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem.UI;

public class CareerChoiceGroupObjectVM : ViewModel
{
    private readonly CareerChoiceGroupDefinition _group;
    private bool _isExpanded;
    private bool _isLocked;
    private bool _isActive;
    private bool _buttonsVisible;
    private MBBindingList<CareerChoiceObjectVM> _choices;

    public CareerChoiceGroupObjectVM(CareerChoiceGroupDefinition group, bool isLocked)
    {
        _group = group;
        _isLocked = isLocked;
        _isActive = !isLocked;
        _buttonsVisible = false;
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

        var next = _choices.FirstOrDefault(c => !c.IsTaken);
        if (next == null) return;
        // SelectChoice routes through the screen VM's TrySelectChoice, which already
        // refreshes the whole screen on success; no second refresh belongs here.
        next.SelectChoice();
    }

    public void ExecuteClickDecrease()
    {
        if (!_isActive) return;

        var last = _choices.LastOrDefault(c => c.IsTaken);
        if (last == null) return;
        last.DeSelectChoice();
    }

    [DataSourceProperty]
    public string GroupName => !string.IsNullOrEmpty(_group.DisplayName)
        ? new TextObject(_group.DisplayName).ToString()
        : HumanizeId(_group.Id);

    // Fallback when a group has no authored display_name yet: turn "ranger_of_ithilien_t1_a"
    // into "Path A" (or title-cased words if the id doesn't carry a "_t<N>_<letter>" suffix),
    // so headers are never raw ids even before lore names are authored.
    private static string HumanizeId(string id)
    {
        if (string.IsNullOrEmpty(id)) return "";
        var parts = id.Split('_');
        var last = parts[parts.Length - 1];
        if (parts.Length >= 2 && last.Length == 1 && char.IsLetter(last[0]))
        {
            var tierTok = parts[parts.Length - 2];
            if (tierTok.Length >= 2 && tierTok[0] == 't' && char.IsDigit(tierTok[1]))
                return "Path " + char.ToUpperInvariant(last[0]);
        }
        return string.Join(" ", System.Array.ConvertAll(parts, p =>
            p.Length == 0 ? p : char.ToUpperInvariant(p[0]) + p.Substring(1)));
    }

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
