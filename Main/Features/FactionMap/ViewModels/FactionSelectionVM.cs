using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TAOM.Features.FactionMap.Models;
using TAOM.Features.FactionMap.Widgets;

namespace TAOM.Features.FactionMap.ViewModels;

public class FactionSelectionVM : ViewModel
{
    private readonly Action<CultureObject> _onCultureSelected;
    private readonly Action _onPreviousStage;
    private readonly IFactionSelectionService _selectionService;
    private readonly IFactionHoverService _hoverService;
    private readonly ICultureResolverService _cultureResolver;
    private readonly Action<string> _onCulturePreviewSelected;
    private string _selectedRegionName = "";
    private string _title = "Choose your Realm";
    private string _selectedFactionName = "";
    private string _selectedFactionDesc = "";
    private bool _hasSelection;
    private bool _selectedFactionPlayable;
    private bool _selectedHasCulture;
    private float _bannerPosX = -1f;
    private float _bannerPosY = -1f;
    private string _bannerColorHex = "#FFFFFFFF";
    private string _bannerImage = "";
    private string _bannerSide = "neutral";
    private bool _hasSpecialUnit;
    private string _factionImageId = "";
    private string _selectedFactionSide = "neutral";
    private int _difficulty;
    private string _difficultyText = "";
    private string _factionColorHex = "#1a1a2eFF";
    private string _factionAccentColorHex = "#835513FF";
    private bool _showLandmarks = true;

    public FactionSelectionVM(
        Action<CultureObject> onCultureSelected,
        Action onPreviousStage,
        IFactionSelectionService selectionService,
        IFactionHoverService hoverService,
        ICultureResolverService cultureResolver,
        ILandmarkService landmarkService,
        Action<string> onCulturePreviewSelected = null)
    {
        _onCultureSelected = onCultureSelected;
        _onPreviousStage = onPreviousStage;
        _selectionService = selectionService;
        _hoverService = hoverService;
        _cultureResolver = cultureResolver;
        _onCulturePreviewSelected = onCulturePreviewSelected;

        FactionTraits = new MBBindingList<FactionTraitItemVM>();
        FactionBonuses = new MBBindingList<FactionBonusItemVM>();
        FactionPerks = new MBBindingList<FactionPerkItemVM>();
        FactionSpecialUnits = new MBBindingList<FactionSpecialUnitItemVM>();
        FactionStrengths = new MBBindingList<FactionBonusItemVM>();
        FactionWeaknesses = new MBBindingList<FactionBonusItemVM>();
        FactionLandmarks = new MBBindingList<LandmarkItemVM>();
        AllLandmarks = new MBBindingList<LandmarkItemVM>();

        FactionDisplayHelper.LoadAllLandmarks(this, landmarkService);
    }

