using System;
using System.Linq;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Client.Features.FordsOfWar;

/// <summary>
/// The part of the command panel that differs by faction: its name and accent colour, the introduction during the countdown, Gondor's
/// region strip and Mordor's creature call. A faction without a mechanic hides that row, and the buy grid below it stays where it is.
/// </summary>
public sealed partial class FordsCommandVM
{
    private bool _isUpgradesVisible = true;
    private string _enemyFactionName = string.Empty;
    private string _speedText = string.Empty;
    /// <summary>The width in pixels of the region and creature timer bars; the prefab's tracks are this wide.</summary>
    internal const float TimerBarWidth = 120f;

    /// <summary>The keys of the creature row, left to right.</summary>
    internal static readonly string[] CreatureKeys = { "G", "B", "N", "M" };

    private string _factionName = string.Empty;
    private string _accentColor = FordsFactionStyle.DefaultAccent;
    private string _incomeText = string.Empty;
    private bool _isIntroVisible;
    private string _introTitle = string.Empty;
    private string _formingText = string.Empty;
    private bool _isRegionVisible;
    private bool _isRegionPickerVisible;
    private bool _canCallRegion;
    private string _regionName = string.Empty;
    private string _regionSeconds = string.Empty;
    private string _nextRegionText = string.Empty;
    private string _callRegionText = string.Empty;
    private float _regionFillWidth;
    private bool _isCreatureVisible;
    private string _creatureTitle = string.Empty;
    private string _creatureSeconds = string.Empty;
    private float _creatureFillWidth;

    /// <summary>The speed the view applies to the mission each frame.</summary>
    internal FordsGameSpeed Speed { get; } = new();

    [DataSourceProperty]
    public string SpeedText
    {
        get => _speedText;
        private set => Set(ref _speedText, value, nameof(SpeedText));
    }

    [DataSourceProperty]
    public string EnemyFactionName
    {
        get => _enemyFactionName;
        private set => Set(ref _enemyFactionName, value, nameof(EnemyFactionName));
    }

    [DataSourceProperty]
    public string FactionName
    {
        get => _factionName;
        private set => Set(ref _factionName, value, nameof(FactionName));
    }

    [DataSourceProperty]
    public string AccentColor
    {
        get => _accentColor;
        private set => Set(ref _accentColor, value, nameof(AccentColor));
    }

    [DataSourceProperty]
    public string IncomeText
    {
        get => _incomeText;
        private set => Set(ref _incomeText, value, nameof(IncomeText));
    }

    [DataSourceProperty]
    public bool IsIntroVisible
    {
        get => _isIntroVisible;
        private set => Set(ref _isIntroVisible, value, nameof(IsIntroVisible));
    }

    [DataSourceProperty]
    public string IntroTitle
    {
        get => _introTitle;
        private set => Set(ref _introTitle, value, nameof(IntroTitle));
    }

    [DataSourceProperty]
    public string FormingText
    {
        get => _formingText;
        private set => Set(ref _formingText, value, nameof(FormingText));
    }

    [DataSourceProperty]
    public MBBindingList<FordsTraitVM> IntroTraits { get; } = new();

    [DataSourceProperty]
    public MBBindingList<FordsCampVM> Camps { get; } = new();

    [DataSourceProperty]
    public MBBindingList<FordsUpgradeVM> Upgrades { get; } = new();

    [DataSourceProperty]
    public bool IsUpgradesVisible
    {
        get => _isUpgradesVisible;
        private set => Set(ref _isUpgradesVisible, value, nameof(IsUpgradesVisible));
    }

    [DataSourceProperty]
    public string UpgradesTitle => FordsHudText.UpgradesTitle();

    [DataSourceProperty]
    public bool IsRegionVisible
    {
        get => _isRegionVisible;
        private set => Set(ref _isRegionVisible, value, nameof(IsRegionVisible));
    }

