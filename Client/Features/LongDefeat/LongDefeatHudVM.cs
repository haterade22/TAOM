using System;
using System.Linq;
using TaleWorlds.Library;
using TAOM.Features.LongDefeat;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Client.Features.LongDefeat;

/// <summary>
/// The view model both Long Defeat movies bind: the HUD strip and the panel (shop and end summary). It reads the run only through
/// <see cref="LongDefeatSession"/>, a copy per refresh, and commands go back through the session. The shop opens on B and closes by
/// itself when a wave starts; the end summary opens by itself when the run has a result, and stays readable after the mission is cleared.
/// </summary>
public sealed class LongDefeatHudVM : LongDefeatViewModel
{
    internal const float NoticeSeconds = 4f;
    internal const float KeepTrackWidth = 300f;

    private readonly LongDefeatSession _session;
    private readonly Action _leave;
    private bool _shopWanted;
    private bool _endDismissed;
    private float _noticeLeft;
    private string _waveText = string.Empty;
    private string _timerText = string.Empty;
    private string _goldText = string.Empty;
    private string _attackersText = string.Empty;
    private string _defendersText = string.Empty;
    private string _keepText = string.Empty;
    private float _keepFillWidth;
    private bool _isKeepVisible;
    private string _hintText = string.Empty;
    private string _affixText = string.Empty;
    private string _commanderText = string.Empty;
    private string _volleyText = string.Empty;
    private string _speedText = string.Empty;
    private string _speedTitle = string.Empty;
    private string _noticeText = string.Empty;
    private bool _isShopVisible;
    private string _shopTitle = string.Empty;
    private string _hornText = string.Empty;
    private string _pickTitle = string.Empty;
    private string _rerollText = string.Empty;
    private bool _isPickVisible;
    private bool _canReroll;
    private string _offerKey = string.Empty;
    private string _closeText = string.Empty;
    private bool _isEndVisible;
    private string _endTitle = string.Empty;
    private string _endLines = string.Empty;
    private string _bestText = string.Empty;
    private string _reportText = string.Empty;
    private string _leaveText = string.Empty;
    private bool _fortTab;
    private bool _boonsHidden;
    private string _waveLabel = string.Empty;
    private string _classLabel = string.Empty;
    private bool _hasClass;
    private string _goldValue = string.Empty;
    private string _speedValue = string.Empty;
    private bool _isTimerVisible;
    private string _shopTimerText = string.Empty;
    private string _reinforcementsText = string.Empty;
    private string _fortificationsText = string.Empty;
    private bool _isReinforcementsTab = true;
    private bool _isFortificationsTab;
    private bool _hasFortTab;
    private string _shopHintText = string.Empty;
    private bool _isOffersEmpty;
    private string _noOffersText = string.Empty;
    private string _pickSubtitle = string.Empty;
    private bool _isBoonPanelVisible;
    private string _keyShopText = string.Empty;
    private string _keyHornText = string.Empty;
    private string _keySpeedText = string.Empty;
    private string _keyVolleyText = string.Empty;
    private bool _isShopKeyEnabled;
    private bool _isHornKeyEnabled;
    private bool _isVolleyKeyEnabled;
    private string _keysLockedText = string.Empty;
    private bool _isKeysLocked;
    private string _boonHintText = string.Empty;
    private bool _isBoonHintVisible;
    private string _upgradesTitle = string.Empty;
    private bool _hasUpgrades;

    internal LongDefeatHudVM(LongDefeatSession session, Action leave)
    {
        _session = session;
        _leave = leave;
        Offers = new MBBindingList<LongDefeatOfferVM>();
        Upgrades = new MBBindingList<LongDefeatUpgradeVM>();
        Boons = new MBBindingList<LongDefeatBoonVM>();
        Speeds = new MBBindingList<LongDefeatSpeedVM>();
        Fort = new LongDefeatFortVM(BuyCare, BuyGate, RepairGate, BuyVolley, BuyTowers);
        foreach (var level in LongDefeatSession.SpeedLevels)
            Speeds.Add(new LongDefeatSpeedVM(level, SelectSpeed));
    }

    internal bool WantsPanel => IsShopVisible || IsEndVisible;

    [DataSourceProperty]
    public string WaveText { get => _waveText; private set => Set(ref _waveText, value, nameof(WaveText)); }

    [DataSourceProperty]
    public string TimerText { get => _timerText; private set => Set(ref _timerText, value, nameof(TimerText)); }

