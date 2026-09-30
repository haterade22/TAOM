using TaleWorlds.Library;

namespace TAOM.Features.RealmBorders.UI;

/// <summary>
/// One lettered realm name. The widget stretches over the whole screen with its text centred, and
/// <see cref="OffsetX"/>/<see cref="OffsetY"/> shift that centre onto the projected map point in
/// real pixels (bound to ScaledPositionXOffset/YOffset), so the UI scale never enters the maths.
/// </summary>
public sealed class RealmNameItemVM : ViewModel
{
    private string _name = string.Empty;
    private float _offsetX, _offsetY, _alpha;
    private bool _useTolkienFont = true, _isShown;

    [DataSourceProperty]
    public string Name
    {
        get => _name;
        set
        {
            if (_name == value)
                return;
            _name = value;
            OnPropertyChangedWithValue(value, nameof(Name));
        }
    }

    [DataSourceProperty]
    public float OffsetX
    {
        get => _offsetX;
        set
        {
            if (_offsetX == value)
                return;
            _offsetX = value;
            OnPropertyChangedWithValue(value, nameof(OffsetX));
        }
    }

    [DataSourceProperty]
    public float OffsetY
    {
        get => _offsetY;
        set
        {
            if (_offsetY == value)
                return;
            _offsetY = value;
            OnPropertyChangedWithValue(value, nameof(OffsetY));
        }
    }

    [DataSourceProperty]
    public float Alpha
    {
        get => _alpha;
        set
        {
            if (_alpha == value)
                return;
            _alpha = value;
            OnPropertyChangedWithValue(value, nameof(Alpha));
        }
    }

    /// <summary>Not bound itself: it drives the two text widgets' visibility through ShowTolkien/ShowPlain.</summary>
    public bool IsShown
    {
        get => _isShown;
        set
        {
            if (_isShown == value)
                return;
            _isShown = value;
            OnPropertyChangedWithValue(ShowTolkien, nameof(ShowTolkien));
            OnPropertyChangedWithValue(ShowPlain, nameof(ShowPlain));
        }
    }

    public bool UseTolkienFont
    {
        get => _useTolkienFont;
        set
        {
            if (_useTolkienFont == value)
                return;
            _useTolkienFont = value;
            OnPropertyChangedWithValue(ShowTolkien, nameof(ShowTolkien));
            OnPropertyChangedWithValue(ShowPlain, nameof(ShowPlain));
        }
    }

    /// <summary>The aniron text widget: Latin and Cyrillic scripts it has glyphs for.</summary>
    [DataSourceProperty]
    public bool ShowTolkien => _isShown && _useTolkienFont;

    /// <summary>The plain-font text widget: Polish, Turkish, Chinese, Japanese, Korean.</summary>
    [DataSourceProperty]
    public bool ShowPlain => _isShown && !_useTolkienFont;
}
