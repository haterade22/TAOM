using System;
using TaleWorlds.Library;
using TAOM.Features.FactionUI.FactionScreen;

namespace TAOM.Features.FactionUI.UI.FactionScreen;

/// <summary>One faction tile in the ribbon, with its minimap marker (#704, Kysaro's).</summary>
public sealed class FactionItemVM : ViewModel
{
    private readonly Action<FactionInfo> _onSelect;
    private readonly Action<FactionInfo?> _onHover;
    private bool _isSelected;
    private bool _isHovered;

    public FactionItemVM(FactionInfo info, Action<FactionInfo> onSelect, Action<FactionInfo?> onHover, float cellWidth)
    {
        Info = info;
        _onSelect = onSelect;
        _onHover = onHover;
        ButtonWidth = cellWidth;
        TileSize = cellWidth * 0.84f;
        IconSize = TileSize * 0.7f;
        GlowSize = TileSize * 1.55f;
        MapMarginLeftBig = (float)(info.MapX * 952.0 - 5.0);
        MapMarginTopBig = (float)(info.MapY * 452.0 - 5.0);
    }

    public FactionInfo Info { get; }

    [DataSourceProperty] public float ButtonWidth { get; }

    [DataSourceProperty] public float TileSize { get; }

    [DataSourceProperty] public float IconSize { get; }

    [DataSourceProperty] public float GlowSize { get; }

    [DataSourceProperty] public float MapMarginLeftBig { get; }

    [DataSourceProperty] public float MapMarginTopBig { get; }

    [DataSourceProperty]
    public string MarkerColor => Info.Alignment switch
    {
        "free" => "#3ADB5AFF",
        "evil" => "#E23B3BFF",
        _ => "#F5E623FF",
    };

    [DataSourceProperty] public float FrameAlpha => _isSelected ? 1f : _isHovered ? 0.85f : 0.42f;

    [DataSourceProperty] public float IconAlpha => _isSelected ? 1f : _isHovered ? 0.95f : 0.62f;

    [DataSourceProperty] public string IconSprite => Info.IconSprite;

    [DataSourceProperty]
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (value == _isSelected)
                return;
            _isSelected = value;
            OnPropertyChangedWithValue(value, nameof(IsSelected));
            RefreshAlpha();
        }
    }

    public void ExecuteHoverBegin()
    {
        SetHovered(true);
        _onHover(Info);
    }

    public void ExecuteHoverEnd()
    {
        SetHovered(false);
        _onHover(null);
    }

    public void ExecuteSelectFaction() => _onSelect(Info);

    private void SetHovered(bool value)
    {
        if (value == _isHovered)
            return;
        _isHovered = value;
        RefreshAlpha();
    }

    private void RefreshAlpha()
    {
        OnPropertyChangedWithValue(FrameAlpha, nameof(FrameAlpha));
        OnPropertyChangedWithValue(IconAlpha, nameof(IconAlpha));
    }
}