    [DataSourceProperty]
    public string GoldText { get => _goldText; private set => Set(ref _goldText, value, nameof(GoldText)); }

    [DataSourceProperty]
    public string AttackersText { get => _attackersText; private set => Set(ref _attackersText, value, nameof(AttackersText)); }

    [DataSourceProperty]
    public string DefendersText { get => _defendersText; private set => Set(ref _defendersText, value, nameof(DefendersText)); }

    [DataSourceProperty]
    public string KeepText { get => _keepText; private set => Set(ref _keepText, value, nameof(KeepText)); }

    [DataSourceProperty]
    public float KeepFillWidth { get => _keepFillWidth; private set => Set(ref _keepFillWidth, value, nameof(KeepFillWidth)); }

    [DataSourceProperty]
    public bool IsKeepVisible { get => _isKeepVisible; private set => Set(ref _isKeepVisible, value, nameof(IsKeepVisible)); }

    [DataSourceProperty]
    public string HintText { get => _hintText; private set => Set(ref _hintText, value, nameof(HintText)); }

    [DataSourceProperty]
    public string AffixText { get => _affixText; private set => Set(ref _affixText, value, nameof(AffixText)); }

    [DataSourceProperty]
    public string CommanderText { get => _commanderText; private set => Set(ref _commanderText, value, nameof(CommanderText)); }

    [DataSourceProperty]
    public string VolleyText { get => _volleyText; private set => Set(ref _volleyText, value, nameof(VolleyText)); }

    [DataSourceProperty]
    public string SpeedText { get => _speedText; private set => Set(ref _speedText, value, nameof(SpeedText)); }

    [DataSourceProperty]
    public string SpeedTitle { get => _speedTitle; private set => Set(ref _speedTitle, value, nameof(SpeedTitle)); }

    [DataSourceProperty]
    public MBBindingList<LongDefeatSpeedVM> Speeds { get; }

    [DataSourceProperty]
    public string NoticeText { get => _noticeText; private set => Set(ref _noticeText, value, nameof(NoticeText)); }

    [DataSourceProperty]
    public bool IsShopVisible { get => _isShopVisible; private set => Set(ref _isShopVisible, value, nameof(IsShopVisible)); }

    [DataSourceProperty]
    public string ShopTitle { get => _shopTitle; private set => Set(ref _shopTitle, value, nameof(ShopTitle)); }

    [DataSourceProperty]
    public string HornText { get => _hornText; private set => Set(ref _hornText, value, nameof(HornText)); }

    [DataSourceProperty]
    public string CloseText { get => _closeText; private set => Set(ref _closeText, value, nameof(CloseText)); }

    [DataSourceProperty]
    public MBBindingList<LongDefeatOfferVM> Offers { get; }

    [DataSourceProperty]
    public LongDefeatFortVM Fort { get; }

    /// <summary>The upgrade steps the shop offers now (#1161): one row per step whose troop has a soldier standing and not yet sold.</summary>
    [DataSourceProperty]
    public MBBindingList<LongDefeatUpgradeVM> Upgrades { get; }

    [DataSourceProperty]
    public string UpgradesTitle { get => _upgradesTitle; private set => Set(ref _upgradesTitle, value, nameof(UpgradesTitle)); }

    [DataSourceProperty]
    public bool HasUpgrades { get => _hasUpgrades; private set => Set(ref _hasUpgrades, value, nameof(HasUpgrades)); }

    [DataSourceProperty]
    public bool IsPickVisible { get => _isPickVisible; private set => Set(ref _isPickVisible, value, nameof(IsPickVisible)); }

    [DataSourceProperty]
    public string PickTitle { get => _pickTitle; private set => Set(ref _pickTitle, value, nameof(PickTitle)); }

    [DataSourceProperty]
    public string RerollText { get => _rerollText; private set => Set(ref _rerollText, value, nameof(RerollText)); }

    [DataSourceProperty]
    public bool CanReroll { get => _canReroll; private set => Set(ref _canReroll, value, nameof(CanReroll)); }

    [DataSourceProperty]
    public MBBindingList<LongDefeatBoonVM> Boons { get; }

    [DataSourceProperty]
    public bool IsEndVisible { get => _isEndVisible; private set => Set(ref _isEndVisible, value, nameof(IsEndVisible)); }

    [DataSourceProperty]
    public string EndTitle { get => _endTitle; private set => Set(ref _endTitle, value, nameof(EndTitle)); }

