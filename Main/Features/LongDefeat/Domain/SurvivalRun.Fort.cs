using System;
using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.LongDefeat.Domain;

/// <summary>
/// The fortification side of the run (phase3-plan.md, Slice 2): the five buys, the levels owned, the purchase list the report reads, and the effects the director is told on the next tick. Pure: the engine
/// work (gate hit points, posts, healing) is the adapters', and what this build can deliver at all is <see cref="RunPlan.Offered"/> (D31), empty in the shipped game until each slice lands its effect.
/// </summary>
internal sealed partial class SurvivalRun
{
    private readonly List<FortPurchase> _purchases = new();
    private IReadOnlyList<LdGateFacts> _gates = Array.Empty<LdGateFacts>();
    private bool _gatesRead;
    private int _gateLevel;
    private int _towerLevel;
    private int _volleyLevel;
    private int _careUsed;
    private int _pendingPosts;
    private bool _pendingFill;
    private float? _pendingGate;
    private bool _pendingRepair;
    private bool _repairHandedOut;
    private float _pendingCare;
    private int _gateWaveLevel;
    private float _gateStartHitPoints;
    private float _gateStartMax;
    private int _gatesDownAtStart;
    private float? _gateFellAt;

    public int GateLevel => _gateLevel;

    public int TowerLevel => _towerLevel;

    public int VolleyLevel => _volleyLevel;

    /// <summary>Every purchase so far and the levels owned, for the report.</summary>
    public RunFort FortSummary() => new(_purchases.ToList(), _gateLevel, _towerLevel, _volleyLevel);

    /// <summary>Buys the next gate level: every visible gate's maximum is raised to the level's share over its vanilla maximum on the next tick (D18).</summary>
    public FortResult TryBuyGateLevel()
    {
        var closed = Closed();
        if (closed != null)
            return closed.Value;

        var quote = QuoteGate();
        if (quote.Result != FortResult.Ok)
            return quote.Result;

        var level = _gateLevel + 1;
        var result = Complete(quote, PurseSink.Gate, PurchaseKind.GateLevel, level, 0f);
        if (result == FortResult.Ok)
        {
            _gateLevel = level;
            _pendingGate = GateFactor();
        }

        return result;
    }

    /// <summary>Mends every visible gate, a broken one paying all of the repair price and a damaged one its missing share of it (D17, D18). The adapter does it on the next tick.</summary>
    public FortResult TryRepairGates()
    {
        var closed = Closed();
        if (closed != null)
            return closed.Value;

        var quote = QuoteRepair();
        if (quote.Result != FortResult.Ok)
            return quote.Result;

        var result = Complete(quote, PurseSink.Gate, PurchaseKind.GateRepair, 0, quote.Share, quote.Shares);
        if (result == FortResult.Ok)
            _pendingRepair = true;

        return result;
    }

    /// <summary>Buys the next tower level. The posts it adds must fit under the garrison cap (D20), so nobody pays for a post that cannot spawn; they are manned on the next tick.</summary>
    public FortResult TryBuyTowerLevel()
    {
        var closed = Closed();
        if (closed != null)
            return closed.Value;

        var quote = QuoteTowers();
        if (quote.Result != FortResult.Ok)
            return quote.Result;

        var level = _towerLevel + 1;
        var result = Complete(quote, PurseSink.Towers, PurchaseKind.TowerLevel, level, 0f);
        if (result == FortResult.Ok)
        {
            _towerLevel = level;
            _pendingPosts += quote.Posts;
            _pendingFill = true;
        }

        return result;
    }

    /// <summary>Field care: every standing defender regains its share of the health he is missing, on the next tick. At most <see cref="CarePlan.PerIntermission"/> a stretch between waves (D21), and not in the preparation, where the garrison has not yet fought and nobody is hurt: that answers <see cref="FortResult.NotOffered"/>, so the panel, the bot and the buy agree.</summary>
    public FortResult TryFieldCare()
    {
        var closed = Closed();
        if (closed != null)
            return closed.Value;

        var quote = QuoteCare();
        if (quote.Result != FortResult.Ok)
            return quote.Result;

        var result = Complete(quote, PurseSink.FieldCare, PurchaseKind.FieldCare, 0, quote.Share);
        if (result == FortResult.Ok)
        {
            _careUsed++;
            _pendingCare = 1f - (1f - _pendingCare) * (1f - quote.Share);
        }

        return result;
    }

