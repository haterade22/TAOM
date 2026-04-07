using TaleWorlds.Library;

namespace TAOM.Features.CareerSystem.UI;

public class CareerAbilityHudVM : ViewModel
{
    private bool _isVisible;
    private int _chargePercent;
    private string _abilityName;
    private bool _isReady;

    [DataSourceProperty]
    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (_isVisible != value)
            {
                _isVisible = value;
                OnPropertyChangedWithValue(value, nameof(IsVisible));
            }
        }
    }

    [DataSourceProperty]
    public int ChargePercent
    {
        get => _chargePercent;
        set
        {
            if (_chargePercent != value)
            {
                _chargePercent = value;
                OnPropertyChangedWithValue(value, nameof(ChargePercent));
            }
        }
    }

    [DataSourceProperty]
    public string AbilityName
    {
        get => _abilityName;
        set
        {
            if (_abilityName != value)
            {
                _abilityName = value;
                OnPropertyChangedWithValue(value, nameof(AbilityName));
            }
        }
    }

    [DataSourceProperty]
    public bool IsReady
    {
        get => _isReady;
        set
        {
            if (_isReady != value)
            {
                _isReady = value;
                OnPropertyChangedWithValue(value, nameof(IsReady));
            }
        }
    }

    public void Update(bool hasCareer, string abilityName, float currentCharge, float maxCharge, bool isReady)
    {
        IsVisible = hasCareer;
        AbilityName = abilityName ?? "";
        ChargePercent = maxCharge > 0 ? (int)(currentCharge / maxCharge * 100f) : 0;
        IsReady = isReady;
    }
}
