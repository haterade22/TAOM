using System;
using System.Linq;
using TaleWorlds.Library;
using TAOM.Features.FordsOfWar;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Client.Features.FordsOfWar;

/// <summary>
/// View model of the Fords of War command panel (FordsCommand.xml, build-spec 6). It reads the human commander's
/// <see cref="MatchView"/> through the <see cref="FordsSession"/> and gives orders through the same session, so a click, a hotkey and
/// the AI all reach the match by the same four commands. <see cref="Refresh"/> rebuilds the panel from the view; every list is
/// updated in place, never rebuilt, so a button the player is pressing stays the same object. <see cref="Tick"/> runs the notice
/// timer. The result button is the panel's only way out; the automatic return to the menu is the mission logic's. All text comes
/// from <see cref="FordsHudText"/>.
/// </summary>
public sealed class FordsCommandVM : FordsViewModel
{
    internal const int LaneHotkeys = 4;
    internal const int OfferHotkeys = 8;
    internal const int MaxFeedLines = 6;
    private const float NoticeSeconds = 4f;

    private static readonly LaneOrder[] OrderRows = { LaneOrder.Push, LaneOrder.Hold, LaneOrder.FallBack };
    private static readonly string[] OrderKeys = { "Z", "X", "C" };

    private readonly FordsSession _session;
    private readonly Action _exit;
    private MatchView? _view;
    private bool _exited;
    private float _noticeSeconds;

    private bool _isVisible;
    private bool _isResultVisible;
    private string _goldText = string.Empty;
    private string _clockText = string.Empty;
    private string _noticeText = string.Empty;
    private string _troopsTitle = string.Empty;
    private string _ordersTitle = string.Empty;
    private string _heroesTitle = string.Empty;
    private string _towersTitle = string.Empty;
    private string _pointsTitle = string.Empty;
    private string _keysHint = string.Empty;
    private string _resultTitle = string.Empty;
    private string _resultText = string.Empty;
    private string _resultButtonText = string.Empty;

    internal FordsCommandVM(FordsSession session, Action exit)
    {
        _session = session;
        _exit = exit;
        Lanes = new MBBindingList<FordsLaneVM>();
        Offers = new MBBindingList<FordsOfferVM>();
        Heroes = new MBBindingList<FordsHeroOfferVM>();
        Orders = new MBBindingList<FordsOrderVM>();
        LaneBars = new MBBindingList<FordsLaneBarVM>();
        Feed = new MBBindingList<FordsFeedLineVM>();
        Points = new MBBindingList<FordsPointVM>();
        OwnKeep = new FordsTowerBarVM();
        EnemyKeep = new FordsTowerBarVM();
        for (var i = 0; i < OrderRows.Length; i++)
            Orders.Add(new FordsOrderVM(OrderRows[i], OrderKeys[i], SetOrder));
    }

    /// <summary>The lane the offers and orders act on.</summary>
    internal int SelectedLane { get; private set; }

    [DataSourceProperty]
    public bool IsVisible
    {
        get => _isVisible;
        private set => Set(ref _isVisible, value, nameof(IsVisible));
    }

    [DataSourceProperty]
    public bool IsResultVisible
    {
        get => _isResultVisible;
        private set => Set(ref _isResultVisible, value, nameof(IsResultVisible));
    }

    [DataSourceProperty]
    public string GoldText
    {
        get => _goldText;
        private set => Set(ref _goldText, value, nameof(GoldText));
    }

    [DataSourceProperty]
    public string ClockText
    {
        get => _clockText;
        private set => Set(ref _clockText, value, nameof(ClockText));
    }

    [DataSourceProperty]
    public string NoticeText
    {
        get => _noticeText;
        private set => Set(ref _noticeText, value, nameof(NoticeText));
    }

    [DataSourceProperty]
    public string TroopsTitle
    {
        get => _troopsTitle;
        private set => Set(ref _troopsTitle, value, nameof(TroopsTitle));
    }

    [DataSourceProperty]
    public string OrdersTitle
    {
        get => _ordersTitle;
        private set => Set(ref _ordersTitle, value, nameof(OrdersTitle));
    }

    [DataSourceProperty]
    public string HeroesTitle
    {
        get => _heroesTitle;
        private set => Set(ref _heroesTitle, value, nameof(HeroesTitle));
    }

    [DataSourceProperty]
    public string TowersTitle
    {
        get => _towersTitle;
        private set => Set(ref _towersTitle, value, nameof(TowersTitle));
    }

    [DataSourceProperty]
    public string PointsTitle
    {
        get => _pointsTitle;
        private set => Set(ref _pointsTitle, value, nameof(PointsTitle));
    }

    [DataSourceProperty]
    public string KeysHint
    {
        get => _keysHint;
        private set => Set(ref _keysHint, value, nameof(KeysHint));
    }

    [DataSourceProperty]
    public string ResultTitle
    {
        get => _resultTitle;
        private set => Set(ref _resultTitle, value, nameof(ResultTitle));
    }