    [DataSourceProperty]
    public string EndLines { get => _endLines; private set => Set(ref _endLines, value, nameof(EndLines)); }

    [DataSourceProperty]
    public string BestText { get => _bestText; private set => Set(ref _bestText, value, nameof(BestText)); }

    [DataSourceProperty]
    public string ReportText { get => _reportText; private set => Set(ref _reportText, value, nameof(ReportText)); }

    [DataSourceProperty]
    public string LeaveText { get => _leaveText; private set => Set(ref _leaveText, value, nameof(LeaveText)); }

    // The themed movies (TAOMLongDefeatHud, TAOMLongDefeatPanel) bind the properties below; the original movies keep the ones above.

    [DataSourceProperty]
    public string WaveLabel { get => _waveLabel; private set => Set(ref _waveLabel, value, nameof(WaveLabel)); }

    [DataSourceProperty]
    public string ClassLabel { get => _classLabel; private set => Set(ref _classLabel, value, nameof(ClassLabel)); }

    [DataSourceProperty]
    public bool HasClass { get => _hasClass; private set => Set(ref _hasClass, value, nameof(HasClass)); }

    [DataSourceProperty]
    public string GoldValue { get => _goldValue; private set => Set(ref _goldValue, value, nameof(GoldValue)); }

    [DataSourceProperty]
    public string SpeedValue { get => _speedValue; private set => Set(ref _speedValue, value, nameof(SpeedValue)); }

    [DataSourceProperty]
    public bool IsTimerVisible { get => _isTimerVisible; private set => Set(ref _isTimerVisible, value, nameof(IsTimerVisible)); }

    [DataSourceProperty]
    public string ShopTimerText { get => _shopTimerText; private set => Set(ref _shopTimerText, value, nameof(ShopTimerText)); }

    [DataSourceProperty]
    public string ReinforcementsText { get => _reinforcementsText; private set => Set(ref _reinforcementsText, value, nameof(ReinforcementsText)); }

    [DataSourceProperty]
    public string FortificationsText { get => _fortificationsText; private set => Set(ref _fortificationsText, value, nameof(FortificationsText)); }

    [DataSourceProperty]
    public bool IsReinforcementsTab { get => _isReinforcementsTab; private set => Set(ref _isReinforcementsTab, value, nameof(IsReinforcementsTab)); }

    [DataSourceProperty]
    public bool IsFortificationsTab { get => _isFortificationsTab; private set => Set(ref _isFortificationsTab, value, nameof(IsFortificationsTab)); }

    [DataSourceProperty]
    public bool HasFortTab { get => _hasFortTab; private set => Set(ref _hasFortTab, value, nameof(HasFortTab)); }

    [DataSourceProperty]
    public string ShopHintText { get => _shopHintText; private set => Set(ref _shopHintText, value, nameof(ShopHintText)); }

    [DataSourceProperty]
    public bool IsOffersEmpty { get => _isOffersEmpty; private set => Set(ref _isOffersEmpty, value, nameof(IsOffersEmpty)); }

    [DataSourceProperty]
    public string NoOffersText { get => _noOffersText; private set => Set(ref _noOffersText, value, nameof(NoOffersText)); }

    [DataSourceProperty]
    public string PickSubtitle { get => _pickSubtitle; private set => Set(ref _pickSubtitle, value, nameof(PickSubtitle)); }

    [DataSourceProperty]
    public bool IsBoonPanelVisible { get => _isBoonPanelVisible; private set => Set(ref _isBoonPanelVisible, value, nameof(IsBoonPanelVisible)); }

    [DataSourceProperty]
    public string KeyShopText { get => _keyShopText; private set => Set(ref _keyShopText, value, nameof(KeyShopText)); }

    [DataSourceProperty]
    public string KeyHornText { get => _keyHornText; private set => Set(ref _keyHornText, value, nameof(KeyHornText)); }

    [DataSourceProperty]
    public string KeySpeedText { get => _keySpeedText; private set => Set(ref _keySpeedText, value, nameof(KeySpeedText)); }

    [DataSourceProperty]
    public string KeyVolleyText { get => _keyVolleyText; private set => Set(ref _keyVolleyText, value, nameof(KeyVolleyText)); }

    [DataSourceProperty]
    public bool IsShopKeyEnabled { get => _isShopKeyEnabled; private set => Set(ref _isShopKeyEnabled, value, nameof(IsShopKeyEnabled)); }

