using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace TAOM.Features.FiefManagement.UI;

public class FiefManagementNavItemVM : ViewModel
{
    private string _displayText;
    private string _hotkeyText;

    public FiefManagementNavItemVM()
    {
        _displayText = new TextObject("{=taom_fief_nav_label}Fiefs").ToString();
        _hotkeyText = "F6";
    }

    [DataSourceProperty]
    public string DisplayText
    {
        get => _displayText;
        set
        {
            if (value == _displayText) return;
            _displayText = value;
            OnPropertyChangedWithValue(value, nameof(DisplayText));
        }
    }

    [DataSourceProperty]
    public string HotkeyText
    {
        get => _hotkeyText;
        set
        {
            if (value == _hotkeyText) return;
            _hotkeyText = value;
            OnPropertyChangedWithValue(value, nameof(HotkeyText));
        }
    }
}
