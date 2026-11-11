using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace TAOM.Features.FactionUI.Campaign;

/// <summary>
/// The data source of TAOMBannerEditor.xml: a layered editor over the banner the vanilla BannerEditorVM edits. The vanilla
/// view model stays the owner of Done, Cancel (it restores its own initial code), the input keys and the 3D character;
/// the prefab reaches those through {Native}. Every edit here changes that same Banner object and then calls the vanilla
/// refresh, so Done saves the layered banner as it would the vanilla one.
/// </summary>
public sealed class TaomBannerEditorVM : ViewModel
{
    private readonly Banner _banner;
    private readonly Action? _refresh;
    private readonly string _initialCode;
    private readonly BannerHistory _history;
    private readonly List<BannerIconCategory> _sigilGroups;
    private readonly List<BannerIconCategory> _patternGroups;
    private int _sigilGroupIndex, _patternGroupIndex;
    private bool _applying;

    private string _bannerCode = "";
    private TaomBannerLayerVM? _selected;
    private string _iconGroupName = "", _layerCountText = "";
    private bool _isEditingFirstColor = true, _isSizeLocked, _canEditColors, _isSigilLayerSelected, _canChangeIconGroup;
    private bool _canAddLayer, _canDuplicateLayer, _canDeleteLayer, _canMoveLayerUp, _canMoveLayerDown, _canUndo, _canRedo;

    public TaomBannerEditorVM(BannerEditorVM native)
    {
        Native = native;
        _banner = native.BannerVM.Banner;
        _refresh = AccessTools.Field(typeof(BannerEditorVM), "_refresh")?.GetValue(native) as Action;
        _initialCode = _banner.Serialize();
        _history = new BannerHistory(_initialCode);
        var groups = BannerManager.Instance.BannerIconGroups;
        _sigilGroups = BannerIconCategory.Sigils(groups);
        _patternGroups = BannerIconCategory.Patterns(groups);
        Layers = new MBBindingList<TaomBannerLayerVM>();
        Icons = new MBBindingList<TaomBannerIconVM>();
        Colors = new MBBindingList<TaomBannerColorVM>();
        RefreshValues();
        native.BannerVM.PropertyChangedWithValue += OnNativeBannerChanged;
        native.PropertyChangedWithValue += OnNativeRulesChanged;
        RebuildLayers(Math.Min(1, _banner.GetBannerDataListCount() - 1));
        BannerCodeAsString = _banner.BannerCode;
    }

    [DataSourceProperty] public BannerEditorVM Native { get; }
    [DataSourceProperty] public MBBindingList<TaomBannerLayerVM> Layers { get; }
    [DataSourceProperty] public MBBindingList<TaomBannerIconVM> Icons { get; }
    [DataSourceProperty] public MBBindingList<TaomBannerColorVM> Colors { get; }

    [DataSourceProperty]
    public string BannerCodeAsString
    {
        get => _bannerCode;
        private set { if (value == _bannerCode) return; _bannerCode = value; OnPropertyChangedWithValue(value); }
    }

    [DataSourceProperty]
    public TaomBannerLayerVM? CurrentSelectedLayer
    {
        get => _selected;
        private set { if (value == _selected) return; _selected = value; OnPropertyChangedWithValue(value); }
    }

    [DataSourceProperty]
    public string IconGroupName
    {
        get => _iconGroupName;
        private set { if (value == _iconGroupName) return; _iconGroupName = value; OnPropertyChangedWithValue(value); }
    }

    [DataSourceProperty]
    public string LayerCountText
    {
        get => _layerCountText;
        private set { if (value == _layerCountText) return; _layerCountText = value; OnPropertyChangedWithValue(value); }
    }

