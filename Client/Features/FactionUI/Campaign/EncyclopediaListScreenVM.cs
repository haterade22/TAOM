using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Library;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.List;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection;
using TaleWorlds.Core.ViewModelCollection.ImageIdentifiers;
using TaleWorlds.Localization;

namespace TAOM.Features.FactionUI.Campaign
{
    // Presentation only: native filter VMs, sort controllers and entry-opening commands remain authoritative.
    public class EncyclopediaListScreenVM : ViewModel
    {
        [DataSourceProperty] public ViewModel Native { get; private set; }
        [DataSourceProperty] public MBBindingList<EncyclopediaEntryVM> Entries { get; private set; } = new MBBindingList<EncyclopediaEntryVM>();
        [DataSourceProperty] public MBBindingList<EncyclopediaFilterSectionVM> FilterSections { get; private set; } = new MBBindingList<EncyclopediaFilterSectionVM>();
        [DataSourceProperty] public MBBindingList<EncyclopediaSortOptionVM> SortOptions { get; private set; } = new MBBindingList<EncyclopediaSortOptionVM>();
        [DataSourceProperty] public string PageTitle { get; private set; }
        [DataSourceProperty] public string FiltersLabel => new TextObject("{=taom_ency_filters}Filters").ToString();
        [DataSourceProperty] public string ResetLabel => new TextObject("{=taom_ency_reset}Reset").ToString();
        [DataSourceProperty] public string OpenEntryLabel => new TextObject("{=taom_ency_open_entry}Open entry").ToString();
        [DataSourceProperty] public string SortByLabel => Read<string>(_sortController, "SortByLabel");
        [DataSourceProperty] public string EmptyListText => Read<string>(Native, "EmptyListText");
        [DataSourceProperty] public ViewModel SortController => _sortController;
        private readonly Dictionary<ViewModel, EncyclopediaEntryVM> _entries = new Dictionary<ViewModel, EncyclopediaEntryVM>();
        private readonly ViewModel _sortController, _selector;
        private readonly IMBBindingList _nativeItems;
        private EncyclopediaEntryVM? _selected;
        private bool _orderDirty = true, _selectionDirty = true, _disposed;
        private bool _hasSelection, _showSortOptions, _isEmpty;
        private bool _showCharacterPreview, _showHeroRender, _showHeroSilhouette, _showTroopRender, _showIllustration = true;
        private ViewModel? _heroCharacter, _bannerIdentifier;
        private CharacterViewModel? _troopCharacter;
        private bool _showBannerPreview, _isBannerDestroyed;
        private string _selectedName = "", _selectedSubtitle = "", _previewImageFile = "", _previewSpriteName = "", _sortLabel = "";
        internal string Category { get; private set; }
        public int SelectionVersion { get; private set; }
        public string? SelectedId => _selected?.Id;

