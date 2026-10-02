using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TAOM.Features.FactionMap.ViewModels;
using TAOM.Features.FactionUI.FactionScreen;
using TAOM.Features.FactionUI.Presets;
using TAOM.Features.FactionUI.Resources;

namespace TAOM.Features.FactionUI.UI.FactionScreen;

/// <summary>
/// Kysaro's faction and hero picker (#704), ported from his <c>FactionScreenVM</c>. It drives TAOM's
/// own <see cref="FactionSelectionVM"/> directly (his module set a private static and invoked TAOM's
/// methods by reflection): confirming selects the faction's region and confirms its culture through the
/// faction map's own path. Every image a property binds is registered first
/// (<see cref="FrontEndSpriteService.EnsureArt"/>), since Gauntlet resolves a sprite name when the bound
/// value is set; a picked hero goes to <see cref="FactionPickService"/>, which takes over a living hero
/// and copies any other pick. The choices about characters
/// (which troop the viewport shows, which lords are listed, when the Leader tab shows, which pin a
/// click lands on) are <see cref="FactionRoster"/>'s; this class only binds them to the screen.
/// </summary>
public sealed class FactionScreenVM : ViewModel
{
    private const float DefaultPortraitHeight = 850f;
    private const string LeaderCategory = "Leader";
    private const string LordCategory = "Lord";
    private const string WandererCategory = "Wanderer";

    private readonly FactionSelectionVM _factionMap;
    private readonly List<FactionInfo> _factions;
    private readonly FactionScreenServices _services;

    private FactionInfo? _selected;
    private FactionDetailVM? _selectedDetail;
    private string _browseCategory = "";
    private bool _activeHeroPortraitIsB;

    private string _backgroundSprite = "";
    private string _hoveredFactionName = "";
    private float _viewportOffset;
    private bool _hasPaintedPortrait;
    private bool _showCharacterViewport = true;
    private string _paintedPortraitSprite = "";
    private float _paintedPortraitWidth = 640f;
    private float _paintedPortraitHeight = DefaultPortraitHeight;
    private float _minimapGlowAlpha;
    private bool _showLeaderCategory = true;
    private string _heroPortraitSprite = "";
    private float _heroPortraitWidth = 500f;
    private float _heroPortraitHeight = DefaultPortraitHeight;
    private string _heroPortraitSpriteB = "";
    private float _heroPortraitWidthB = 500f;
    private float _heroPortraitHeightB = DefaultPortraitHeight;

    public FactionScreenVM(FactionSelectionVM factionMap, List<FactionInfo> factions, FactionScreenServices services)
    {
        _factionMap = factionMap;
        _factions = factions;
        _services = services;
        BackLabel = new TextObject("{=taom_fui_back_to_menu}Main Menu").ToString();
        Character = new CharacterViewModel(CharacterViewModel.StanceTypes.None);
        Factions = new MBBindingList<FactionItemVM>();
        Heroes = new MBBindingList<HeroItemVM>();
        BrowseList = new MBBindingList<HeroItemVM>();

        var cellWidth = Math.Min(118f, 1860f / Math.Max(1, factions.Count));
        foreach (var info in factions)
        {
            services.Sprites.EnsureArt(info.IconSprite);
            Factions.Add(new FactionItemVM(info, SelectFaction, HoverFaction, cellWidth));
        }

        var first = factions.FirstOrDefault(f => f.Key == "stewardship_of_gondor") ?? factions.FirstOrDefault();
        if (first != null)
            SelectFaction(first);
    }

    [DataSourceProperty] public string BackLabel { get; }

    [DataSourceProperty] public CharacterViewModel Character { get; }

    [DataSourceProperty] public MBBindingList<FactionItemVM> Factions { get; }

    [DataSourceProperty] public MBBindingList<HeroItemVM> Heroes { get; }

    [DataSourceProperty] public MBBindingList<HeroItemVM> BrowseList { get; }

    [DataSourceProperty] public bool IsLeaderCategorySelected => _browseCategory == LeaderCategory;

    [DataSourceProperty] public bool IsLordCategorySelected => _browseCategory == LordCategory;

    [DataSourceProperty] public bool IsWandererCategorySelected => _browseCategory == WandererCategory;

    [DataSourceProperty]
    public FactionDetailVM? Selected
    {
        get => _selectedDetail;
        set => Set(ref _selectedDetail, value, nameof(Selected));
    }

    [DataSourceProperty]
    public string BackgroundSprite
    {
        get => _backgroundSprite;
        set => SetSprite(ref _backgroundSprite, value, nameof(BackgroundSprite));
    }