    /// <summary>Buys the next Elven Volley level. What the level does in a wave is the volley's (Slice 3 and 6).</summary>
    public FortResult TryBuyVolleyLevel()
    {
        var closed = Closed();
        if (closed != null)
            return closed.Value;

        var quote = QuoteVolley();
        if (quote.Result != FortResult.Ok)
            return quote.Result;

        var level = _volleyLevel + 1;
        var result = Complete(quote, PurseSink.Volley, PurchaseKind.VolleyLevel, level, 0f);
        if (result == FortResult.Ok)
            _volleyLevel = level;

        return result;
    }

    private readonly record struct Quote(FortResult Result, int BasePrice = 0, int Price = 0, float Share = 0f, int Posts = 0, IReadOnlyList<float>? Shares = null);

    /// <summary>
    /// The field has been read and showed no visible gate it can write to (a gate without a destructible is skipped at the boot), so a gate level or a repair would spend gold on nothing and its check would fail
    /// with <c>gates=0</c>. Before the first tick nothing has been read and the rows are sold as the shop lists them.
    /// </summary>
    private bool NoGateInField => _gatesRead && _gates.Count == 0;

    private FortResult? Closed() => Result != null ? FortResult.RunOver : !BetweenWaves ? FortResult.NotOpen : null;

    /// <summary>A price through the boons' price factor, at least 1: the same rule a pack follows (D29).</summary>
    private int FortPrice(int basePrice) => Math.Max(1, GoldFactors.Apply(basePrice, _factors.Price));

    private static Quote Refuse(FortResult why) => new(why);

    private Quote QuoteGate()
    {
        var gate = _plan.Shop.Fort?.Gate;
        if (_plan.Offered.Gate <= 0)
            return Refuse(FortResult.NotOffered);
        if (gate == null || NoGateInField)
            return Refuse(FortResult.Hidden);
        if (_gateLevel >= gate.Levels.Count)
            return Refuse(FortResult.MaxLevel);
        if (_gateLevel >= _plan.Offered.Gate)
            return Refuse(FortResult.NotOffered);

        var price = gate.Levels[_gateLevel].Price;
        return new Quote(FortResult.Ok, price, FortPrice(price));
    }

    private Quote QuoteRepair()
    {
        var gate = _plan.Shop.Fort?.Gate;
        if (_plan.Offered.Gate <= 0)
            return Refuse(FortResult.NotOffered);
        if (gate == null || NoGateInField)
            return Refuse(FortResult.Hidden);

        // The facts below are last tick's: while a repair waits to be written, or was written after this tick's read, a gate still reads as broken and would be sold again.
        if (_pendingRepair || _repairHandedOut)
            return Refuse(FortResult.NothingToRepair);

        var missing = _gates.Select(g => g.MissingShare).Where(share => share >= GateMath.MinMissingShare).ToList();
        if (missing.Count == 0)
            return Refuse(FortResult.NothingToRepair);

        // One row acts on every visible gate and charges per gate (D18); the purchase keeps each gate's share, rounded as the report writes it, so the judge can replay the per-gate prices.
        var whole = FortPrice(gate.RepairPrice);
        var shares = missing.Select(GateMath.ReportedShare).ToList();
        return new Quote(FortResult.Ok, gate.RepairPrice, missing.Sum(share => GateMath.RepairPrice(whole, share)), shares.Sum(), 0, shares);
    }

