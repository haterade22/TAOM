using System;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace TAOM.Features.FactionUI.Campaign;

/// <summary>
/// One banner layer: a row in the layer list and, while selected, the canvas's editable area. The canvas widget
/// (BannerBuilderEditableAreaWidget) writes PositionValue, SizeValue and RotationValue every drag frame and fires
/// ExecuteUpdateBanner on release, so those setters only move the BannerData and the release commits. The slider
/// values commit as they change, as their sliders send one value on release.
/// </summary>
public sealed class TaomBannerLayerVM : ViewModel
{
    private readonly TaomBannerEditorVM _owner;
    private Vec2 _position, _size;
    private float _rotation;
    private bool _isSelected, _isMirrorActive, _isDrawStrokeActive;
    private string _iconId = "";
    private Color _color1, _color2;

    internal TaomBannerLayerVM(TaomBannerEditorVM owner, BannerData data, int index)
    {
        _owner = owner;
        Data = data;
        LayerIndex = index;
        _position = data.Position;
        _size = data.Size;
        _rotation = data.RotationValue;
        _isMirrorActive = data.Mirror;
        _isDrawStrokeActive = data.DrawStroke;
        Name = index == 0
            ? new TextObject("{=taom_fui_banner_layer_background}Background").ToString()
            : new TextObject("{=taom_fui_banner_layer_sigil}Sigil {INDEX}").SetTextVariable("INDEX", index).ToString();
        RefreshAppearance();
    }

    internal BannerData Data { get; }

    [DataSourceProperty] public int LayerIndex { get; }
    [DataSourceProperty] public bool IsLayerPattern => LayerIndex == 0;
    /// <summary>The background always fills the banner; only sigils move, scale, rotate, mirror or take an outline.</summary>
    [DataSourceProperty] public bool IsTransformEnabled => LayerIndex > 0;
    [DataSourceProperty] public int EditableAreaSize => Banner.BannerEditableAreaSize;
    [DataSourceProperty] public int TotalAreaSize => Banner.BannerFullSize;
    [DataSourceProperty] public string Name { get; }

    [DataSourceProperty]
    public bool IsSelected
    {
        get => _isSelected;
        set { if (value == _isSelected) return; _isSelected = value; OnPropertyChangedWithValue(value); }
    }

    [DataSourceProperty]
    public string IconIDAsString
    {
        get => _iconId;
        private set { if (value == _iconId) return; _iconId = value; OnPropertyChangedWithValue(value); }
    }

    [DataSourceProperty]
    public Color Color1
    {
        get => _color1;
        private set { if (value == _color1) return; _color1 = value; OnPropertyChangedWithValue(value); }
    }

    [DataSourceProperty]
    public Color Color2
    {
        get => _color2;
        private set { if (value == _color2) return; _color2 = value; OnPropertyChangedWithValue(value); }
    }

    [DataSourceProperty]
    public bool IsMirrorActive
    {
        get => _isMirrorActive;
        private set { if (value == _isMirrorActive) return; _isMirrorActive = value; OnPropertyChangedWithValue(value); }
    }

    [DataSourceProperty]
    public bool IsDrawStrokeActive
    {
        get => _isDrawStrokeActive;
        private set { if (value == _isDrawStrokeActive) return; _isDrawStrokeActive = value; OnPropertyChangedWithValue(value); }
    }

    // Canvas bindings: no commit, the widget's release does that.
    [DataSourceProperty]
    public Vec2 PositionValue
    {
        get => _position;
        set
        {
            if (value == _position) return;
            _position = value;
            Data.Position = value;
            OnPropertyChangedWithValue(value);
            OnPropertyChangedWithValue(PositionX, nameof(PositionX));
            OnPropertyChangedWithValue(PositionY, nameof(PositionY));
        }
    }

    [DataSourceProperty]
    public Vec2 SizeValue
    {
        get => _size;
        set
        {
            if (value == _size) return;
            _size = value;
            Data.Size = value;
            OnPropertyChangedWithValue(value);
            OnPropertyChangedWithValue(SizeX, nameof(SizeX));
            OnPropertyChangedWithValue(SizeY, nameof(SizeY));
        }
    }

    [DataSourceProperty]
    public float RotationValue
    {
        get => _rotation;
        set
        {
            if (value == _rotation) return;
            _rotation = value;
            Data.RotationValue = value;
            OnPropertyChangedWithValue(value);
            OnPropertyChangedWithValue(Rotation360, nameof(Rotation360));
        }
    }

    // Slider bindings: the equality guard keeps the slider's echo of a canvas drag from committing.
    [DataSourceProperty]
    public int PositionX
    {
        get => (int)Math.Round(_position.X);
        set
        {
            value = BannerLayerEditing.ClampPosition(value);
            if (value == PositionX) return;
            PositionValue = new Vec2(value, _position.Y);
            _owner.CommitEdit();
        }
    }