    [DataSourceProperty]
    public string HoveredFactionName
    {
        get => _hoveredFactionName;
        set => Set(ref _hoveredFactionName, value, nameof(HoveredFactionName));
    }

    [DataSourceProperty]
    public float ViewportOffset
    {
        get => _viewportOffset;
        set => SetValue(ref _viewportOffset, value, nameof(ViewportOffset));
    }

    [DataSourceProperty]
    public bool HasPaintedPortrait
    {
        get => _hasPaintedPortrait;
        set => SetValue(ref _hasPaintedPortrait, value, nameof(HasPaintedPortrait));
    }

    [DataSourceProperty]
    public bool ShowCharacterViewport
    {
        get => _showCharacterViewport;
        set => SetValue(ref _showCharacterViewport, value, nameof(ShowCharacterViewport));
    }

    [DataSourceProperty]
    public string PaintedPortraitSprite
    {
        get => _paintedPortraitSprite;
        set => SetSprite(ref _paintedPortraitSprite, value, nameof(PaintedPortraitSprite));
    }

    [DataSourceProperty]
    public float PaintedPortraitWidth
    {
        get => _paintedPortraitWidth;
        set => SetValue(ref _paintedPortraitWidth, value, nameof(PaintedPortraitWidth));
    }

    [DataSourceProperty]
    public float PaintedPortraitHeight
    {
        get => _paintedPortraitHeight;
        set => SetValue(ref _paintedPortraitHeight, value, nameof(PaintedPortraitHeight));
    }

    [DataSourceProperty]
    public float MinimapGlowAlpha
    {
        get => _minimapGlowAlpha;
        set => SetValue(ref _minimapGlowAlpha, value, nameof(MinimapGlowAlpha));
    }

    [DataSourceProperty]
    public bool ShowLeaderCategory
    {
        get => _showLeaderCategory;
        set => SetValue(ref _showLeaderCategory, value, nameof(ShowLeaderCategory));
    }

    [DataSourceProperty]
    public string SelectedHeroPortraitSprite
    {
        get => _heroPortraitSprite;
        set
        {
            if (SetSprite(ref _heroPortraitSprite, value, nameof(SelectedHeroPortraitSprite)))
                OnPropertyChanged(nameof(HasSelectedHeroPortrait));
        }
    }

    [DataSourceProperty] public bool HasSelectedHeroPortrait => !string.IsNullOrEmpty(SelectedHeroPortraitSprite);

    [DataSourceProperty]
    public float SelectedHeroPortraitWidth
    {
        get => _heroPortraitWidth;
        set => SetValue(ref _heroPortraitWidth, value, nameof(SelectedHeroPortraitWidth));
    }

    [DataSourceProperty]
    public float SelectedHeroPortraitHeight
    {
        get => _heroPortraitHeight;
        set => SetValue(ref _heroPortraitHeight, value, nameof(SelectedHeroPortraitHeight));
    }

    [DataSourceProperty]
    public string SelectedHeroPortraitSpriteB
    {
        get => _heroPortraitSpriteB;
        set
        {
            if (SetSprite(ref _heroPortraitSpriteB, value, nameof(SelectedHeroPortraitSpriteB)))
                OnPropertyChanged(nameof(HasSelectedHeroPortraitB));
        }
    }

    [DataSourceProperty] public bool HasSelectedHeroPortraitB => !string.IsNullOrEmpty(SelectedHeroPortraitSpriteB);

    [DataSourceProperty]
    public float SelectedHeroPortraitWidthB
    {
        get => _heroPortraitWidthB;
        set => SetValue(ref _heroPortraitWidthB, value, nameof(SelectedHeroPortraitWidthB));
    }

    [DataSourceProperty]
    public float SelectedHeroPortraitHeightB
    {
        get => _heroPortraitHeightB;
        set => SetValue(ref _heroPortraitHeightB, value, nameof(SelectedHeroPortraitHeightB));
    }

    public void ExecuteBrowseLeader() => SetBrowseCategory(_browseCategory == LeaderCategory ? "" : LeaderCategory);

    public void ExecuteBrowseLord() => SetBrowseCategory(_browseCategory == LordCategory ? "" : LordCategory);

    public void ExecuteBrowseWanderer() => SetBrowseCategory(_browseCategory == WandererCategory ? "" : WandererCategory);

    public void ExecuteConfirm()
    {
        if (_selected != null)
            _factionMap.ConfirmRegion(_selected.RegionName);
    }

    public void ExecuteBack() => _factionMap.OnPreviousStage();