    private Quote QuoteTowers()
    {
        var towers = _plan.Shop.Fort?.Towers;
        if (_plan.Offered.Towers <= 0)
            return Refuse(FortResult.NotOffered);
        if (towers == null || !Towers.Any(t => t.Posts.Count > 0))
            return Refuse(FortResult.Hidden);
        if (_towerLevel >= towers.Levels.Count)
            return Refuse(FortResult.MaxLevel);
        if (_towerLevel >= _plan.Offered.Towers)
            return Refuse(FortResult.NotOffered);

        var price = towers.Levels[_towerLevel].Price;
        return new Quote(FortResult.Ok, price, FortPrice(price), 0f, PostsAdded(towers, _towerLevel, _towerLevel + 1));
    }

    private Quote QuoteCare()
    {
        var care = _plan.Shop.Fort?.Care;
        if (!_plan.Offered.FieldCare)
            return Refuse(FortResult.NotOffered);
        if (care == null)
            return Refuse(FortResult.Hidden);
        if (_phase == RunPhase.Preparation)
            return Refuse(FortResult.NotOffered);
        if (_careUsed >= care.PerIntermission)
            return Refuse(FortResult.Used);

        return new Quote(FortResult.Ok, care.Price, FortPrice(care.Price), care.Share);
    }

    private Quote QuoteVolley()
    {
        var volley = _plan.Shop.Fort?.Volley;
        if (_plan.Offered.Volley <= 0)
            return Refuse(FortResult.NotOffered);
        if (volley == null || VolleyOrigins.Count == 0)
            return Refuse(FortResult.Hidden);
        if (_volleyLevel >= volley.Levels.Count)
            return Refuse(FortResult.MaxLevel);
        if (_volleyLevel >= _plan.Offered.Volley)
            return Refuse(FortResult.NotOffered);

        var price = volley.Levels[_volleyLevel].Price;
        return new Quote(FortResult.Ok, price, FortPrice(price));
    }

    /// <summary>Spends the quote's price and lists the purchase. The gold is tested before the cap, as a pack's is.</summary>
    private FortResult Complete(Quote quote, PurseSink sink, PurchaseKind kind, int level, float share, IReadOnlyList<float>? shares = null)
    {
        if (quote.Price > _purse.Balance)
            return FortResult.NoGold;
        if (quote.Posts > 0 && !PostsFit(quote.Posts))
            return FortResult.GarrisonFull;
        if (!_purse.TrySpend(quote.Price, sink))
            return FortResult.NoGold;

        _purchases.Add(new FortPurchase(_wave + 1, kind, string.Empty, level, quote.BasePrice, quote.Price, GateMath.ReportedShare(share), shares));
        return FortResult.Ok;
    }

    private bool PostsFit(int posts) => _lastDefenders + PendingDefenders + posts <= _r.GarrisonCap;

    /// <summary>The soldiers going from <paramref name="from"/> to <paramref name="to"/> adds across the recorded towers: a tower fills the posts it has, so a level above its recorded posts adds none.</summary>
    private int PostsAdded(TowerPlan towers, int from, int to)
    {
        int Target(int level) => level <= 0 ? 0 : towers.Levels[level - 1].Posts;
        return Towers.Sum(t => Math.Min(Target(to), t.Posts.Count) - Math.Min(Target(from), t.Posts.Count));
    }

    /// <summary>The factor over the vanilla maximum the gate level in force gives: 1 with no level.</summary>
    private float GateFactor() => _gateLevel <= 0 ? 1f : 1f + _plan.Shop.Fort!.Gate!.Levels[_gateLevel - 1].HitPointsPercent / 100f;

    /// <summary>
    /// Hands the director what was bought since the last tick, before any batch of this tick (the posts come before every pack). The repair's factor is read here, so a gate level bought after the
    /// repair and before this tick is the maximum the repair restores. Returns whether anything was handed out: the tick then does not start a wave, because the gates it was given were read before
    /// these effects reached them (the wave's gate record and the care's "never in a wave" both depend on it).
    /// </summary>
    private bool TakePendingFort(RunEffects fx)
    {
        fx.GateLevel = _pendingGate;
        fx.RepairGates = _pendingRepair ? GateFactor() : null;
        _repairHandedOut |= _pendingRepair;
        fx.FieldCare = _pendingCare;
        var handed = _pendingGate != null || _pendingRepair || _pendingCare > 0f || _pendingFill;
        _pendingGate = null;
        _pendingRepair = false;
        _pendingCare = 0f;
        if (!_pendingFill)
            return handed;

        fx.FillPosts = FillNow();
        _pendingFill = false;
        _pendingPosts = 0;
        return handed;
    }