    [DataSourceProperty]
    public bool IsRegionPickerVisible
    {
        get => _isRegionPickerVisible;
        private set => Set(ref _isRegionPickerVisible, value, nameof(IsRegionPickerVisible));
    }

    [DataSourceProperty]
    public bool CanCallRegion
    {
        get => _canCallRegion;
        private set => Set(ref _canCallRegion, value, nameof(CanCallRegion));
    }

    [DataSourceProperty]
    public string RegionName
    {
        get => _regionName;
        private set => Set(ref _regionName, value, nameof(RegionName));
    }

    [DataSourceProperty]
    public string RegionSeconds
    {
        get => _regionSeconds;
        private set => Set(ref _regionSeconds, value, nameof(RegionSeconds));
    }

    [DataSourceProperty]
    public string NextRegionText
    {
        get => _nextRegionText;
        private set => Set(ref _nextRegionText, value, nameof(NextRegionText));
    }

    [DataSourceProperty]
    public string CallRegionText
    {
        get => _callRegionText;
        private set => Set(ref _callRegionText, value, nameof(CallRegionText));
    }

    [DataSourceProperty]
    public float RegionFillWidth
    {
        get => _regionFillWidth;
        private set => Set(ref _regionFillWidth, value, nameof(RegionFillWidth));
    }

    [DataSourceProperty]
    public MBBindingList<FordsRegionChoiceVM> RegionChoices { get; } = new();

    [DataSourceProperty]
    public bool IsCreatureVisible
    {
        get => _isCreatureVisible;
        private set => Set(ref _isCreatureVisible, value, nameof(IsCreatureVisible));
    }

    [DataSourceProperty]
    public string CreatureTitle
    {
        get => _creatureTitle;
        private set => Set(ref _creatureTitle, value, nameof(CreatureTitle));
    }

    [DataSourceProperty]
    public string CreatureSeconds
    {
        get => _creatureSeconds;
        private set => Set(ref _creatureSeconds, value, nameof(CreatureSeconds));
    }

    [DataSourceProperty]
    public float CreatureFillWidth
    {
        get => _creatureFillWidth;
        private set => Set(ref _creatureFillWidth, value, nameof(CreatureFillWidth));
    }

    [DataSourceProperty]
    public MBBindingList<FordsCreatureVM> Creatures { get; } = new();

    public void ExecuteToggleRegionPicker() => ToggleRegionPicker();

    /// <summary>The R key and the call button: shows or hides the list of regions.</summary>
    internal void ToggleRegionPicker()
    {
        if (_view?.Region == null)
            return;

        IsRegionPickerVisible = !IsRegionPickerVisible;
        Refresh();
    }

    internal void ToggleUpgrades() => IsUpgradesVisible = !IsUpgradesVisible;

    public void ExecuteCycleSpeed() => CycleSpeed();

    internal void CycleSpeed()
    {
        Speed.Cycle();
        SpeedText = FordsHudText.SpeedButton(Speed.Current);
    }

    /// <summary>The row's key or click: buys that track's next level for the whole army.</summary>
    internal void BuyUpgrade(int index)
    {
        var upgrades = _view?.Upgrades;
        if (upgrades == null || index < 0 || index >= upgrades.Count)
            return;

        Report("BuyUpgrade", _session.TryBuyUpgrade(upgrades[index].Id));
    }

    /// <summary>The row's creature key: calls that creature into the selected lane.</summary>
    internal void CallCreature(int index)
    {
        var offers = _view?.Creatures?.Offers;
        if (offers == null || index < 0 || index >= offers.Count)
            return;

        Report("CallCreature", _session.TryCallCreature(SelectedLane, offers[index].OfferId));
    }

    private void CallRegion(string regionId)
    {
        IsRegionPickerVisible = false;
        Report("CallRegion", _session.TryCallRegion(regionId));
    }

