using System;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Library;
using TaleWorlds.TwoDimension;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Client.Adapters;

/// <summary>The view model of <c>SeatSelectBox.xml</c>: the selection rectangle, in real pixels.</summary>
public sealed class SeatSelectBoxVM : ViewModel
{
    private bool _isVisible;
    private float _offsetX;
    private float _offsetY;
    private float _boxWidth;
    private float _boxHeight;

    [DataSourceProperty]
    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (_isVisible == value)
                return;
            _isVisible = value;
            OnPropertyChangedWithValue(value, nameof(IsVisible));
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
    public float BoxWidth
    {
        get => _boxWidth;
        set
        {
            if (_boxWidth == value)
                return;
            _boxWidth = value;
            OnPropertyChangedWithValue(value, nameof(BoxWidth));
        }
    }

    [DataSourceProperty]
    public float BoxHeight
    {
        get => _boxHeight;
        set
        {
            if (_boxHeight == value)
                return;
            _boxHeight = value;
            OnPropertyChangedWithValue(value, nameof(BoxHeight));
        }
    }

    internal void Place(in SeatRect box)
    {
        OffsetX = box.Left;
        OffsetY = box.Top;
        BoxWidth = box.Width;
        BoxHeight = box.Height;
        IsVisible = true;
    }
}

/// <summary>
/// The selection rectangle on the Seat's cursor layer, loaded once when the layer is built and shown only while a box drag runs. A prefab that
/// fails to load leaves the box off for the mission and writes one error line; the drag still selects.
/// </summary>
internal sealed class SeatBoxView
{
    private const string PrefabName = "SeatSelectBox";

    private readonly Action<string> _logError;
    private SeatSelectBoxVM? _vm;
    private bool _failed;

    internal SeatBoxView(Action<string> logError) => _logError = logError;

    /// <summary>Whether the rectangle is on the screen.</summary>
    internal bool Shown => _vm is { IsVisible: true };

    /// <summary>Loads the movie on the layer, once; a missing prefab is logged and the box stays off.</summary>
    internal void Attach(GauntletLayer layer)
    {
        if (_vm != null || _failed)
            return;

        try
        {
            // LoadMovie keeps the movie even when the prefab lookup finds nothing, so a missing prefab leaves a movie with no prefab, and
            // releasing it at the screen's teardown throws (GauntletMovie.Release calls _moviePrefab.OnRelease()).
            if (UIResourceManager.WidgetFactory?.IsCustomType(PrefabName) != true)
            {
                _failed = true;
                _logError("[SeatOfSeeing][Mouse] the selection rectangle prefab is missing: box select works with no rectangle");
                return;
            }

            var vm = new SeatSelectBoxVM();
            layer.LoadMovie(PrefabName, vm);
            _vm = vm;
        }
        catch (Exception ex)
        {
            _failed = true;
            _logError($"[SeatOfSeeing][Mouse] the selection rectangle failed to load: {ex.Message}");
        }
    }

    internal void Show(in SeatRect box) => _vm?.Place(in box);

    internal void Hide()
    {
        if (_vm is { IsVisible: true } vm)
            vm.IsVisible = false;
    }

    internal void Drop()
    {
        _vm = null;
        _failed = true;
    }
}