    [DataSourceProperty] public bool IsEditingFirstColor { get => _isEditingFirstColor; private set => SetFlag(ref _isEditingFirstColor, value, nameof(IsEditingFirstColor)); }
    [DataSourceProperty] public bool IsEditingSecondColor => !_isEditingFirstColor;
    [DataSourceProperty] public bool IsSizeLocked { get => _isSizeLocked; private set => SetFlag(ref _isSizeLocked, value, nameof(IsSizeLocked)); }
    [DataSourceProperty] public bool CanEditColors { get => _canEditColors; private set => SetFlag(ref _canEditColors, value, nameof(CanEditColors)); }
    [DataSourceProperty] public bool IsSigilLayerSelected { get => _isSigilLayerSelected; private set => SetFlag(ref _isSigilLayerSelected, value, nameof(IsSigilLayerSelected)); }
    [DataSourceProperty] public bool CanChangeIconGroup { get => _canChangeIconGroup; private set => SetFlag(ref _canChangeIconGroup, value, nameof(CanChangeIconGroup)); }
    [DataSourceProperty] public bool CanAddLayer { get => _canAddLayer; private set => SetFlag(ref _canAddLayer, value, nameof(CanAddLayer)); }
    [DataSourceProperty] public bool CanDuplicateLayer { get => _canDuplicateLayer; private set => SetFlag(ref _canDuplicateLayer, value, nameof(CanDuplicateLayer)); }
    [DataSourceProperty] public bool CanDeleteLayer { get => _canDeleteLayer; private set => SetFlag(ref _canDeleteLayer, value, nameof(CanDeleteLayer)); }
    [DataSourceProperty] public bool CanMoveLayerUp { get => _canMoveLayerUp; private set => SetFlag(ref _canMoveLayerUp, value, nameof(CanMoveLayerUp)); }
    [DataSourceProperty] public bool CanMoveLayerDown { get => _canMoveLayerDown; private set => SetFlag(ref _canMoveLayerDown, value, nameof(CanMoveLayerDown)); }
    [DataSourceProperty] public bool CanUndo { get => _canUndo; private set => SetFlag(ref _canUndo, value, nameof(CanUndo)); }
    [DataSourceProperty] public bool CanRedo { get => _canRedo; private set => SetFlag(ref _canRedo, value, nameof(CanRedo)); }

    // The canvas preview frame (BannerPreviewFrame). Kept for the session, so the editor reopens on the frame last used.
    private static bool s_pennantPreview;
    private static BannerPreviewFrame PreviewFrame => s_pennantPreview ? BannerPreviewFrame.Pennant : BannerPreviewFrame.Shield;
    [DataSourceProperty] public bool IsShieldPreview => !s_pennantPreview;
    [DataSourceProperty] public bool IsPennantPreview => s_pennantPreview;
    [DataSourceProperty] public float PreviewWidth => PreviewFrame.ViewWidth;
    [DataSourceProperty] public float PreviewCanvasSize => PreviewFrame.CanvasSize;

    public void ExecuteShowShieldPreview() => SetPreviewFrame(pennant: false);

    public void ExecuteShowPennantPreview() => SetPreviewFrame(pennant: true);

    private void SetPreviewFrame(bool pennant)
    {
        if (s_pennantPreview == pennant)
            return;
        s_pennantPreview = pennant;
        OnPropertyChangedWithValue(IsShieldPreview, nameof(IsShieldPreview));
        OnPropertyChangedWithValue(IsPennantPreview, nameof(IsPennantPreview));
        OnPropertyChangedWithValue(PreviewWidth, nameof(PreviewWidth));
        OnPropertyChangedWithValue(PreviewCanvasSize, nameof(PreviewCanvasSize));
    }

    public override void RefreshValues()
    {
        base.RefreshValues();
        // Static labels are LocalizedText in the prefab; the group name is the one engine text shown here.
        if (_selected != null)
            RefreshIconGroupName();
    }

    public override void OnFinalize()
    {
        base.OnFinalize();
        Native.BannerVM.PropertyChangedWithValue -= OnNativeBannerChanged;
        Native.PropertyChangedWithValue -= OnNativeRulesChanged;
    }