    private void RefreshIdentity(MatchView view)
    {
        FactionName = view.FactionName.Length == 0 ? string.Empty : new TextObject(view.FactionName).ToString();
        EnemyFactionName = view.EnemyFactionName.Length == 0 ? string.Empty : new TextObject(view.EnemyFactionName).ToString();
        AccentColor = FordsFactionStyle.Accent(view.FactionId);
        SpeedText = FordsHudText.SpeedButton(Speed.Current);
        IncomeText = view.IncomePerMinute > 0 ? FordsHudText.Income(view.IncomePerMinute) : string.Empty;

        var traits = view.Traits;
        IsIntroVisible = view.Phase == MatchPhase.Countdown && traits != null && traits.Count > 0;
        IntroTitle = FordsHudText.YouCommand(FactionName);
        FormingText = FordsHudText.Forming();
        var count = traits?.Count ?? 0;
        Sync(IntroTraits, count, _ => new FordsTraitVM());
        for (var i = 0; i < count; i++)
            IntroTraits[i].Update(traits![i]);
    }

    private void RefreshRegion(MatchView view)
    {
        var region = view.Region;
        IsRegionVisible = region != null;
        if (region == null)
        {
            IsRegionPickerVisible = false;
            Sync(RegionChoices, 0, _ => new FordsRegionChoiceVM(CallRegion));
            return;
        }

        var live = IsLive(view);
        RegionName = new TextObject(region.Name).ToString();
        RegionSeconds = FordsHudText.Seconds((int)Math.Ceiling(region.SecondsLeft));
        RegionFillWidth = Fill(region.SecondsLeft, region.TotalSeconds);
        NextRegionText = FordsHudText.RegionNext(new TextObject(region.NextName).ToString());
        CallRegionText = FordsHudText.RegionCall(region.CallFee);
        CanCallRegion = live && region.CanAffordCall;
        if (!CanCallRegion)
            IsRegionPickerVisible = false;

        var others = region.Choices.Where(c => c.Id != region.Id).ToList();
        Sync(RegionChoices, others.Count, _ => new FordsRegionChoiceVM(CallRegion));
        for (var i = 0; i < others.Count; i++)
            RegionChoices[i].Update(others[i].Id, new TextObject(others[i].Name).ToString(), CanCallRegion);
    }

    private void RefreshCreatures(MatchView view)
    {
        var call = view.Creatures;
        IsCreatureVisible = call != null;
        var count = call?.Offers.Count ?? 0;
        Sync(Creatures, count, i => new FordsCreatureVM(i, CallCreature));
        if (call == null)
            return;

        var live = IsLive(view);
        var ready = call.CooldownLeft <= 0f;
        CreatureTitle = FordsHudText.CreatureTitle();
        CreatureSeconds = ready ? FordsHudText.CreatureReady() : FordsHudText.Seconds((int)Math.Ceiling(call.CooldownLeft));
        CreatureFillWidth = ready ? TimerBarWidth : TimerBarWidth - Fill(call.CooldownLeft, call.CooldownTotal);
        for (var i = 0; i < count; i++)
            Creatures[i].Update(call.Offers[i], NameOf(call.Offers[i].TroopId), live && ready);
    }

    private void RefreshUpgrades(MatchView view)
    {
        var upgrades = view.Upgrades ?? Array.Empty<MatchView.UpgradeView>();
        Sync(Upgrades, upgrades.Count, i => new FordsUpgradeVM(i, BuyUpgrade));
        for (var i = 0; i < upgrades.Count; i++)
            Upgrades[i].Update(upgrades[i], view.Phase != MatchPhase.Ended);
    }

    private void RefreshCamps(MatchView view)
    {
        var camps = view.Camps ?? Array.Empty<MatchView.CampView>();
        Sync(Camps, camps.Count, _ => new FordsCampVM());
        for (var i = 0; i < camps.Count; i++)
            Camps[i].Update(i + 1, camps[i]);
    }

    private static float Fill(float left, float total) =>
        total <= 0f ? 0f : (float)Math.Round(TimerBarWidth * Math.Max(0f, Math.Min(1f, left / total)));
}