    [DataSourceProperty]
    public bool IsHornKeyEnabled { get => _isHornKeyEnabled; private set => Set(ref _isHornKeyEnabled, value, nameof(IsHornKeyEnabled)); }

    [DataSourceProperty]
    public bool IsVolleyKeyEnabled { get => _isVolleyKeyEnabled; private set => Set(ref _isVolleyKeyEnabled, value, nameof(IsVolleyKeyEnabled)); }

    [DataSourceProperty]
    public string KeysLockedText { get => _keysLockedText; private set => Set(ref _keysLockedText, value, nameof(KeysLockedText)); }

    [DataSourceProperty]
    public bool IsKeysLocked { get => _isKeysLocked; private set => Set(ref _isKeysLocked, value, nameof(IsKeysLocked)); }

    [DataSourceProperty]
    public string BoonHintText { get => _boonHintText; private set => Set(ref _boonHintText, value, nameof(BoonHintText)); }

    [DataSourceProperty]
    public bool IsBoonHintVisible { get => _isBoonHintVisible; private set => Set(ref _isBoonHintVisible, value, nameof(IsBoonHintVisible)); }

    public void ExecuteShowReinforcements() => ShowTab(fortifications: false);

    public void ExecuteShowFortifications() => ShowTab(fortifications: true);

    /// <summary>Hides the boon panel until the shop is next opened. The pick stays on offer: nothing is taken or rerolled.</summary>
    public void ExecuteHideBoons()
    {
        _boonsHidden = true;
        Refresh();
    }

    private void ShowTab(bool fortifications)
    {
        _fortTab = fortifications;
        Refresh();
    }

    public void ExecuteHorn() => SoundHorn();

    public void ExecuteClose()
    {
        if (IsShopVisible)
            _shopWanted = false;
        else if (IsEndVisible)
            _endDismissed = true;
        Refresh();
    }

    public void ExecuteLeave() => _leave();

    public void ExecuteReroll()
    {
        Notify(LongDefeatHudText.Refusal(_session.TryReroll()));
        Refresh();
    }

    /// <summary>Width of the keep bar's fill for a progress of 0 to 1. A positive requirement, so a NaN reads as empty.</summary>
    internal static float KeepWidth(float progress) => progress > 0f ? Math.Min(progress, 1f) * KeepTrackWidth : 0f;

    internal void Tick(float dt)
    {
        if (!(dt > 0f) || float.IsInfinity(dt) || _noticeLeft <= 0f)
            return;

        _noticeLeft -= dt;
        if (_noticeLeft <= 0f)
            NoticeText = string.Empty;
    }

    internal void ToggleShop()
    {
        var view = _session.GetView();
        if (view == null || _session.Result != null)
            return;

        if (!view.ShopOpen)
        {
            Notify(LongDefeatHudText.Refusal(BuyResult.NotOpen));
            return;
        }

        _shopWanted = !_shopWanted;
        if (_shopWanted)
        {
            _fortTab = false;
            _boonsHidden = false;
        }
        Refresh();
    }

    /// <summary>The speed key: the next of 1x, 2x, 4x and 10x, then back to 1x.</summary>
    internal void CycleSpeed()
    {
        _session.CycleSpeed();
        Refresh();
    }

    private void SelectSpeed(int speed)
    {
        _session.SetSpeed(speed);
        Refresh();
    }

    internal void SoundHorn()
    {
        Notify(LongDefeatHudText.Refusal(_session.TryStartWave()));
        Refresh();
    }

    internal void Refresh()
    {
        var view = _session.GetView();
        var result = _session.Result;
        if (view == null && result == null)
            return;

        ApplySpeed();
        if (view != null)
            ApplyStrip(view);
        if (view == null || !view.ShopOpen || result != null)
            _shopWanted = false;

        IsShopVisible = _shopWanted;
        if (view != null && _shopWanted)
        {
            ShopTitle = LongDefeatHudText.ShopTitle();
            HornText = LongDefeatHudText.Horn();
            CloseText = LongDefeatHudText.Close();
            ApplyOffers(view);
            ApplyUpgrades(view);
            Fort.Update(view);
            ApplyShop(view);
        }
        else
        {
            Fort.Hide();
        }

        ApplyBoons(view);

        IsEndVisible = result != null && !_endDismissed;
        if (result != null)
        {
            EndTitle = LongDefeatHudText.EndTitle(result.Reason);
            EndLines = LongDefeatHudText.EndLines(result.WavesCleared, result.Kills, _session.LostInClearedWaves, _session.BoonsTaken, _session.ClassName);
            ReportText = _session.ReportPath is { } path ? LongDefeatHudText.ReportFile(System.IO.Path.GetFileName(path)) : string.Empty;
            BestText = _session.Best is { } best ? LongDefeatHudText.Best(best.Waves, best.Kills) : LongDefeatHudText.NoBest();
            LeaveText = LongDefeatHudText.Leave();
        }
    }

