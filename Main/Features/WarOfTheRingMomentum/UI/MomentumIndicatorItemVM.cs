using System;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace TAOM.Features.WarOfTheRingMomentum.UI;

/// <summary>
/// The on-map slider item (MomentumMapIndicator.xml). SliderValue comes pre-signed
/// from the query facade (positive = Evil). Fixes LOTRAOM's event-subscription leak:
/// Unsubscribe() is called from the MapView's OnFinalize.
/// </summary>
public sealed class MomentumIndicatorItemVM : ViewModel
{
    private readonly IMomentumQueryService _query;
    private readonly IMomentumSettingsProvider _settings;
    private readonly Action _openPopup;

    private int _momentum;
    private bool _isIndicatorVisible;

    public MomentumIndicatorItemVM(IMomentumQueryService query, IMomentumSettingsProvider settings, Action openPopup)
    {
        _query = query;
        _settings = settings;
        _openPopup = openPopup;

        _query.MomentumChanged += OnMomentumChanged;
        OnMomentumChanged();
    }

    public void Unsubscribe() => _query.MomentumChanged -= OnMomentumChanged;

    public void OpenDetailedBalanceOfPowerView() => _openPopup?.Invoke();

    /// <summary>Re-evaluates slider + visibility; also polled by the MapView for MCM live-hide.</summary>
    public void OnMomentumChanged()
    {
        Momentum = _query.SliderValue;
        IsIndicatorVisible = _settings.ShowMapMeter && _query.HasWarStarted && !_query.HasWarEnded;
    }

    [DataSourceProperty]
    public string Text => new TextObject("{=taom_wotr_title}War of the Ring").ToString();

    [DataSourceProperty]
    public int Momentum
    {
        get => _momentum;
        set
        {
            if (value != _momentum)
            {
                _momentum = value;
                OnPropertyChangedWithValue(value, nameof(Momentum));
            }
        }
    }

    [DataSourceProperty]
    public bool IsIndicatorVisible
    {
        get => _isIndicatorVisible;
        set
        {
            if (value != _isIndicatorVisible)
            {
                _isIndicatorVisible = value;
                OnPropertyChangedWithValue(value, nameof(IsIndicatorVisible));
            }
        }
    }
}
