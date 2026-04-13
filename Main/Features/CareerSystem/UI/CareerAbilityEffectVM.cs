using TaleWorlds.Library;

namespace TAOM.Features.CareerSystem.UI;

public class CareerAbilityEffectVM : ViewModel
{
    private string _lineText;

    public CareerAbilityEffectVM(string text)
    {
        _lineText = text;
    }

    [DataSourceProperty]
    public string LineText
    {
        get => _lineText;
        set
        {
            if (_lineText != value)
            {
                _lineText = value;
                OnPropertyChangedWithValue(value, nameof(LineText));
            }
        }
    }
}