    // Layer list

    public void ExecuteAddLayer()
    {
        if (!BannerLayerEditing.CanAdd(_banner))
            return;
        int after = _selected is { LayerIndex: > 0 } ? _selected.LayerIndex : _banner.GetBannerDataListCount() - 1;
        int index = BannerLayerEditing.Insert(_banner, NewSigil(), after);
        if (index < 0)
            return;
        RebuildLayers(index);
        CommitEdit();
    }

    public void ExecuteDuplicateLayer()
    {
        if (_selected == null)
            return;
        int index = BannerLayerEditing.Duplicate(_banner, _selected.LayerIndex);
        if (index < 0)
            return;
        RebuildLayers(index);
        CommitEdit();
    }

    public void ExecuteDeleteLayer()
    {
        if (_selected == null || !BannerLayerEditing.Remove(_banner, _selected.LayerIndex))
            return;
        RebuildLayers(Math.Min(_selected.LayerIndex, _banner.GetBannerDataListCount() - 1));
        CommitEdit();
    }

    /// <summary>The list shows the front layer first, so up brings the layer forward.</summary>
    public void ExecuteMoveLayerUp() => MoveSelected(1);

    public void ExecuteMoveLayerDown() => MoveSelected(-1);

    // History

    public void ExecuteUndo()
    {
        string? code = _history.Undo();
        if (code != null)
            ApplyCode(code);
    }

    public void ExecuteRedo()
    {
        string? code = _history.Redo();
        if (code != null)
            ApplyCode(code);
    }

    /// <summary>Back to the banner the editor opened with, as an undoable step.</summary>
    public void ExecuteReset()
    {
        ApplyCode(_initialCode);
        _history.Record(_initialCode);
        RefreshFlags();
    }

    // Selected layer

    public void ExecuteRandomizeLayer()
    {
        if (_selected == null)
            return;
        var data = _selected.Data;
        if (Icons.Count > 0)
            data.MeshId = Icons[MBRandom.RandomInt(Icons.Count)].Id;
        if (CanEditColors && Colors.Count > 0)
        {
            data.ColorId = Colors[MBRandom.RandomInt(Colors.Count)].Id;
            data.ColorId2 = Colors[MBRandom.RandomInt(Colors.Count)].Id;
        }
        CommitEdit();
        RefreshIcons(jumpToMesh: true);
        RefreshColors();
    }

    /// <summary>
    /// A new pattern or sigil and two new colours on every layer, background included, as one undoable step. Sigils
    /// and patterns come from the pages the grid last showed for them, so a Dúnedain banner stays Dúnedain; positions,
    /// sizes and rotations stay. A background whose colours the kingdom sets (CanChangeBackgroundColor) keeps them.
    /// </summary>
    public void ExecuteRandomizeAll()
    {
        int count = _banner.GetBannerDataListCount();
        if (count < 1)
            return;
        var sigils = _sigilGroups.Count > 0 ? CurrentSigilGroup().Ids : (IReadOnlyList<int>)Array.Empty<int>();
        var patterns = _patternGroups.Count > 0 ? CurrentPatternGroup().Ids : (IReadOnlyList<int>)Array.Empty<int>();
        var sigilColors = ChoosableColors(background: false);
        var backgroundColors = Native.CanChangeBackgroundColor ? ChoosableColors(background: true) : new List<int>();
        for (int i = 0; i < count; i++)
        {
            var data = _banner.BannerDataList[i];
            var ids = i == 0 ? patterns : sigils;
            var colors = i == 0 ? backgroundColors : sigilColors;
            if (ids.Count > 0)
                data.MeshId = ids[MBRandom.RandomInt(ids.Count)];
            if (colors.Count > 0)
            {
                data.ColorId = colors[MBRandom.RandomInt(colors.Count)];
                data.ColorId2 = colors[MBRandom.RandomInt(colors.Count)];
            }
        }
        CommitEdit();
        foreach (var layer in Layers)
            layer.RefreshAppearance();
        RefreshIcons(jumpToMesh: true);
        RefreshColors();
    }

