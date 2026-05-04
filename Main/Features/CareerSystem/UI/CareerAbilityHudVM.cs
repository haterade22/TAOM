using TaleWorlds.Library;

namespace TAOM.Features.CareerSystem.UI;

public class CareerAbilityHudVM : ViewModel
{
    private bool _isVisible;
    private int _chargePercent;
    private string _abilityName;
    private string _abilitySprite;
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
    public string AbilitySprite
    {
        get => _abilitySprite;
        set
        {
            if (_abilitySprite != value)
            {
                _abilitySprite = value;
                OnPropertyChangedWithValue(value, nameof(AbilitySprite));
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

    public void Update(bool hasCareer, string abilityName, string abilitySprite, float progress01, bool isReady)
    {
        IsVisible = hasCareer;
        AbilityName = abilityName ?? "";
        AbilitySprite = abilitySprite ?? "";
        if (progress01 < 0f) progress01 = 0f;
        if (progress01 > 1f) progress01 = 1f;
        ChargePercent = (int)(progress01 * 100f);
        IsReady = isReady;
    }
}
