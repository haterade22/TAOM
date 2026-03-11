using TaleWorlds.Library;

namespace TAOM.Features.FactionMap.ViewModels;

public class FactionTraitItemVM : ViewModel
{
    private string _text;

    public FactionTraitItemVM(string text) { _text = text; }

    [DataSourceProperty]
    public string Text
    {
        get => _text;
        set { if (value != _text) { _text = value; OnPropertyChangedWithValue(value); } }
    }
}