    public void ExecuteMinimapClick() => _services.Widgets.ToggleMinimap();

    public void ExecuteMinimapHoverBegin() => MinimapGlowAlpha = 0.28f;

    public void ExecuteMinimapHoverEnd() => MinimapGlowAlpha = 0f;

    public void ExecuteMinimapMapClick()
    {
        if (!_services.Widgets.TryGetMinimapClick(out var x, out var y))
            return;
        if (FactionRoster.NearestFaction(_factions, x, y) is { } nearest)
            SelectFaction(nearest);
    }

    /// <summary>Also empties the lists, so a closed faction screen holds no heroes or characters.</summary>
    public override void OnFinalize()
    {
        base.OnFinalize();
        Character.OnFinalize();
        foreach (var hero in Heroes.Concat(BrowseList))
            hero.OnFinalize();
        Heroes.Clear();
        BrowseList.Clear();
        Factions.Clear();
        _selected = null;
    }

    private void SelectFaction(FactionInfo info)
    {
        var replay = HasPaintedPortrait != !string.IsNullOrEmpty(info.PaintedPortraitSprite) || _selected?.Key != info.Key;
        _selected = info;
        foreach (var item in Factions)
            item.IsSelected = item.Info == info;

        _services.Sprites.EnsureArt(info.TerritorySprite);
        Selected = new FactionDetailVM(info);
        BackgroundSprite = info.BackgroundSprite;
        HoveredFactionName = info.Name;
        ShowFactionCharacter(info);
        _services.Widgets.ResetRightColumnToTop();

        var hasPortrait = !string.IsNullOrEmpty(info.PaintedPortraitSprite);
        PaintedPortraitSprite = info.PaintedPortraitSprite;
        PaintedPortraitWidth = info.PaintedPortraitWidth * info.PaintedPortraitScale;
        PaintedPortraitHeight = DefaultPortraitHeight * info.PaintedPortraitScale;
        HasPaintedPortrait = hasPortrait;
        ShowCharacterViewport = !hasPortrait;
        if (hasPortrait && replay)
            _services.Widgets.OnFactionSelected();

        SelectedHeroPortraitSprite = "";
        SelectedHeroPortraitSpriteB = "";
        _activeHeroPortraitIsB = false;
        _services.Widgets.ResetHeroPortraits();
        _services.Picks.Clear();

        ShowLeaderCategory = _services.Roster.ShowLeaderCategory(info);
        if (!ShowLeaderCategory && _browseCategory == LeaderCategory)
            _browseCategory = "";

        RefreshHeroes(info);
        RefreshBrowseList();
    }

    private void RefreshHeroes(FactionInfo info)
    {
        foreach (var old in Heroes)
            old.OnFinalize();
        Heroes.Clear();

        _services.Sprites.EnsureArt(FactionScreenArt.CustomCharacterPortrait);
        Heroes.Add(new HeroItemVM(
            new TextObject("{=taom_fui_custom_character}Custom Character").ToString(),
            FactionScreenArt.CustomCharacterPortrait,
            SelectHero));

        if (!FactionScreenArt.SpecialCharacters.TryGetValue(info.Key, out var cards))
            return;

        foreach (var card in cards)
        {
            // A card whose character TAOM lacks shows the culture's troop under the card's own name.
            var character = _services.Roster.Character(card.CharacterId);
            var pick = character ?? _services.Roster.CardCharacter(card, info.CultureId);
            _services.Sprites.EnsureArt(card.PortraitSprite);
            Heroes.Add(new HeroItemVM(character?.Name ?? card.FallbackName, pick, SelectHero)
            {
                PortraitSprite = card.PortraitSprite,
                RevealSprite = card.RevealSprite,
                RevealWidth = card.RevealWidth,
                RevealScale = card.RevealScale,
            });
        }
    }

    private void SetBrowseCategory(string category)
    {
        _browseCategory = category;
        OnPropertyChanged(nameof(IsLeaderCategorySelected));
        OnPropertyChanged(nameof(IsLordCategorySelected));
        OnPropertyChanged(nameof(IsWandererCategorySelected));
        RefreshBrowseList();
    }