        [DataSourceProperty] public bool HasSelection { get => _hasSelection; private set { _hasSelection = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public bool IsEmpty { get => _isEmpty; private set { _isEmpty = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public bool ShowSortOptions { get => _showSortOptions; private set { _showSortOptions = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public string SelectedName { get => _selectedName; private set { _selectedName = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public string SelectedSubtitle { get => _selectedSubtitle; private set { _selectedSubtitle = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public string PreviewImageFile { get => _previewImageFile; private set { _previewImageFile = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public string PreviewSpriteName { get => _previewSpriteName; private set { _previewSpriteName = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public string SortLabel { get => _sortLabel; private set { _sortLabel = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public ViewModel? HeroCharacter { get => _heroCharacter; private set { _heroCharacter = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public CharacterViewModel? TroopCharacter { get => _troopCharacter; private set { _troopCharacter = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public bool ShowCharacterPreview { get => _showCharacterPreview; private set { _showCharacterPreview = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public bool ShowHeroRender { get => _showHeroRender; private set { _showHeroRender = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public bool ShowHeroSilhouette { get => _showHeroSilhouette; private set { _showHeroSilhouette = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public bool ShowTroopRender { get => _showTroopRender; private set { _showTroopRender = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public bool ShowIllustration { get => _showIllustration; private set { _showIllustration = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public bool ShowBannerPreview { get => _showBannerPreview; private set { _showBannerPreview = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public bool IsBannerDestroyed { get => _isBannerDestroyed; private set { _isBannerDestroyed = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public ViewModel? BannerIdentifier { get => _bannerIdentifier; private set { _bannerIdentifier = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public string HeroSilhouetteSprite
        {
            get
            {
                return "Encyclopedia\\hero_silhouette";
            }
        }

        public EncyclopediaListScreenVM(ViewModel native)
        {
            Native = native;
            var list = (EncyclopediaListVM)native;
            PageTitle = list.GetName();
            string id = list.Page.GetStringID().ToLowerInvariant();
            Category = id.Contains("settlement") ? "settlements" : id.Contains("hero") ? "heroes"
                : id.Contains("unit") || id.Contains("troop") ? "troops"
                : id.Contains("faction") || id.Contains("kingdom") ? "kingdoms" : id.Contains("clan") ? "clans" : "concepts";
            _nativeItems = (IMBBindingList)native.GetPropertyValue("Items");
            _nativeItems.ListChanged += OnItemsChanged;
            Native.PropertyChangedWithBoolValue += OnNativeBoolChanged;
            _sortController = Read<ViewModel>(native, "SortController");
            _selector = Read<ViewModel>(_sortController, "SortSelection");
            _selector.PropertyChangedWithIntValue += OnSortChanged;
            foreach (ViewModel group in (IEnumerable)native.GetPropertyValue("FilterGroups")) FilterSections.Add(new EncyclopediaFilterSectionVM(group));
            int index = 0;
            foreach (ViewModel option in (IEnumerable)_selector.GetPropertyValue("ItemList")) SortOptions.Add(new EncyclopediaSortOptionVM(this, index++, Read<string>(option, "StringItem")));
            Synchronize();
        }

        internal static T Read<T>(ViewModel source, string property) => (T)source.GetPropertyValue(property);

        private void OnItemsChanged(object sender, ListChangedEventArgs e) { _orderDirty = true; _selectionDirty = true; }
        private void OnNativeBoolChanged(object sender, PropertyChangedWithBoolValueEventArgs e)
        {
            if (e.PropertyName == "IsInitializationOver" && e.Value) _selectionDirty = true;
        }
        private void OnSortChanged(object sender, PropertyChangedWithIntValueEventArgs e) { _orderDirty = true; _selectionDirty = true; }
        internal void MarkSelectionDirty() { _selectionDirty = true; }

        public void Synchronize()
        {
            if (_disposed) return;
            if (_orderDirty)
            {
                _orderDirty = false;
                Entries.Clear();
                var live = new HashSet<ViewModel>();
                foreach (ViewModel item in _nativeItems)
                {
                    live.Add(item);
                    if (!_entries.TryGetValue(item, out var row)) _entries.Add(item, row = new EncyclopediaEntryVM(this, item));
                    Entries.Add(row);
                }
                foreach (var removed in _entries.Keys.Where(x => !live.Contains(x)).ToArray()) { _entries[removed].Dispose(); _entries.Remove(removed); }
                SelectionVersion++;
            }
            int sortIndex = Read<int>(_selector, "SelectedIndex");
            if (sortIndex >= 0 && sortIndex < SortOptions.Count && SortLabel != SortOptions[sortIndex].Name) SortLabel = SortOptions[sortIndex].Name;
            if (!_selectionDirty) return;
            _selectionDirty = false;
            var visible = Entries.Where(x => x.IsVisible).ToArray();
            IsEmpty = visible.Length == 0;
            if (_selected == null || !_selected.IsVisible || !Entries.Contains(_selected))
            {
                string previous = Read<string>(Native, "LastSelectedItemId");
                Select(visible.FirstOrDefault(x => x.Id == previous) ?? visible.FirstOrDefault());
            }
        }

        internal void Select(EncyclopediaEntryVM? entry)
        {
            if (_selected == entry) return;
            if (_selected != null) _selected.IsSelected = false;
            _selected = entry;
            HasSelection = entry != null;
            SelectedName = entry?.Name ?? "";
            SelectedSubtitle = entry?.Subtitle ?? "";
            if (entry != null) { entry.IsSelected = true; Native.SetPropertyValue("LastSelectedItemId", entry.Id); }
            PreviewImageFile = Category == "settlements" && entry != null ? ResolvePicture(entry) : "";
            PreviewSpriteName = Category == "settlements" ? "fs_previewonly_encyclopedia_settlement" : Category == "heroes" ? "" : "ec_window_" + Category;
            UpdateCharacterPreview(entry);
            UpdateBannerPreview(entry);
            ShowIllustration = !ShowCharacterPreview && !ShowBannerPreview;
            SelectionVersion++;
        }

        internal static bool UsesBannerPreview(string category, bool hasSelection)
        {
            return hasSelection && (category == "kingdoms" || category == "clans");
        }

        internal static bool UsesCharacterPreview(string category, bool hasSelection)
        {
            return hasSelection && (category == "heroes" || category == "troops");
        }

        // The selected kingdom or clan's own Banner, built the way the native faction and clan pages
        // build theirs, so a new selection changes both the colours and the emblem.
        private void UpdateBannerPreview(EncyclopediaEntryVM? entry)
        {
            ReleaseBannerPreview();
            if (entry == null || !UsesBannerPreview(Category, true)) return;
            var source = ((EncyclopediaListItemVM)entry.Native).Object;
            Banner? banner = null;
            bool destroyed = false;
            if (source is Kingdom kingdom) { banner = kingdom.Banner; destroyed = kingdom.IsEliminated; }
            else if (source is Clan clan) { banner = TAOM.Features.Realms.Hooks.HouseBanners.HouseBannerOf(clan); destroyed = clan.IsEliminated; }
            if (banner == null) return;
            IsBannerDestroyed = destroyed;
            BannerIdentifier = new BannerImageIdentifierVM(banner, nineGrid: true);
            ShowBannerPreview = true;
        }

        // The identifier is created here, so it is finalized here; the Banner itself is borrowed.
        private void ReleaseBannerPreview()
        {
            ShowBannerPreview = false;
            IsBannerDestroyed = false;
            if (BannerIdentifier == null) return;
            BannerIdentifier.OnFinalize();
            BannerIdentifier = null;
        }

        // Heroes and troops share the raised viewport and one release path; each keeps its own tableau,
        // because only the hero view model has the IsDead the encyclopedia tableau desaturates on.
        private void UpdateCharacterPreview(EncyclopediaEntryVM? entry)
        {
            ReleaseCharacterPreview();
            if (entry == null || !UsesCharacterPreview(Category, true)) return;
            // Use the actual selected hero or troop, as the native encyclopedia hero and unit pages do.
            var source = ((EncyclopediaListItemVM)entry.Native).Object;
            if (source is Hero hero) ShowHero(hero);
            else if (source is CharacterObject troop) ShowTroop(troop);
        }

        private void ShowHero(Hero hero)
        {
            // None keeps the full-body inventory camera the tableau starts with, as SPInventoryVM's hero does;
            // EmphasizeFace would reset it and move in on the face.
            var character = new HeroViewModel(CharacterViewModel.StanceTypes.None);
            character.FillFrom(hero, -1, hero.IsNotable, useCharacteristicIdleAction: true);
            bool known = !character.IsHidden && !string.IsNullOrEmpty(character.CharStringId);
            if (known)
            {
                character.SetEquipment(EquipmentIndex.Horse, default);
                character.SetEquipment(EquipmentIndex.HorseHarness, default);
                // Match the native encyclopedia portrait: keep the face visible.
                character.SetEquipment(EquipmentIndex.Head, default);
                SelectedSubtitle = hero.Culture?.Name.ToString() ?? "";
            }
            else SelectedSubtitle = "";
            character.IsTableauEnabled = known;
            HeroCharacter = character;
            ShowHeroSilhouette = !known;
            ShowHeroRender = known;
            ShowCharacterPreview = true;
        }

        private void ShowTroop(CharacterObject troop)
        {
            var character = new CharacterViewModel(CharacterViewModel.StanceTypes.None);
            character.FillFrom(troop);
            // FillFrom fills nothing for a child-aged character; the troops illustration stays then.
            if (string.IsNullOrEmpty(character.CharStringId)) { character.OnFinalize(); return; }
            // FillFrom cloned the troop's equipment, so dropping the mount changes only this preview.
            // The helmet stays: a troop is shown as it takes the field.
            character.SetEquipment(EquipmentIndex.Horse, default);
            character.SetEquipment(EquipmentIndex.HorseHarness, default);
            character.IsTableauEnabled = true;
            TroopCharacter = character;
            SelectedSubtitle = troop.Culture?.Name?.ToString() ?? SelectedSubtitle;
            ShowTroopRender = true;
            ShowCharacterPreview = true;
        }

        // The character view models are created here, so they are finalized here, tableau first.
        private void ReleaseCharacterPreview()
        {
            ShowCharacterPreview = false;
            ShowHeroRender = false;
            ShowHeroSilhouette = false;
            ShowTroopRender = false;
            if (HeroCharacter != null) { ReleaseTableau(HeroCharacter); HeroCharacter = null; }
            if (TroopCharacter != null) { ReleaseTableau(TroopCharacter); TroopCharacter = null; }
        }

        private static void ReleaseTableau(ViewModel character)
        {
            character.SetPropertyValue("IsTableauEnabled", false);
            character.OnFinalize();
        }

        private string ResolvePicture(EncyclopediaEntryVM entry)
        {
            return EncyclopediaPictureCache.ResolveSettlement(entry.Id, entry.CultureId, entry.Kind);
        }

        private void ExecuteResetFilters() { Native.ExecuteCommand("ExecuteResetFilters", Array.Empty<object>()); Synchronize(); }
        private void ExecuteOpenEntry() { _selected?.Native.ExecuteCommand("Execute", Array.Empty<object>()); }
        private void ExecuteToggleSort()
        {
            if (!ShowSortOptions) _selector.ExecuteCommand("ExecuteOnDropdownActivated", Array.Empty<object>());
            ShowSortOptions = !ShowSortOptions;
        }
        internal void ApplySort(int index)
        {
            _sortController.ExecuteCommand("SetSortSelection", new object[] { index });
            ShowSortOptions = false;
            Synchronize();
        }

        internal void DisposePresentation()
        {
            if (_disposed) return;
            _disposed = true;
            ReleaseCharacterPreview();
            ReleaseBannerPreview();
            _nativeItems.ListChanged -= OnItemsChanged;
            Native.PropertyChangedWithBoolValue -= OnNativeBoolChanged;
            _selector.PropertyChangedWithIntValue -= OnSortChanged;
            foreach (var entry in _entries.Values) entry.Dispose();
        }
        public override void OnFinalize() { DisposePresentation(); base.OnFinalize(); }
    }

    public class EncyclopediaEntryVM : ViewModel
    {
        private readonly EncyclopediaListScreenVM _owner;
        internal ViewModel Native { get; }
        internal string CultureId { get; private set; } = "";
        internal string Kind { get; private set; } = "";
        [DataSourceProperty] public string Id { get; private set; }
        [DataSourceProperty] public string Subtitle { get; private set; }
        private string _name = "", _comparedValue = "";
        private bool _isVisible, _isSelected, _isBookmarked;
        [DataSourceProperty] public string Name { get => _name; private set { _name = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public string ComparedValue { get => _comparedValue; private set { _comparedValue = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public bool IsVisible { get => _isVisible; private set { _isVisible = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public bool IsSelected { get => _isSelected; internal set { _isSelected = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public bool IsBookmarked { get => _isBookmarked; private set { _isBookmarked = value; OnPropertyChangedWithValue(value); } }

        internal EncyclopediaEntryVM(EncyclopediaListScreenVM owner, ViewModel native)
        {
            _owner = owner; Native = native;
            Id = EncyclopediaListScreenVM.Read<string>(native, "Id");
            Subtitle = owner.PageTitle;
            if (((EncyclopediaListItemVM)native).Object is Settlement settlement)
            {
                CultureId = settlement.Culture.StringId;
                Kind = settlement.IsVillage ? "village" : settlement.IsCastle ? "castle" : "town";
                Subtitle = char.ToUpperInvariant(Kind[0]) + Kind.Substring(1) + " · " + settlement.Culture.Name;
            }
            native.PropertyChangedWithBoolValue += OnBoolChanged;
            native.PropertyChangedWithValue += OnValueChanged;
            Refresh();
        }
        private void Refresh()
        {
            Name = EncyclopediaListScreenVM.Read<string>(Native, "Name");
            ComparedValue = EncyclopediaListScreenVM.Read<bool>(Native, "PlayerCanSeeValues") ? EncyclopediaListScreenVM.Read<string>(Native, "ComparedValue") : "";
            IsVisible = !EncyclopediaListScreenVM.Read<bool>(Native, "IsFiltered");
            IsBookmarked = EncyclopediaListScreenVM.Read<bool>(Native, "IsBookmarked");
        }
        private void OnBoolChanged(object sender, PropertyChangedWithBoolValueEventArgs e) { Refresh(); _owner.MarkSelectionDirty(); }
        private void OnValueChanged(object sender, PropertyChangedWithValueEventArgs e) { Refresh(); }
        private void ExecuteSelect() { _owner.Select(this); }
        private void ExecuteBeginTooltip() { Native.ExecuteCommand("ExecuteBeginTooltip", Array.Empty<object>()); }
        private void ExecuteEndTooltip() { Native.ExecuteCommand("ExecuteEndTooltip", Array.Empty<object>()); }
        internal void Dispose() { Native.PropertyChangedWithBoolValue -= OnBoolChanged; Native.PropertyChangedWithValue -= OnValueChanged; }
    }

    public class EncyclopediaFilterSectionVM : ViewModel
    {
        [DataSourceProperty] public ViewModel Native { get; private set; }
        [DataSourceProperty] public string Name => EncyclopediaListScreenVM.Read<string>(Native, "FilterName");
        private bool _isExpanded = true;
        [DataSourceProperty] public bool IsExpanded { get => _isExpanded; private set { _isExpanded = value; OnPropertyChangedWithValue(value); } }
        internal EncyclopediaFilterSectionVM(ViewModel native) { Native = native; }
        private void ExecuteToggle() { IsExpanded = !IsExpanded; }
    }

    public class EncyclopediaSortOptionVM : ViewModel
    {
        private readonly EncyclopediaListScreenVM _owner;
        private readonly int _index;
        [DataSourceProperty] public string Name { get; private set; }
        internal EncyclopediaSortOptionVM(EncyclopediaListScreenVM owner, int index, string name) { _owner = owner; _index = index; Name = name; }
        private void ExecuteChoose() { _owner.ApplySort(_index); }
    }
}
