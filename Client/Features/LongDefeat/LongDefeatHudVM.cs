using System;
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
    private string _noticeText = string.Empty;
    private bool _isShopVisible;
    private string _shopTitle = string.Empty;
    private string _hornText = string.Empty;
    private string _closeText = string.Empty;
    private bool _isEndVisible;
    private string _endTitle = string.Empty;
    private string _endLines = string.Empty;
    private string _bestText = string.Empty;
    private string _reportText = string.Empty;
    private string _leaveText = string.Empty;

    internal LongDefeatHudVM(LongDefeatSession session, Action leave)
    {
        _session = session;
        _leave = leave;
        Offers = new MBBindingList<LongDefeatOfferVM>();
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
        }

        IsEndVisible = result != null && !_endDismissed;
        if (result != null)
        {
            EndTitle = LongDefeatHudText.EndTitle(result.Reason);
            EndLines = LongDefeatHudText.EndLines(result.WavesCleared, result.Kills, _session.LostInClearedWaves);
            ReportText = _session.ReportPath is { } path ? LongDefeatHudText.ReportFile(System.IO.Path.GetFileName(path)) : string.Empty;
            BestText = _session.Best is { } best ? LongDefeatHudText.Best(best.Waves, best.Kills) : LongDefeatHudText.NoBest();
            LeaveText = LongDefeatHudText.Leave();
        }
    }

    private void ApplyStrip(RunView view)
    {
        WaveText = LongDefeatHudText.Wave(view.Phase, view.Wave);
        TimerText = view.ShopOpen ? LongDefeatHudText.Clock(view.SecondsLeft) : string.Empty;
        GoldText = LongDefeatHudText.Gold(view.Gold);
        AttackersText = LongDefeatHudText.Attackers(view.AttackersStanding);
        DefendersText = LongDefeatHudText.Defenders(view.DefendersStanding + view.DefendersPending);
        HintText = LongDefeatHudText.Hint(view.Phase);
        IsKeepVisible = view.Phase == RunPhase.Wave && view.KeepProgress > 0f;
        KeepFillWidth = IsKeepVisible ? KeepWidth(view.KeepProgress) : 0f;
        KeepText = IsKeepVisible ? LongDefeatHudText.Keep() : string.Empty;
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
