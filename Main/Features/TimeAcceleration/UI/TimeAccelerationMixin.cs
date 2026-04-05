using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Map.MapBar;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;

namespace TAOM.Features.TimeAcceleration.UI;

[ViewModelMixin("RefreshValues")]
internal class TimeAccelerationMixin : BaseViewModelMixin<MapTimeControlVM>
{
    private bool _isExtraFastForwardActive;
    private BasicTooltipViewModel _extraFastForwardHint;

    public TimeAccelerationMixin(MapTimeControlVM viewModel) : base(viewModel)
    {
        _extraFastForwardHint = new BasicTooltipViewModel(() => "Extra Fast Forward (E)");
    }

    [DataSourceProperty]
    public bool IsExtraFastForwardActive
    {
        get => _isExtraFastForwardActive;
        set
        {
            if (_isExtraFastForwardActive != value)
            {
                _isExtraFastForwardActive = value;
                OnPropertyChanged(nameof(IsExtraFastForwardActive));
            }
        }
    }

    [DataSourceProperty]
    public BasicTooltipViewModel ExtraFastForwardHint
    {
        get => _extraFastForwardHint;
        set
        {
            if (_extraFastForwardHint != value)
            {
                _extraFastForwardHint = value;
                OnPropertyChanged(nameof(ExtraFastForwardHint));
            }
        }
    }

    public override void OnRefresh()
    {
        if (Campaign.Current == null)
            return;

        IsExtraFastForwardActive = Campaign.Current.SpeedUpMultiplier > 4f;
    }

    public override void OnFinalize() { }
}