    /// <summary>A wave was cleared: the posts are manned again, free, up to the room left under the cap. Packs are bought later in the intermission, so the refill always comes first.</summary>
    private void RefillPosts(RunEffects fx)
    {
        if (_towerLevel > 0 && Towers.Any(t => t.Posts.Count > 0))
            fx.FillPosts = FillNow();
    }

    private LdPostFill FillNow()
    {
        var troop = _plan.Shop.Fort!.Towers!;
        var room = _r.GarrisonCap - _lastDefenders - (PendingDefenders - _pendingPosts);
        return new LdPostFill(troop.Troop, troop.Levels[_towerLevel - 1].Posts, Math.Max(0, room), DefenderHealth(new WaveSoldier(troop.Troop, FormationSlot.Ranged)));
    }

    private void ObserveGates(IReadOnlyList<LdGateFacts>? gates)
    {
        _gates = gates ?? Array.Empty<LdGateFacts>();
        _gatesRead = true;
        _repairHandedOut = false;
    }

    /// <summary>The wave begins: the level in force, and the hit points and gates down as it starts.</summary>
    private void InitGateRecord()
    {
        _gateWaveLevel = _gateLevel;
        _gateStartHitPoints = _gates.Sum(g => g.Destroyed ? 0f : Math.Max(0f, g.HitPoints));
        _gateStartMax = _gates.Sum(g => Math.Max(0f, g.MaxHitPoints));
        _gatesDownAtStart = _gates.Count(g => g.Destroyed);
        _gateFellAt = null;
    }

    /// <summary>A gate standing at the wave's start and destroyed since: the first moment it was seen so is when it fell.</summary>
    private void ObserveGateFall()
    {
        if (_gateFellAt == null && _gates.Count(g => g.Destroyed) > _gatesDownAtStart)
            _gateFellAt = _waveSeconds;
    }

    private FortWave FortWaveNow() => new(new GateWaveRecord(_gateWaveLevel, _gateStartHitPoints, _gateStartMax, _gateFellAt != null, _gateFellAt), PostsWaveNow(), ElvenWaveNow(), TowerWaveNow());

    private FortView FortViewNow()
    {
        var fort = _plan.Shop.Fort ?? FortPlan.None;
        var tower = QuoteTowers();
        return new FortView(
            Row(QuoteGate(), _gateLevel, fort.Gate?.Levels.Count ?? 0),
            Row(QuoteRepair(), 0, 0),
            Row(tower, _towerLevel, Math.Min(fort.Towers?.Levels.Count ?? 0, _plan.Offered.Towers), tower.Posts == 0 || PostsFit(tower.Posts)),
            Row(QuoteCare(), _careUsed, fort.Care?.PerIntermission ?? 0),
            Row(QuoteVolley(), _volleyLevel, fort.Volley?.Levels.Count ?? 0),
            _gates,
            _plan.Options.StartGold != null)
        {
            CareShare = fort.Care?.Share ?? 0f,
            TowerPostsShown = fort.Towers is { } towerRow ? towerRow.Levels[Math.Min(_towerLevel, Math.Min(towerRow.Levels.Count, Math.Max(1, _plan.Offered.Towers)) - 1)].Posts : 0,
            GatePercent = fort.Gate is { } gate ? gate.Levels[Math.Min(_gateLevel, gate.Levels.Count - 1)].HitPointsPercent : 0f,
            VolleyShown = fort.Volley is { } volley ? volley.Levels[Math.Min(_volleyLevel, volley.Levels.Count - 1)] : null,
        };
    }

    private static FortRow Row(Quote quote, int level, int maxLevel, bool fits = true) => new(level, maxLevel, quote.Price, quote.Result == FortResult.Ok && fits) { Full = quote.Result == FortResult.Ok && !fits };
}