    [DataSourceProperty]
    public string ResultText
    {
        get => _resultText;
        private set => Set(ref _resultText, value, nameof(ResultText));
    }

    [DataSourceProperty]
    public string ResultButtonText
    {
        get => _resultButtonText;
        private set => Set(ref _resultButtonText, value, nameof(ResultButtonText));
    }

    [DataSourceProperty]
    public MBBindingList<FordsLaneVM> Lanes { get; }

    [DataSourceProperty]
    public MBBindingList<FordsOfferVM> Offers { get; }

    [DataSourceProperty]
    public MBBindingList<FordsHeroOfferVM> Heroes { get; }

    [DataSourceProperty]
    public MBBindingList<FordsOrderVM> Orders { get; }

    [DataSourceProperty]
    public MBBindingList<FordsLaneBarVM> LaneBars { get; }

    [DataSourceProperty]
    public MBBindingList<FordsFeedLineVM> Feed { get; }

    [DataSourceProperty]
    public MBBindingList<FordsPointVM> Points { get; }

    [DataSourceProperty]
    public FordsTowerBarVM OwnKeep { get; }

    [DataSourceProperty]
    public FordsTowerBarVM EnemyKeep { get; }

    /// <summary>Keeps <paramref name="list"/> at exactly <paramref name="count"/> items, adding or dropping at the end only.</summary>
    internal static void Sync<T>(MBBindingList<T> list, int count, Func<int, T> create)
    {
        while (list.Count > count)
            list.RemoveAt(list.Count - 1);
        while (list.Count < count)
            list.Add(create(list.Count));
    }

    /// <summary>Rebuilds the panel from the session's view; hides it while no match runs.</summary>
    internal void Refresh()
    {
        var view = _session.GetView();
        _view = view;
        if (view == null)
        {
            IsVisible = false;
            IsResultVisible = false;
            return;
        }

        IsVisible = true;
        KeepSelectedLaneOwned(view);
        GoldText = FordsHudText.Gold(view.Gold);
        ClockText = view.Phase == MatchPhase.Countdown ? FordsHudText.Forming() : Clock(view.Elapsed);
        TroopsTitle = FordsHudText.TroopsTitle(SelectedLane + 1);
        OrdersTitle = FordsHudText.OrdersTitle(SelectedLane + 1);
        HeroesTitle = FordsHudText.HeroesTitle();
        TowersTitle = FordsHudText.TowersTitle();
        KeysHint = FordsHudText.KeysHint();
        RefreshLanes(view);
        RefreshOffers(view);
        RefreshHeroes(view);
        RefreshOrders(view);
        RefreshTowers(view);
        RefreshPoints(view);
        RefreshFeed(view);
        RefreshResult(view);
    }

    /// <summary>
    /// Advances the notice timer. The views keep ticking while the escape menu pauses the engine, and a refusal should still be
    /// on screen when the menu closes, so a paused frame counts for nothing.
    /// </summary>
    internal void Tick(float dt, bool enginePaused = false)
    {
        if (enginePaused || _noticeSeconds <= 0f)
            return;

        _noticeSeconds -= dt;
        if (_noticeSeconds <= 0f)
            NoticeText = string.Empty;
    }

    internal void SelectLane(int lane)
    {
        if (_view == null || !_view.Lanes.Any(l => l.Index == lane && l.Owned))
            return;

        SelectedLane = lane;
        Refresh();
    }

    internal void BuyOffer(int index)
    {
        if (_view == null || index < 0 || index >= _view.UnitOffers.Count)
            return;

        Report(_session.TryBuyPack(SelectedLane, _view.UnitOffers[index].OfferId));
    }

    internal void SetOrder(LaneOrder order) => Report(_session.TrySetLaneOrder(SelectedLane, order));

    /// <summary>The H key: fields the first hero that is waiting and that the gold reaches, in the selected lane.</summary>
    internal void FieldFirstAffordableHero()
    {
        var offer = _view?.HeroOffers.FirstOrDefault(o => !o.Fielded && o.CanAfford);
        if (offer == null)
        {
            Notice(FordsHudText.NoHeroAffordable());
            return;
        }

        Report(_session.TryBuyHero(SelectedLane, offer.HeroId));
    }

    public void ExecuteExit()
    {
        if (_exited)
            return;

        _exited = true;
        _exit();
    }

    private void ActOnHero(string heroId)
    {
        var fallen = _view?.Heroes.Any(h => h.HeroId == heroId && h.Side == _view.Side && h.State == HeroState.Dead) == true;
        Report(fallen ? _session.TryBuyback(heroId) : _session.TryBuyHero(SelectedLane, heroId));
    }

    private void Report(CommandResult result)
    {
        Notice(FordsHudText.Refusal(result));
        Refresh();
    }

    private void Notice(string text)
    {
        NoticeText = text;
        _noticeSeconds = text.Length == 0 ? 0f : NoticeSeconds;
    }

    private void KeepSelectedLaneOwned(MatchView view)
    {
        if (view.Lanes.Any(l => l.Index == SelectedLane && l.Owned))
            return;

        SelectedLane = view.OwnedLanes.Count > 0 ? view.OwnedLanes.Min() : 0;
    }