    [DataSourceProperty] public string Title { get => _title; set { if (_title != value) { _title = value; OnPropertyChangedWithValue(value); } } }
    [DataSourceProperty] public bool HasSelection { get => _hasSelection; set { if (value != _hasSelection) { _hasSelection = value; OnPropertyChangedWithValue(value); OnPropertyChanged(nameof(HasNoSelection)); OnPropertyChanged(nameof(CanConfirm)); } } }
    [DataSourceProperty] public bool HasNoSelection => !_hasSelection;
    [DataSourceProperty] public bool SelectedFactionPlayable { get => _selectedFactionPlayable; set { if (value != _selectedFactionPlayable) { _selectedFactionPlayable = value; OnPropertyChangedWithValue(value); OnPropertyChanged(nameof(SelectedFactionNotPlayable)); OnPropertyChanged(nameof(CanConfirm)); } } }
    [DataSourceProperty] public bool SelectedFactionNotPlayable => !_selectedFactionPlayable;
    [DataSourceProperty] public bool CanConfirm => _hasSelection && _selectedFactionPlayable && _selectedHasCulture;
    [DataSourceProperty] public string SelectedFactionName { get => _selectedFactionName; set { if (value != _selectedFactionName) { _selectedFactionName = value; OnPropertyChangedWithValue(value); } } }
    [DataSourceProperty] public string SelectedFactionDesc { get => _selectedFactionDesc; set { if (value != _selectedFactionDesc) { _selectedFactionDesc = value; OnPropertyChangedWithValue(value); } } }
    [DataSourceProperty] public MBBindingList<FactionTraitItemVM> FactionTraits { get; }
    [DataSourceProperty] public MBBindingList<FactionBonusItemVM> FactionBonuses { get; }
    [DataSourceProperty] public MBBindingList<FactionPerkItemVM> FactionPerks { get; }
    [DataSourceProperty] public MBBindingList<FactionSpecialUnitItemVM> FactionSpecialUnits { get; }
    [DataSourceProperty] public bool HasSpecialUnit { get => _hasSpecialUnit; set { if (value != _hasSpecialUnit) { _hasSpecialUnit = value; OnPropertyChangedWithValue(value); } } }
    [DataSourceProperty] public string FactionImageId { get => _factionImageId; set { if (value != _factionImageId) { _factionImageId = value; OnPropertyChangedWithValue(value); } } }
    [DataSourceProperty] public MBBindingList<FactionBonusItemVM> FactionStrengths { get; }
    [DataSourceProperty] public MBBindingList<FactionBonusItemVM> FactionWeaknesses { get; }
    [DataSourceProperty] public bool HasStrengths => FactionStrengths.Count > 0;
    [DataSourceProperty] public bool HasWeaknesses => FactionWeaknesses.Count > 0;
    [DataSourceProperty] public string SelectedFactionSide { get => _selectedFactionSide; set { if (value != _selectedFactionSide) { _selectedFactionSide = value; OnPropertyChangedWithValue(value); OnPropertyChanged(nameof(IsFreePeoples)); OnPropertyChanged(nameof(IsEvil)); OnPropertyChanged(nameof(SideDisplayText)); } } }
    [DataSourceProperty] public bool IsFreePeoples => _selectedFactionSide == "free";
    [DataSourceProperty] public bool IsEvil => _selectedFactionSide == "evil";
    [DataSourceProperty] public string SideDisplayText => _selectedFactionSide switch { "free" => "Free Peoples", "evil" => "Forces of Evil", _ => "Neutral" };
    [DataSourceProperty] public int Difficulty { get => _difficulty; set { if (value != _difficulty) { _difficulty = value; OnPropertyChangedWithValue(value); OnPropertyChanged(nameof(HasDifficulty)); } } }
    [DataSourceProperty] public bool HasDifficulty => _difficulty > 0;
    [DataSourceProperty] public string DifficultyText { get => _difficultyText; set { if (value != _difficultyText) { _difficultyText = value; OnPropertyChangedWithValue(value); } } }
    [DataSourceProperty] public string FactionColorHex { get => _factionColorHex; set { if (value != _factionColorHex) { _factionColorHex = value; OnPropertyChangedWithValue(value); } } }
    [DataSourceProperty] public string FactionAccentColorHex { get => _factionAccentColorHex; set { if (value != _factionAccentColorHex) { _factionAccentColorHex = value; OnPropertyChangedWithValue(value); } } }
    [DataSourceProperty] public MBBindingList<LandmarkItemVM> FactionLandmarks { get; }
    [DataSourceProperty] public MBBindingList<LandmarkItemVM> AllLandmarks { get; }
    [DataSourceProperty] public bool ShowLandmarks { get => _showLandmarks; set { if (value != _showLandmarks) { _showLandmarks = value; OnPropertyChangedWithValue(value); } } }
    [DataSourceProperty] public float BannerPosX { get => _bannerPosX; set { if (Math.Abs(_bannerPosX - value) > 0.0001f) { _bannerPosX = value; OnPropertyChangedWithValue(value); } } }
    [DataSourceProperty] public float BannerPosY { get => _bannerPosY; set { if (Math.Abs(_bannerPosY - value) > 0.0001f) { _bannerPosY = value; OnPropertyChangedWithValue(value); } } }
    [DataSourceProperty] public string BannerColorHex { get => _bannerColorHex; set { if (_bannerColorHex != value) { _bannerColorHex = value; OnPropertyChangedWithValue(value); } } }
    [DataSourceProperty] public string BannerImage { get => _bannerImage; set { if (_bannerImage != value) { _bannerImage = value; OnPropertyChangedWithValue(value); } } }
    [DataSourceProperty] public string BannerSide { get => _bannerSide; set { if (_bannerSide != value) { _bannerSide = value; OnPropertyChangedWithValue(value); } } }

    public bool SelectedHasCulture { get => _selectedHasCulture; set { if (value != _selectedHasCulture) { _selectedHasCulture = value; OnPropertyChanged(nameof(CanConfirm)); } } }
    public void Tick()
    {
        string current = PolygonWidget.HoveredFactionName ?? "";
        var change = _hoverService.UpdateHover(current);
        if (change != null)
            FactionDisplayHelper.ShowHoverTooltip(change);
    }

    public void ExecuteSelectRegion()
    {
        string regionName = PolygonWidget.LastClickedRegionName;
        if (string.IsNullOrEmpty(regionName)) return;

        _selectedRegionName = regionName;
        var result = _selectionService.SelectRegion(regionName);
        FactionDisplayHelper.ApplyResult(this, result);
        SignalCulturePreview(result);
    }

    public void ExecuteConfirm()
    {
        if (string.IsNullOrEmpty(_selectedRegionName)) return;

        var cultureId = _selectionService.GetCultureIdForRegion(_selectedRegionName);
        if (string.IsNullOrEmpty(cultureId))
        {
            FactionMapPaths.LogError($"No game_faction mapped for {_selectedRegionName}");
            return;
        }

        var cultureObj = _cultureResolver.ResolveCulture(cultureId);
        if (cultureObj is not CultureObject culture)
        {
            FactionMapPaths.LogError($"Culture '{cultureId}' not available!");
            return;
        }

        FactionMapPaths.Log($"Faction confirmed: {_selectedFactionName} -> {culture.Name}");
        _onCultureSelected?.Invoke(culture);
    }

    public void OnPreviousStage() => _onPreviousStage?.Invoke();

    public override void OnFinalize()
    {
        base.OnFinalize();
        _hoverService.Reset();
        FactionDisplayHelper.Finalize(this);
    }

    private void SignalCulturePreview(FactionSelectionResult result)
    {
        if (_onCulturePreviewSelected == null ||
            result == null ||
            !result.Found ||
            !result.Playable ||
            !result.HasCulture ||
            string.IsNullOrWhiteSpace(result.CultureId))
        {
            return;
        }

        _onCulturePreviewSelected(result.CultureId);
    }
}