    private void RefreshBrowseList()
    {
        foreach (var old in BrowseList)
            old.OnFinalize();
        BrowseList.Clear();
        if (_selected == null)
            return;

        switch (_browseCategory)
        {
            case LeaderCategory:
                if (_services.Roster.Ruler(_selected.KingdomId, aliveOnly: true) is { } ruler)
                    BrowseList.Add(new HeroItemVM(ruler.Name, ruler, SelectHero));
                break;
            case LordCategory:
                foreach (var lord in _services.Roster.Lords(_selected.KingdomId))
                    BrowseList.Add(new HeroItemVM(lord.Name, lord, SelectHero));
                break;
            case WandererCategory:
                foreach (var wanderer in _services.Roster.Wanderers(_selected.CultureId))
                    BrowseList.Add(new HeroItemVM(wanderer.Name, wanderer, SelectHero));
                break;
        }
    }

    private void SelectHero(HeroItemVM hero)
    {
        if (!hero.IsAvailable)
            return;

        foreach (var item in Heroes)
            item.IsSelected = item == hero;
        foreach (var item in BrowseList)
            item.IsSelected = item == hero;

        if (hero.IsCustom)
        {
            _services.Picks.Clear();
            SelectedHeroPortraitSprite = "";
            SelectedHeroPortraitSpriteB = "";
            ExecuteConfirm();
            return;
        }

        if (hero.Pick is not { } pick)
            return;
        _services.Picks.Pick(pick, _selected?.CultureId);

        var portrait = !string.IsNullOrEmpty(hero.RevealSprite) ? hero.RevealSprite : hero.PortraitSprite;
        if (!string.IsNullOrEmpty(portrait))
            ShowHeroPortrait(portrait, hero.RevealWidth * hero.RevealScale, DefaultPortraitHeight * hero.RevealScale);
    }

    private void ShowHeroPortrait(string sprite, float width, float height)
    {
        _activeHeroPortraitIsB = !_activeHeroPortraitIsB;
        if (_activeHeroPortraitIsB)
        {
            SelectedHeroPortraitSpriteB = sprite;
            SelectedHeroPortraitWidthB = width;
            SelectedHeroPortraitHeightB = height;
        }
        else
        {
            SelectedHeroPortraitSprite = sprite;
            SelectedHeroPortraitWidth = width;
            SelectedHeroPortraitHeight = height;
        }
        _services.Widgets.OnHeroSelected(_activeHeroPortraitIsB);
    }

    private void HoverFaction(FactionInfo? info) => HoveredFactionName = (info ?? _selected)?.Name ?? "";

    private void ShowFactionCharacter(FactionInfo info)
    {
        try
        {
            if (_services.Roster.ViewportCharacter(info)?.Source is not CharacterObject character)
                return;

            Character.FillFrom(character);
            if (_services.Roster.ViewportRace(info) is { } race)
                Character.Race = race;

            var culture = character.Culture ?? TaleWorlds.ObjectSystem.MBObjectManager.Instance?.GetObject<CultureObject>(info.CultureId);
            if (culture != null)
            {
                if (Character.ArmorColor1 == 0)
                    Character.ArmorColor1 = culture.Color;
                if (Character.ArmorColor2 == 0)
                    Character.ArmorColor2 = culture.Color2;
            }

            var equipment = character.Equipment?.Clone();
            if (equipment != null)
            {
                equipment[EquipmentIndex.Horse] = EquipmentElement.Invalid;
                equipment[EquipmentIndex.HorseHarness] = EquipmentElement.Invalid;
                if (_services.Roster.HideViewportWeapons(info))
                {
                    for (var slot = EquipmentIndex.WeaponItemBeginSlot; slot < EquipmentIndex.NumAllWeaponSlots; slot++)
                        equipment[slot] = EquipmentElement.Invalid;
                }
                Character.SetEquipment(equipment);
                Character.MountCreationKey = "";
                Character.HasMount = false;
            }

            ViewportOffset = _services.Roster.ViewportOffset(info);
            Character.IsTableauEnabled = true;
        }
        catch (Exception)
        {
            // A character the viewport cannot draw leaves the viewport empty, never the screen broken.
        }
    }

    private bool SetSprite(ref string field, string? value, string propertyName)
    {
        value ??= "";
        if (value.Length > 0)
            _services.Sprites.EnsureArt(value);
        return Set(ref field, value, propertyName);
    }

    private bool Set<T>(ref T field, T value, string propertyName) where T : class?
    {
        if (Equals(field, value))
            return false;
        field = value;
        OnPropertyChangedWithValue(value, propertyName);
        return true;
    }

    private void SetValue(ref float field, float value, string propertyName)
    {
        if (field == value)
            return;
        field = value;
        OnPropertyChangedWithValue(value, propertyName);
    }

    private void SetValue(ref bool field, bool value, string propertyName)
    {
        if (field == value)
            return;
        field = value;
        OnPropertyChangedWithValue(value, propertyName);
    }
}
