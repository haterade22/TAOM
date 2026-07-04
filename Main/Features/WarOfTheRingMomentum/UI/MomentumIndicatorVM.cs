using TaleWorlds.Library;

namespace TAOM.Features.WarOfTheRingMomentum.UI;

/// <summary>Movie root for MomentumMapIndicator.xml — wraps the item VM.</summary>
public sealed class MomentumIndicatorVM : ViewModel
{
    private MomentumIndicatorItemVM _momentumIndicator;

    public MomentumIndicatorVM(MomentumIndicatorItemVM item)
    {
        _momentumIndicator = item;
    }

    [DataSourceProperty]
    public MomentumIndicatorItemVM MomentumIndicator
    {
        get => _momentumIndicator;
        set
        {
            if (value != _momentumIndicator)
            {
                _momentumIndicator = value;
                OnPropertyChangedWithValue(value, nameof(MomentumIndicator));
            }
        }
    }
}