    public void ExecutePreviousIconGroup() => StepIconGroup(-1);

    public void ExecuteNextIconGroup() => StepIconGroup(1);

    public void ExecuteEditFirstColor()
    {
        IsEditingFirstColor = true;
        OnPropertyChangedWithValue(IsEditingSecondColor, nameof(IsEditingSecondColor));
        RefreshColorSelection();
    }

    public void ExecuteEditSecondColor()
    {
        IsEditingFirstColor = false;
        OnPropertyChangedWithValue(IsEditingSecondColor, nameof(IsEditingSecondColor));
        RefreshColorSelection();
    }

    public void ExecuteSwapColors()
    {
        if (_selected == null || !CanEditColors)
            return;
        BannerLayerEditing.SwapColors(_selected.Data);
        CommitEdit();
        RefreshColorSelection();
    }

    public void ExecuteToggleSizeLock() => IsSizeLocked = !IsSizeLocked;

    /// <summary>Saves the current banner as one step: clears the banner's cached code, redraws and records history.</summary>
    internal void CommitEdit()
    {
        if (_applying)
            return;
        BannerLayerEditing.Invalidate(_banner);
        string code = _banner.BannerCode;
        BannerCodeAsString = code;
        _history.Record(code);
        _refresh?.Invoke();
        _selected?.RefreshAppearance();
        RefreshFlags();
    }

    internal void SelectLayer(TaomBannerLayerVM layer)
    {
        if (_selected != null)
            _selected.IsSelected = false;
        layer.IsSelected = true;
        CurrentSelectedLayer = layer;
        IsSigilLayerSelected = layer.LayerIndex > 0;
        RefreshCanEditColors();
        RefreshIcons(jumpToMesh: true);
        RefreshColors();
        RefreshFlags();
    }

    internal static Color ColorOf(int colorId) =>
        BannerManager.Instance.ReadOnlyColorPalette.TryGetValue(colorId, out var color)
            ? Color.FromUint(color.Color)
            : new Color(1f, 1f, 1f);

    private void MoveSelected(int delta)
    {
        if (_selected == null)
            return;
        int index = BannerLayerEditing.Move(_banner, _selected.LayerIndex, delta);
        if (index == _selected.LayerIndex)
            return;
        RebuildLayers(index);
        CommitEdit();
    }

    /// <summary>Loads a code from history and redraws without recording it again.</summary>
    private void ApplyCode(string code)
    {
        int keep = _selected?.LayerIndex ?? 1;
        _applying = true;
        try
        {
            _banner.Deserialize(code);
        }
        finally
        {
            _applying = false;
        }
        BannerLayerEditing.Invalidate(_banner);
        BannerCodeAsString = _banner.BannerCode;
        RebuildLayers(Math.Min(keep, _banner.GetBannerDataListCount() - 1));
        _refresh?.Invoke();
        RefreshFlags();
    }

    // The clipboard paste (Patch23) sets BannerVM.BannerCode and then calls the vanilla refresh itself.
    private void OnNativeBannerChanged(object sender, PropertyChangedWithValueEventArgs e)
    {
        if (_applying || e.PropertyName != BannerViewModelCode)
            return;
        if (_banner.GetBannerDataListCount() == 0)
        {
            // An unreadable clipboard leaves no layers; put the last good banner back before Patch23 redraws.
            _applying = true;
            try { _banner.Deserialize(_history.Current); }
            finally { _applying = false; }
        }
        int keep = _selected?.LayerIndex ?? 1;
        RebuildLayers(Math.Min(keep, _banner.GetBannerDataListCount() - 1));
        BannerLayerEditing.Invalidate(_banner);
        BannerCodeAsString = _banner.BannerCode;
        _history.Record(BannerCodeAsString);
        RefreshFlags();
    }

    private const string BannerViewModelCode = "BannerCode";