    private void ApplyStrip(RunView view)
    {
        WaveText = LongDefeatHudText.WithClass(LongDefeatHudText.Wave(view.Phase, view.Wave), view.ClassName);
        TimerText = view.ShopOpen ? LongDefeatHudText.Clock(view.SecondsLeft) : string.Empty;
        GoldText = LongDefeatHudText.Gold(view.Gold);
        AttackersText = LongDefeatHudText.Attackers(view.AttackersStanding);
        DefendersText = LongDefeatHudText.Defenders(view.DefendersStanding + view.DefendersPending);
        HintText = LongDefeatHudText.Hint(view);
        AffixText = LongDefeatHudText.AffixLine(view);
        CommanderText = LongDefeatHudText.CommanderLine(view);
        VolleyText = LongDefeatHudText.VolleyLine(view);
        IsKeepVisible = view.Phase == RunPhase.Wave && view.KeepProgress > 0f;
        KeepFillWidth = IsKeepVisible ? KeepWidth(view.KeepProgress) : 0f;
        KeepText = IsKeepVisible ? LongDefeatHudText.Keep() : string.Empty;
        WaveLabel = LongDefeatHudText.Wave(view.Phase, view.Wave);
        ClassLabel = LongDefeatHudText.ClassLabel(view.ClassName);
        HasClass = ClassLabel.Length > 0;
        GoldValue = LongDefeatHudText.GoldValue(view.Gold);
        IsTimerVisible = view.ShopOpen;
        ApplyKeys(view);
    }

    /// <summary>The themed key strip: B and H usable only while the shop is open, N from the run's own volley state, and the line above the strip (the reason B and H are grey, or the open boon pick).</summary>
    private void ApplyKeys(RunView view)
    {
        KeyShopText = LongDefeatHudText.ShopTitle();
        KeyHornText = LongDefeatHudText.KeyHorn();
        KeyVolleyText = LongDefeatHudText.KeyVolley(view);
        IsShopKeyEnabled = view.ShopOpen;
        IsHornKeyEnabled = view.ShopOpen;
        IsVolleyKeyEnabled = view.Phase == RunPhase.Wave && view.Volleys.Owned && view.Volleys.Ready;
        IsKeysLocked = view.Phase == RunPhase.Wave;
        KeysLockedText = IsKeysLocked ? LongDefeatHudText.KeysLocked() : string.Empty;
        IsBoonHintVisible = view.ShopOpen && view.BoonOffer.Count > 0 && !_shopWanted;
        BoonHintText = IsBoonHintVisible ? LongDefeatHudText.BoonAwaits() : string.Empty;
    }

    /// <summary>The themed shop's header, tabs and hints. The Fortifications tab shows only while the build sells a fortification; without one the shop stays on Reinforcements.</summary>
    private void ApplyShop(RunView view)
    {
        ShopTimerText = LongDefeatHudText.NextWaveIn(view.SecondsLeft);
        ReinforcementsText = LongDefeatHudText.TabReinforcements();
        FortificationsText = LongDefeatHudText.FortTitle();
        HasFortTab = Fort.IsVisible;
        IsFortificationsTab = _fortTab && HasFortTab;
        IsReinforcementsTab = !IsFortificationsTab;
        IsOffersEmpty = view.Offers.Count == 0;
        NoOffersText = IsOffersEmpty ? LongDefeatHudText.NoOffers() : string.Empty;
        ShopHintText = IsOffersEmpty ? string.Empty : LongDefeatHudText.ShopHint();
    }

    private void ApplySpeed()
    {
        SpeedText = LongDefeatHudText.Speed(_session.Speed);
        SpeedTitle = LongDefeatHudText.SpeedTitle();
        SpeedValue = LongDefeatHudText.SpeedButton(_session.Speed);
        KeySpeedText = LongDefeatHudText.KeySpeed(_session.Speed);
        foreach (var button in Speeds)
            button.Update(_session.Speed);
    }