    [DataSourceProperty]
    public int PositionY
    {
        get => (int)Math.Round(_position.Y);
        set
        {
            value = BannerLayerEditing.ClampPosition(value);
            if (value == PositionY) return;
            PositionValue = new Vec2(_position.X, value);
            _owner.CommitEdit();
        }
    }

    [DataSourceProperty]
    public int SizeX
    {
        get => (int)Math.Round(_size.X);
        set
        {
            value = BannerLayerEditing.ClampSize(value);
            if (value == SizeX) return;
            float y = _owner.IsSizeLocked && _size.X > 0f ? BannerLayerEditing.ClampSize((int)Math.Round(_size.Y * value / _size.X)) : _size.Y;
            SizeValue = new Vec2(value, y);
            _owner.CommitEdit();
        }
    }

    [DataSourceProperty]
    public int SizeY
    {
        get => (int)Math.Round(_size.Y);
        set
        {
            value = BannerLayerEditing.ClampSize(value);
            if (value == SizeY) return;
            float x = _owner.IsSizeLocked && _size.Y > 0f ? BannerLayerEditing.ClampSize((int)Math.Round(_size.X * value / _size.Y)) : _size.X;
            SizeValue = new Vec2(x, value);
            _owner.CommitEdit();
        }
    }

    [DataSourceProperty]
    public int Rotation360
    {
        get => BannerLayerEditing.ToDegrees(_rotation);
        set
        {
            if (value == Rotation360) return;
            RotationValue = BannerLayerEditing.ToTurns(value);
            _owner.CommitEdit();
        }
    }

    public void ExecuteSelect() => _owner.SelectLayer(this);

    public void ExecuteUpdateBanner() => _owner.CommitEdit();

    public void ExecuteToggleMirror()
    {
        Data.Mirror = !Data.Mirror;
        IsMirrorActive = Data.Mirror;
        _owner.CommitEdit();
    }

    public void ExecuteToggleStroke()
    {
        Data.DrawStroke = !Data.DrawStroke;
        IsDrawStrokeActive = Data.DrawStroke;
        _owner.CommitEdit();
    }

    public void ExecuteCenter()
    {
        BannerLayerEditing.CentreLayer(Data);
        PositionValue = Data.Position;
        _owner.CommitEdit();
    }

    /// <summary>Default size and no rotation; the position stays.</summary>
    public void ExecuteResetTransform()
    {
        BannerLayerEditing.ResetSize(Data, IsLayerPattern);
        SizeValue = Data.Size;
        RotationValue = 0f;
        _owner.CommitEdit();
    }

    /// <summary>Rereads mesh and colours after the editor changed them on Data.</summary>
    internal void RefreshAppearance()
    {
        IconIDAsString = Data.MeshId.ToString();
        Color1 = TaomBannerEditorVM.ColorOf(Data.ColorId);
        Color2 = TaomBannerEditorVM.ColorOf(Data.ColorId2);
    }
}

/// <summary>One cell of the icon grid: a sigil, or a background pattern while the background layer is selected.</summary>
public sealed class TaomBannerIconVM : ViewModel
{
    private readonly Action<TaomBannerIconVM> _onSelect;
    private bool _isSelected;

    internal TaomBannerIconVM(int id, Action<TaomBannerIconVM> onSelect)
    {
        Id = id;
        IconPath = id.ToString();
        _onSelect = onSelect;
    }

    internal int Id { get; }

    /// <summary>The sprite name: ui_bannericons and TAOM's ui_taom_bannericons name each sprite after its mesh id.</summary>
    [DataSourceProperty] public string IconPath { get; }

    [DataSourceProperty]
    public bool IsSelected
    {
        get => _isSelected;
        set { if (value == _isSelected) return; _isSelected = value; OnPropertyChangedWithValue(value); }
    }

    public void ExecuteSelect() => _onSelect(this);
}

/// <summary>One palette swatch.</summary>
public sealed class TaomBannerColorVM : ViewModel
{
    private readonly Action<TaomBannerColorVM> _onSelect;
    private bool _isSelected;

    internal TaomBannerColorVM(int id, uint color, Action<TaomBannerColorVM> onSelect)
    {
        Id = id;
        ColorAsStr = Color.FromUint(color).ToString();
        _onSelect = onSelect;
    }

    internal int Id { get; }

    [DataSourceProperty] public string ColorAsStr { get; }

    [DataSourceProperty]
    public bool IsSelected
    {
        get => _isSelected;
        set { if (value == _isSelected) return; _isSelected = value; OnPropertyChangedWithValue(value); }
    }

    public void ExecuteSelect() => _onSelect(this);
}