    // A clan in a kingdom wears the kingdom's background colour. The campaign screen applies that rule through
    // SetClanRelatedRules only after the editor view, and so this wrapper, already exists, so follow the change.
    private void OnNativeRulesChanged(object sender, PropertyChangedWithValueEventArgs e)
    {
        if (e.PropertyName == nameof(BannerEditorVM.CanChangeBackgroundColor))
            RefreshCanEditColors();
    }

    private void RefreshCanEditColors() =>
        CanEditColors = _selected != null && (_selected.LayerIndex > 0 || Native.CanChangeBackgroundColor);

    /// <summary>Rebuilds the list front layer first and selects <paramref name="selectIndex"/>.</summary>
    private void RebuildLayers(int selectIndex)
    {
        if (_selected != null)
            _selected.IsSelected = false;
        _selected = null;
        Layers.Clear();
        int count = _banner.GetBannerDataListCount();
        TaomBannerLayerVM? select = null;
        for (int i = count - 1; i >= 0; i--)
        {
            var layer = new TaomBannerLayerVM(this, _banner.BannerDataList[i], i);
            Layers.Add(layer);
            if (i == selectIndex)
                select = layer;
        }
        select ??= Layers.LastOrDefault();
        if (select != null)
            SelectLayer(select);
        else
            CurrentSelectedLayer = null;
    }

    private BannerData NewSigil()
    {
        int colorId, colorId2;
        if (_selected is { LayerIndex: > 0 })
        {
            colorId = _selected.Data.ColorId;
            colorId2 = _selected.Data.ColorId2;
        }
        else
        {
            var palette = BannerManager.Instance.ReadOnlyColorPalette;
            colorId = palette.FirstOrDefault(c => c.Value.PlayerCanChooseForSigil).Key;
            colorId2 = colorId;
        }
        int meshId = _sigilGroups.Count > 0 ? CurrentSigilGroup().Ids[0] : _banner.GetIconMeshId();
        float size = BannerLayerEditing.DefaultSigilSize;
        float centre = BannerLayerEditing.Centre;
        return new BannerData(meshId, colorId, colorId2, new Vec2(size, size), new Vec2(centre, centre), false, false, 0f);
    }

    private BannerIconCategory CurrentSigilGroup() => _sigilGroups[Math.Max(0, Math.Min(_sigilGroupIndex, _sigilGroups.Count - 1))];

    private BannerIconCategory CurrentPatternGroup() => _patternGroups[Math.Max(0, Math.Min(_patternGroupIndex, _patternGroups.Count - 1))];

    /// <summary>The palette ids a player may pick for a background or a sigil; the whole palette if none is marked.</summary>
    private static List<int> ChoosableColors(bool background)
    {
        var palette = BannerManager.Instance.ReadOnlyColorPalette;
        var ids = palette.Where(c => background ? c.Value.PlayerCanChooseForBackground : c.Value.PlayerCanChooseForSigil)
            .Select(c => c.Key).ToList();
        return ids.Count > 0 ? ids : palette.Keys.ToList();
    }

    private void StepIconGroup(int delta)
    {
        if (_selected == null)
            return;
        if (_selected.IsLayerPattern)
        {
            if (_patternGroups.Count < 2)
                return;
            _patternGroupIndex = (_patternGroupIndex + delta + _patternGroups.Count) % _patternGroups.Count;
        }
        else
        {
            if (_sigilGroups.Count < 2)
                return;
            _sigilGroupIndex = (_sigilGroupIndex + delta + _sigilGroups.Count) % _sigilGroups.Count;
        }
        RefreshIcons(jumpToMesh: false);
    }

