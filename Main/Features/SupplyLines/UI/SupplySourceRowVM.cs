using System;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TAOM.Core.Validation;

namespace TAOM.Features.SupplyLines.UI;

/// <summary>
/// One selectable source row (settlement or friendly lord). Selection is delegated to the
/// parent VM; the one-source-at-a-time lock arrives through <see cref="SetLocked"/> and only
/// affects <see cref="RowEnabled"/>, never the underlying eligibility.
/// </summary>
public sealed class SupplySourceRowVM : ViewModel
{
    private readonly Action<SupplySourceRowVM> _onSelected;
    private readonly string _displayName;
    private readonly string _distanceText;
    private bool _isSelected;
    private bool _locked;

    public SupplySourceRowVM(SupplySourceInfo info, Action<SupplySourceRowVM> onSelected)
    {
        Info = info;
        _onSelected = onSelected;

        // Engine-derived float: a non-finite or sentinel distance must not reach pricing. But a
        // sentinel is not "distance 0": quoting near-zero transport for a source the order
        // service will reject with no-route lied to the player (review round B), so an
        // unreachable row is DISABLED with the same no-route reason the confirm path uses, and
        // its distance renders as "?" instead of a fake 0.
        var distance = info?.Distance ?? 0f;
        bool reachable = FiniteFloatValidator.IsFiniteAtLeast(distance, 0f) && distance < float.MaxValue;
        Distance = reachable ? distance : 0f;
        _distanceText = reachable ? ((int)Math.Round(Distance)).ToString() : "?";
        CanOrder = (info?.CanOrder ?? false) && reachable;

        var name = info?.DisplayName;
        if (string.IsNullOrEmpty(name))
            name = "?";
        var relation = info?.RelationText ?? string.Empty;

        var template = CanOrder
            ? new TextObject("{=taom_sl_source_row}{NAME} ({RELATION})")
            : new TextObject("{=taom_sl_source_row_blocked}{NAME} ({RELATION}) - {REASON}");
        template.SetTextVariable("NAME", name);
        template.SetTextVariable("RELATION", relation);
        if (!CanOrder)
        {
            // An eligible-but-unreachable source reuses the placement failure's registered text
            // so the row explains itself with the same words the confirm would have used.
            var reason = info?.DisabledReason;
            if (string.IsNullOrEmpty(reason) && !reachable)
                reason = new TextObject(
                    "{=taom_sl_fail_no_route}There is no route from that source to you right now.").ToString();
            template.SetTextVariable("REASON", reason ?? string.Empty);
        }
        _displayName = template.ToString();
    }

    /// <summary>The service-built source record; the parent VM hands it back on confirm.</summary>
    public SupplySourceInfo Info { get; }

    /// <summary>Sanitized map distance, safe to feed into pricing.</summary>
    public float Distance { get; }

    public bool IsLord => !string.IsNullOrEmpty(Info?.HeroId);

    /// <summary>Eligibility fixed at build time: the source's own eligibility AND a reachable
    /// distance (the float.MaxValue sentinel disables the row instead of quoting from 0). Not a
    /// binding: the prefab drives the button through <see cref="RowEnabled"/> (the source
    /// module's bound CanOrder was dead).</summary>
    public bool CanOrder { get; }

    [DataSourceProperty]
    public bool RowEnabled => CanOrder && !_locked;

    [DataSourceProperty]
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChangedWithValue(value, nameof(IsSelected));
            }
        }
    }

    [DataSourceProperty]
    public string DisplayName => _displayName;

    [DataSourceProperty]
    public string DistanceText => _distanceText;

    public void ExecuteSelect()
    {
        if (CanOrder && !_locked)
            _onSelected?.Invoke(this);
    }

    public void SetLocked(bool locked)
    {
        if (_locked != locked)
        {
            _locked = locked;
            OnPropertyChanged(nameof(RowEnabled));
        }
    }
}