    private void RefreshLanes(MatchView view)
    {
        var owned = view.Lanes.Where(l => l.Owned).ToList();
        Sync(Lanes, owned.Count, _ => new FordsLaneVM(SelectLane));
        for (var i = 0; i < owned.Count; i++)
            Lanes[i].Update(owned[i], owned[i].Index == SelectedLane);
    }

    private void RefreshOffers(MatchView view)
    {
        Sync(Offers, view.UnitOffers.Count, i => new FordsOfferVM(i, BuyOffer));
        for (var i = 0; i < view.UnitOffers.Count; i++)
            Offers[i].Update(view.UnitOffers[i], NameOf(view.UnitOffers[i].TroopId), IsLive(view));
    }

    /// <summary>The match refuses every purchase and lane order with NotLive outside the Live phase, so no button is lit then.</summary>
    private static bool IsLive(MatchView view) => view.Phase == MatchPhase.Live;

    private void RefreshHeroes(MatchView view)
    {
        var live = IsLive(view);
        Sync(Heroes, view.HeroOffers.Count, i => new FordsHeroOfferVM(ActOnHero));
        for (var i = 0; i < view.HeroOffers.Count; i++)
        {
            var offer = view.HeroOffers[i];
            var hero = view.Heroes.FirstOrDefault(h => h.HeroId == offer.HeroId && h.Side == view.Side);
            var name = NameOf(offer.HeroId);
            if (hero is { State: HeroState.Dead })
                Heroes[i].Update(offer.HeroId, name, FordsHudText.HeroFallen((int)Math.Ceiling(hero.RespawnLeft)),
                    FordsHudText.HeroBuyback(hero.BuybackPrice), live && hero.CanBuyback && view.Gold >= hero.BuybackPrice);
            else if (offer.Fielded)
                Heroes[i].Update(offer.HeroId, name, FordsHudText.HeroFielded(), FordsHudText.HeroField(offer.Price), false);
            else
                Heroes[i].Update(offer.HeroId, name, FordsHudText.HeroReady(), FordsHudText.HeroField(offer.Price), live && offer.CanAfford && view.HeroSlotsFree > 0);
        }
    }

    private void RefreshOrders(MatchView view)
    {
        var lane = view.Lanes.FirstOrDefault(l => l.Index == SelectedLane);
        foreach (var order in Orders)
            order.Update(lane?.Order ?? LaneOrder.Push, lane?.CooldownLeft ?? 0f, IsLive(view));
    }

    private void RefreshTowers(MatchView view)
    {
        Sync(LaneBars, view.Lanes.Count, _ => new FordsLaneBarVM());
        for (var i = 0; i < view.Lanes.Count; i++)
            LaneBars[i].Update(view.Lanes[i], view.Side);

        var own = view.Keeps.FirstOrDefault(k => k.Owner == view.Side);
        var enemy = view.Keeps.FirstOrDefault(k => k.Owner != view.Side);
        if (own != null)
            OwnKeep.Update(own, view.Side, FordsHudText.OwnKeep());
        if (enemy != null)
            EnemyKeep.Update(enemy, view.Side, FordsHudText.EnemyKeep());
    }

    private void RefreshPoints(MatchView view)
    {
        PointsTitle = view.CapturePoints.Count == 0 ? string.Empty : FordsHudText.PointsTitle();
        Sync(Points, view.CapturePoints.Count, _ => new FordsPointVM());
        for (var i = 0; i < view.CapturePoints.Count; i++)
            Points[i].Update(i + 1, view.CapturePoints[i], view.Side);
    }

    private void RefreshFeed(MatchView view)
    {
        foreach (var e in _session.DrainEvents())
        {
            var needsName = e.Kind == MatchEventKind.HeroFielded || e.Kind == MatchEventKind.HeroDied || e.Kind == MatchEventKind.HeroRespawned;
            var line = FordsHudText.Feed(e, view.Side, needsName ? NameOf(e.Subject) : e.Subject);
            if (line == null)
                continue;

            Feed.Add(new FordsFeedLineVM(line));
            while (Feed.Count > MaxFeedLines)
                Feed.RemoveAt(0);
        }
    }

    private void RefreshResult(MatchView view)
    {
        var ended = view.Phase == MatchPhase.Ended || view.Winner != null;
        IsResultVisible = ended;
        if (!ended)
            return;

        var won = view.Winner == view.Side;
        ResultTitle = FordsHudText.ResultTitle(won);
        ResultText = FordsHudText.ResultText(won);
        ResultButtonText = FordsHudText.ResultButton();
    }

    private string NameOf(string characterId)
    {
        var name = _session.DisplayName(characterId);
        return string.IsNullOrEmpty(name) ? characterId : name;
    }

    private static string Clock(float elapsed)
    {
        var total = (int)Math.Max(0f, elapsed);
        return $"{total / 60:D2}:{total % 60:D2}";
    }
}
