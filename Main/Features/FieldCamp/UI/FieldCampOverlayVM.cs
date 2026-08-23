using System;
using System.Collections.Generic;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TAOM.Adapters;
using TAOM.Features.FieldCamp.Domain;
using TAOM.Features.FieldCamp.Hooks;

namespace TAOM.Features.FieldCamp.UI;

/// <summary>
/// Data source for TaomFieldCampOverlay.xml: the always-on Make Camp button plus the
/// bottom-center status panel. All campaign state arrives through injected services (review #26
/// lesson: no IoC.Resolve inside a VM; <see cref="FieldCampMapView"/> resolves at its
/// engine-instantiated boundary and passes everything in).
///
/// <para>Ported from the source module's FieldCampOverlayVM with fixes: the button visibility
/// follows the MCM master toggle (the source hardcoded <c>ShowCampButton = true</c>, so a
/// disabled feature still showed a dead button), the dead bindings the source refreshed per frame
/// (CampTypeShort, IconVisible, IconX/IconY) are gone because nothing in the prefab binds them,
/// and the whole VM refreshes at 4 Hz from the MapView instead of per frame.</para>
///
/// <para>Build progress is read through <paramref name="buildProgress"/> rather than
/// <see cref="CampState.BuildProgress"/> directly: that property walks
/// <c>CampaignTime.Now -&gt; Campaign.Current</c>, which is null under unit tests, so the
/// delegate is the seam that keeps this VM constructible with mocks only.</para>
/// </summary>
public sealed class FieldCampOverlayVM : ViewModel
{
    /// <summary>Track width in the prefab; ProgressFillWidth is scaled onto 0..this.</summary>
    public const int BarMaxWidthPixels = 200;

    private readonly ICampService _camps;
    private readonly ICampSettingsProvider _settings;
    private readonly IGameMenuAdapter _menus;
    private readonly ICampMenuActivationQuery _activation;
    private readonly IReadOnlyList<ICampOverlayContributor> _contributors;
    private readonly Func<CampState, float> _buildProgress;

    private bool _isCampActive;
    private bool _showCampButton;
    private bool _canMakeCamp;
    private string _campButtonText = string.Empty;
    private string _campStatusText = string.Empty;
    private bool _progressVisible;
    private int _progressInt;
    private float _progressFillWidth;

    public FieldCampOverlayVM(
        ICampService camps,
        ICampSettingsProvider settings,
        IGameMenuAdapter menus,
        ICampMenuActivationQuery activation,
        IReadOnlyList<ICampOverlayContributor> contributors,
        Func<CampState, float> buildProgress)
    {
        _camps = camps;
        _settings = settings;
        _menus = menus;
        _activation = activation;
        _contributors = contributors ?? Array.Empty<ICampOverlayContributor>();
        _buildProgress = buildProgress;
    }

    [DataSourceProperty]
    public bool IsCampActive
    {
        get => _isCampActive;
        set
        {
            if (_isCampActive == value)
                return;
            _isCampActive = value;
            OnPropertyChangedWithValue(value, nameof(IsCampActive));
        }
    }

    [DataSourceProperty]
    public bool ShowCampButton
    {
        get => _showCampButton;
        set
        {
            if (_showCampButton == value)
                return;
            _showCampButton = value;
            OnPropertyChangedWithValue(value, nameof(ShowCampButton));
        }
    }

    [DataSourceProperty]
    public bool CanMakeCamp
    {
        get => _canMakeCamp;
        set
        {
            if (_canMakeCamp == value)
                return;
            _canMakeCamp = value;
            OnPropertyChangedWithValue(value, nameof(CanMakeCamp));
        }
    }

    [DataSourceProperty]
    public string CampButtonText
    {
        get => _campButtonText;
        set
        {
            if (_campButtonText == value)
                return;
            _campButtonText = value;
            OnPropertyChangedWithValue(value, nameof(CampButtonText));
        }
    }

    [DataSourceProperty]
    public string CampStatusText
    {
        get => _campStatusText;
        set
        {
            if (_campStatusText == value)
                return;
            _campStatusText = value;
            OnPropertyChangedWithValue(value, nameof(CampStatusText));
        }
    }

    [DataSourceProperty]
    public bool ProgressVisible
    {
        get => _progressVisible;
        set
        {
            if (_progressVisible == value)
                return;
            _progressVisible = value;
            OnPropertyChangedWithValue(value, nameof(ProgressVisible));
        }
    }