    /// <summary>Fills the grid with the selected layer's kind: patterns for the background, sigils otherwise.</summary>
    private void RefreshIcons(bool jumpToMesh)
    {
        Icons.Clear();
        if (_selected == null)
            return;
        int mesh = _selected.Data.MeshId;
        IEnumerable<int> ids;
        if (_selected.IsLayerPattern)
        {
            if (_patternGroups.Count == 0)
            {
                CanChangeIconGroup = false;
                IconGroupName = "";
                return;
            }
            if (jumpToMesh)
            {
                int found = _patternGroups.FindIndex(g => g.Contains(mesh));
                if (found >= 0)
                    _patternGroupIndex = found;
            }
            _patternGroupIndex = Math.Max(0, Math.Min(_patternGroupIndex, _patternGroups.Count - 1));
            ids = _patternGroups[_patternGroupIndex].Ids;
            CanChangeIconGroup = _patternGroups.Count > 1;
        }
        else
        {
            if (_sigilGroups.Count == 0)
            {
                CanChangeIconGroup = false;
                IconGroupName = "";
                return;
            }
            if (jumpToMesh)
            {
                int found = _sigilGroups.FindIndex(g => g.Contains(mesh));
                if (found >= 0)
                    _sigilGroupIndex = found;
            }
            ids = CurrentSigilGroup().Ids;
            CanChangeIconGroup = _sigilGroups.Count > 1;
        }
        foreach (int id in ids)
            Icons.Add(new TaomBannerIconVM(id, OnIconSelected) { IsSelected = id == mesh });
        RefreshIconGroupName();
    }

    private void RefreshIconGroupName()
    {
        if (_selected == null)
            return;
        var groups = _selected.IsLayerPattern ? _patternGroups : _sigilGroups;
        int index = _selected.IsLayerPattern ? _patternGroupIndex : _sigilGroupIndex;
        IconGroupName = index >= 0 && index < groups.Count ? groups[index].Name : "";
    }

    private void OnIconSelected(TaomBannerIconVM icon)
    {
        if (_selected == null)
            return;
        foreach (var item in Icons)
            item.IsSelected = item == icon;
        if (_selected.Data.MeshId == icon.Id)
            return;
        _selected.Data.MeshId = icon.Id;
        CommitEdit();
    }

    /// <summary>The palette of the selected layer: background colours for the background, sigil colours otherwise.</summary>
    private void RefreshColors()
    {
        Colors.Clear();
        if (_selected == null)
            return;
        var palette = BannerManager.Instance.ReadOnlyColorPalette;
        foreach (int id in ChoosableColors(background: _selected.IsLayerPattern))
            Colors.Add(new TaomBannerColorVM(id, palette[id].Color, OnColorSelected));
        RefreshColorSelection();
    }

    private void RefreshColorSelection()
    {
        if (_selected == null)
            return;
        int id = _isEditingFirstColor ? _selected.Data.ColorId : _selected.Data.ColorId2;
        foreach (var color in Colors)
            color.IsSelected = color.Id == id;
    }

    private void OnColorSelected(TaomBannerColorVM color)
    {
        if (_selected == null || !CanEditColors)
        {
            RefreshColorSelection();
            return;
        }
        if (_isEditingFirstColor)
            _selected.Data.ColorId = color.Id;
        else
            _selected.Data.ColorId2 = color.Id;
        RefreshColorSelection();
        CommitEdit();
    }

    private void RefreshFlags()
    {
        int count = _banner.GetBannerDataListCount();
        int index = _selected?.LayerIndex ?? -1;
        CanAddLayer = BannerLayerEditing.CanAdd(_banner);
        CanDuplicateLayer = index > 0 && CanAddLayer;
        CanDeleteLayer = index > 0;
        CanMoveLayerUp = index > 0 && index < count - 1;
        CanMoveLayerDown = index > 1;
        CanUndo = _history.CanUndo;
        CanRedo = _history.CanRedo;
        LayerCountText = $"{Math.Max(0, count - 1)} / {Banner.MaxIconCount}";
    }

    private void SetFlag(ref bool field, bool value, string name)
    {
        if (field == value)
            return;
        field = value;
        OnPropertyChangedWithValue(value, name);
    }
}
