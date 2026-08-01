using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using TAOM.Features.CoopInterop;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Map.MapBar;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace TAOM.Features.TimeAcceleration.UI;

[ViewModelMixin("RefreshValues")]
[CoopSuppressedUi("BannerlordTogether owns campaign time under co-op")]
internal class TimeAccelerationMixin : BaseViewModelMixin<MapTimeControlVM>
{
    private bool _isExtraFastForwardActive;
    private BasicTooltipViewModel _extraFastForwardHint;

    public TimeAccelerationMixin(MapTimeControlVM viewModel) : base(viewModel)
    {
        // Phase 9b #168 P3 — localized tooltip. Was hardcoded English literal.
        _extraFastForwardHint = new BasicTooltipViewModel(
            () => new TextObject("{=taom_extra_fast_forward_hint}Extra Fast Forward (E)").ToString());
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

        // Phase 9b #168 P2 — watch the right field. Pre-fix the mixin read SpeedUpMultiplier (only
        // mutated by the `campaign.set_campaign_speed_multiplier` cheat console — vanilla play
        // never raises it), so IsExtraFastForwardActive was permanently false.
        //
        // The TAOM button's `CommandParameter.Click="2"` invokes vanilla
        // `MapTimeControlVM.ExecuteTimeControlChange(2)` which sets
        // `TimeControlMode = StoppableFastForward`. Watching that enum gives a working
        // selected-state visual.
        //
        // Known limitation (Option B from audit #168): this mirrors vanilla's FastForward speed —
        // the button is functionally redundant with vanilla's FastForwardButton. To deliver actual
        // EXTRA speed, the command handler would need to raise `SpeedUpMultiplier` via a service.
        // Tracked as future enhancement.
        IsExtraFastForwardActive = Campaign.Current.TimeControlMode == CampaignTimeControlMode.StoppableFastForward;
    }

    public override void OnFinalize() { }
}