    /// <summary>
    /// 0..100, clamped. Deliberately NOT a [DataSourceProperty]: the prefab binds only the derived
    /// <see cref="ProgressFillWidth"/> (the source module bound neither yet notified both every
    /// frame). Setting this drives the fill width.
    /// </summary>
    public int ProgressInt
    {
        get => _progressInt;
        set
        {
            var clamped = value < 0 ? 0 : (value > 100 ? 100 : value);
            _progressInt = clamped;
            ProgressFillWidth = clamped * BarMaxWidthPixels / 100f;
        }
    }

    [DataSourceProperty]
    public float ProgressFillWidth
    {
        get => _progressFillWidth;
        set
        {
            if (_progressFillWidth == value)
                return;
            _progressFillWidth = value;
            OnPropertyChangedWithValue(value, nameof(ProgressFillWidth));
        }
    }

    /// <summary>Called by the MapView at 4 Hz; nothing else refreshes this VM.</summary>
    public void Refresh()
    {
        // FIX over source: the master toggle hides the button (the source hardcoded true).
        ShowCampButton = _settings.Enabled;
        CanMakeCamp = _settings.Enabled && MayOpenCampMenu();

        var camp = _camps.PlayerCamp;
        CampButtonText = ResolveCaption(camp);

        if (camp == null)
        {
            ApplyContributorStatus();
            return;
        }

        IsCampActive = true;
        var progress = _buildProgress(camp);
        if (progress < 1f)
        {
            CampStatusText = FieldCampTexts.RaisingLabel(camp.TypeEnum).ToString();
            ProgressInt = (int)(Clamp01(progress) * 100f);
            ProgressVisible = true;
        }
        else if (camp.Foraging)
        {
            CampStatusText = FieldCampTexts.ForagingStatus(camp.TypeEnum, camp.ForagedTotal).ToString();
            // The bar shows the accumulator toward the NEXT grain unit, same as the source.
            ProgressInt = (int)(Clamp01(camp.ForageAccumulator) * 100f);
            ProgressVisible = true;
        }
        else
        {
            CampStatusText = FieldCampTexts.TypeLabel(camp.TypeEnum).ToString();
            ProgressVisible = false;
        }
    }

    /// <summary>Prefab Command.Click. Re-checks every guard: the enabled state on the button is a
    /// 4 Hz snapshot, so the click itself must not trust it.</summary>
    public void ExecuteOpenCampMenu()
    {
        if (!_settings.Enabled)
            return;
        if (!MayOpenCampMenu())
            return;

        _menus.Activate(FieldCampCampaignBehavior.BaseMenuId);
    }

    private bool MayOpenCampMenu()
    {
        return _activation.IsMapScreenClear
            && _activation.IsMainPartyStationary
            && !_activation.IsMainPartyInSettlement
            && !_activation.IsMainPartyInEncounter
            && !_activation.IsMainPartyDisorganized;
    }

    private string ResolveCaption(CampState? camp)
    {
        // First contributor with a non-empty caption wins (Refuge renames the button while one of
        // its own structures stands here). Per-contributor try/catch: one broken contributor must
        // not blank the whole overlay - same containment the source's static delegate hook had.
        foreach (var contributor in _contributors)
        {
            try
            {
                var caption = contributor.CaptionOverride();
                if (!string.IsNullOrEmpty(caption))
                    return caption!;
            }
            catch
            {
                // Deliberate swallow: a contributor fault degrades to the default caption.
            }
        }

        return camp != null
            ? new TextObject("{=taom_fcamp_btn_options}Camp Options").ToString()
            : new TextObject("{=taom_fcamp_btn_make}Make Camp").ToString();
    }

    private void ApplyContributorStatus()
    {
        foreach (var contributor in _contributors)
        {
            CampOverlayStatus? status;
            try
            {
                status = contributor.OverlayStatus();
            }
            catch
            {
                // Same containment as ResolveCaption: skip the faulty contributor.
                continue;
            }

            if (status == null)
                continue;

            IsCampActive = true;
            CampStatusText = status.Value.Text ?? string.Empty;
            // Negative percent means "status line without a bar" (a standing refuge, not one
            // being raised); the contract struct has no separate visibility flag.
            var percent = status.Value.ProgressPercent;
            ProgressVisible = percent >= 0;
            if (percent >= 0)
                ProgressInt = percent;
            return;
        }

        IsCampActive = false;
        ProgressVisible = false;
    }

    private static float Clamp01(float value)
    {
        if (!(value > 0f))
            return 0f;
        return value > 1f ? 1f : value;
    }
}
