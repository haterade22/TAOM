using TaleWorlds.Library;

namespace TAOM.Features.FactionMap.ViewModels;

public class FactionSpecialUnitItemVM : ViewModel
{
    private string _unitName;
    private string _unitDescription;

    public FactionSpecialUnitItemVM(string name, string description)
    {
        _unitName = name;
        _unitDescription = description;
    }

    [DataSourceProperty]
    public string UnitName
    {
        get => _unitName;
        set { if (value != _unitName) { _unitName = value; OnPropertyChangedWithValue(value); } }
    }

    [DataSourceProperty]
    public string UnitDescription
    {
        get => _unitDescription;
        set { if (value != _unitDescription) { _unitDescription = value; OnPropertyChangedWithValue(value); } }
    }
}