    private void ApplyOffers(RunView view)
    {
        if (Offers.Count != view.Offers.Count)
        {
            Offers.Clear();
            for (var i = 0; i < view.Offers.Count; i++)
                Offers.Add(new LongDefeatOfferVM(Buy));
        }

        for (var i = 0; i < view.Offers.Count; i++)
            Offers[i].Update(view.Offers[i], view.Gold);
    }

    /// <summary>The upgrade group under the packs: its title and one row per step on offer.</summary>
    private void ApplyUpgrades(RunView view)
    {
        HasUpgrades = view.Upgrades.Count > 0;
        UpgradesTitle = HasUpgrades ? LongDefeatHudText.UpgradesTitle() : string.Empty;
        if (Upgrades.Count != view.Upgrades.Count)
        {
            Upgrades.Clear();
            for (var i = 0; i < view.Upgrades.Count; i++)
                Upgrades.Add(new LongDefeatUpgradeVM(Upgrade));
        }

        for (var i = 0; i < view.Upgrades.Count; i++)
            Upgrades[i].Update(view.Upgrades[i]);
    }

    private void Upgrade(string stepId)
    {
        Notify(LongDefeatHudText.Refusal(_session.TryUpgrade(stepId)));
        Refresh();
    }

    /// <summary>The pick group shows with the open shop while a pick is open. An offer seen for the first time raises a notice, because the panel never opens by itself for it.</summary>
    private void ApplyBoons(RunView? view)
    {
        var offer = view?.BoonOffer ?? Array.Empty<BoonCard>();
        var key = string.Join(",", offer.Select(c => c.Id));
        if (key != _offerKey)
        {
            var first = _offerKey.Length == 0;
            _offerKey = key;
            if (first && key.Length > 0 && view!.ShopOpen)
                Notify(LongDefeatHudText.BoonOffered());
        }

        IsPickVisible = _shopWanted && offer.Count > 0;
        IsBoonPanelVisible = IsPickVisible && !_boonsHidden;
        if (!IsPickVisible)
        {
            if (Boons.Count > 0)
                Boons.Clear();
            return;
        }

        PickTitle = LongDefeatHudText.PickTitle();
        PickSubtitle = LongDefeatHudText.PickSubtitle();
        RerollText = LongDefeatHudText.Reroll(view!.Rerolls);
        CanReroll = view.Rerolls > 0;
        if (Boons.Count != offer.Count)
        {
            Boons.Clear();
            for (var i = 0; i < offer.Count; i++)
                Boons.Add(new LongDefeatBoonVM(PickBoon));
        }

        for (var i = 0; i < offer.Count; i++)
            Boons[i].Update(offer[i]);
    }

    private void PickBoon(string boonId)
    {
        var result = _session.TryPickBoon(boonId);
        if (result == PickResult.Ok)
        {
            NoticeText = string.Empty;
            _noticeLeft = 0f;
        }
        else
        {
            Notify(LongDefeatHudText.Refusal(result));
        }

        Refresh();
    }

    private void BuyCare()
    {
        Notify(LongDefeatHudText.Refusal(_session.TryFieldCare()));
        Refresh();
    }

    private void BuyGate()
    {
        Notify(LongDefeatHudText.Refusal(_session.TryBuyGateLevel()));
        Refresh();
    }

    private void RepairGate()
    {
        Notify(LongDefeatHudText.Refusal(_session.TryRepairGates()));
        Refresh();
    }

    private void BuyTowers()
    {
        Notify(LongDefeatHudText.RefusalTowers(_session.TryBuyTowerLevel()));
        Refresh();
    }

    private void BuyVolley()
    {
        Notify(LongDefeatHudText.RefusalVolley(_session.TryBuyVolleyLevel()));
        Refresh();
    }

    /// <summary>The volley key (D24): arms the Elven Volley in a wave, which the director looses on the next tick. A refusal is said on the notice line; an armed press says nothing.</summary>
    internal void FireVolley()
    {
        Notify(LongDefeatHudText.Refusal(_session.TryVolley()));
        Refresh();
    }

    private void Buy(string offerId)
    {
        Notify(LongDefeatHudText.Refusal(_session.TryBuy(offerId)));
        Refresh();
    }

    private void Notify(string line)
    {
        if (line.Length == 0)
            return;

        NoticeText = line;
        _noticeLeft = NoticeSeconds;
    }
}
