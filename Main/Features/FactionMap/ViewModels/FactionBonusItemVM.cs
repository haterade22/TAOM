using TaleWorlds.Library;

namespace TAOM.Features.FactionMap.ViewModels;

public class FactionBonusItemVM : ViewModel
{
    private string _text;
    private bool _isPositive;

    public FactionBonusItemVM(string text, bool isPositive)
    {
        _text = text;
        _isPositive = isPositive;
    }

    [DataSourceProperty]
    public string Text
    {
        get => _text;
        set { if (value != _text) { _text = value; OnPropertyChangedWithValue(value); } }
    }

    [DataSourceProperty]
    public bool IsPositive
    {
        get => _isPositive;
        set { if (value != _isPositive) { _isPositive = value; OnPropertyChangedWithValue(value); OnPropertyChanged(nameof(IsNegative)); } }
    }

    [DataSourceProperty]
    public bool IsNegative => !_isPositive;
}
