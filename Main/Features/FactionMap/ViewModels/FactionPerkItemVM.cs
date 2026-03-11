using TaleWorlds.Library;

namespace TAOM.Features.FactionMap.ViewModels;

public class FactionPerkItemVM : ViewModel
{
    private string _perkName;
    private string _perkDescription;

    public FactionPerkItemVM(string name, string description)
    {
        _perkName = name;
        _perkDescription = description;
    }

    [DataSourceProperty]
    public string PerkName
    {
        get => _perkName;
        set { if (value != _perkName) { _perkName = value; OnPropertyChangedWithValue(value); } }
    }

    [DataSourceProperty]
    public string PerkDescription
    {
        get => _perkDescription;
        set { if (value != _perkDescription) { _perkDescription = value; OnPropertyChangedWithValue(value); } }
    }
}
