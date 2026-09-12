using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using TAOM.Features.CoopInterop;
using TaleWorlds.CampaignSystem.ViewModelCollection.Map.MapBar;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace TAOM.Features.TimeAcceleration.UI;

// Hooked on Tick rather than the default RefreshValues. MapTimeControlVM calls RefreshValues only
// from its constructor and on a gamepad state change (v1.4.8), so a lit state refreshed there never
// followed a key press or a click. MapBarVM.Tick calls MapTimeControlVM.Tick every frame, which is
// where vanilla recomputes its own TimeFlowState; OnRefresh below is therefore per-frame and must
// stay allocation-free.
[ViewModelMixin("Tick")]
[CoopSuppressedUi("BannerlordTogether owns campaign time under co-op")]
internal class TimeAccelerationMixin : BaseViewModelMixin<MapTimeControlVM>
{
    private readonly ITimeAccelerationService _service;
    private bool _isExtraFastForwardActive;
    private BasicTooltipViewModel _extraFastForwardHint;

    public TimeAccelerationMixin(MapTimeControlVM viewModel) : base(viewModel)
    {
        // Once per VM instance (one MapTimeControlVM per map screen), never per frame.
        _service = IoC.Resolve<ITimeAccelerationService>();

        // Phase 9b #168 P3 — localized tooltip. Was hardcoded English literal.
        _extraFastForwardHint = new BasicTooltipViewModel(
            () => new TextObject("{=taom_extra_fast_forward_hint}Extra Fast Forward").ToString());
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

    // #574 — the Extra Fast Forward button. It used to bind vanilla ExecuteTimeControlChange(2)
    // directly, which sets the MODE only; Campaign.TickMapTime scales real time by
    // SpeedUpMultiplier, so the button was vanilla fast-forward under a different tooltip whatever
    // the MCM slider said (#168 shipped that as "Option A"). The service writes the multiplier
    // first; the vanilla call after it keeps TimeFlowState and the _onTimeFlowStateChange callback
    // in step, and no-ops harmlessly when already fast-forwarding because the multiplier write is
    // what changes the pace. The parameter is the prefab's CommandParameter.Click, kept at "2" so
    // vanilla's own button needs only its Command.Click swapped.
    [DataSourceMethod]
    public void ExecuteExtraFastForward(int selectedTimeSpeed)
    {
        _service.EnterExtraFastForward();
        ViewModel?.ExecuteTimeControlChange(selectedTimeSpeed);
    }

    // Vanilla's own FastForward button is rebound to this (TimeAccelerationPrefab) so it puts the
    // NORMAL multiplier back. With vanilla's mode-only handler, one extra press left the extra value
    // in place until Space happened to restore it.
    [DataSourceMethod]
    public void ExecuteFastForward(int selectedTimeSpeed)
    {
        _service.EnterFastForward();
        ViewModel?.ExecuteTimeControlChange(selectedTimeSpeed);
    }

    public override void OnRefresh()
    {
        // Engine state, not a latch: a key press, a click and a vanilla toggle all land here.
        IsExtraFastForwardActive = _service.IsExtraFastForwardActive;
    }

    public override void OnFinalize() { }
}
