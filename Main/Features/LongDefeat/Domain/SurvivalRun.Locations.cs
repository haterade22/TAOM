using System;
using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.LongDefeat.Domain;

/// <summary>
/// The two places of Phase 4 (phase4-plan.md, Slice 2): the armoury's gear sets and the engineer's yard's engines, bought between waves by a lord who stands at the recorded place, each handed to the
/// director on the next tick, and the errand the unattended bot walks to reach a place (D44). Pure: the swap, the deploy and the walk are the adapters', and what this build can deliver at all is
/// <see cref="RunPlan.Locations"/> (D46), empty in the shipped game until each slice lands its effect.
/// </summary>
internal sealed partial class SurvivalRun
{
    /// <summary>The mission seconds an errand may walk before the lord is put at the place (D44).</summary>
    internal const float ErrandBudgetSeconds = 20f;

    /// <summary>The seconds after the teleport by which the lord must stand at the place, or the errand is given up.</summary>
    private const float ErrandSettleSeconds = 5f;

    /// <summary>How far above or below a recorded place the lord may stand and still be at it, in metres (D37): the wall walk over a yard is not the yard.</summary>
    private const float PlaceHeightBand = 3f;

    private const string Mangonel = "mangonel";
    private const string Trebuchet = "trebuchet";

    private sealed record EngineHeld(string Kind, int? LostInWave);

    private sealed class OpenErrand
    {
        public LdPlaceKind Place;
        public float Seconds;
        public bool Started;
        public bool Teleported;
    }

    private readonly List<GearSet> _ownedSets = new();
    private readonly Dictionary<int, EngineHeld> _engines = new();
    private readonly List<EngineOrder> _pendingEngines = new();
    private readonly List<LdPlaceKind> _unreached = new();
    private readonly HashSet<LdPlaceKind> _dropped = new();
    private IReadOnlyList<LdSite> _sites = Array.Empty<LdSite>();
    private LdLordFacts _lord = LdLordFacts.None;
    private bool _pendingSwap;
    private OpenErrand? _errand;
    private bool _errandEnd;

    /// <summary>
    /// The lord's class kit with the sets he owns laid over it (D35), null when he plays no class. The run hands it out once, as <see cref="RunEffects.SwapGear"/>, and the director builds it at every
    /// spawn of the lord too (the start, the rise and the sally's remount), so a bought set outlives his body.
    /// </summary>
    public LdLoadout? EffectiveLoadout => LoadoutMerge.Merge(_plan.Loadout, _ownedSets);

    /// <summary>The yard's sites as the field read them at boot. Until this is called the yard has no site, and no engine is sold.</summary>
    public void SetSites(IReadOnlyList<LdSite> sites) => _sites = sites ?? Array.Empty<LdSite>();

    /// <summary>Buys a gear set at the armoury. The next tick hands the lord's merged kit to the director, which puts it on his live body.</summary>
    public LocationResult TryBuyGear(string setId)
    {
        if (ClosedForPlaces() is { } closed)
            return closed;
        if (!_plan.Locations.Armoury)
            return LocationResult.NotOffered;

        var set = SetsOnSale().FirstOrDefault(s => string.Equals(s.Id, setId, StringComparison.Ordinal));
        if (set == null)
            return LocationResult.Hidden;
        if (!Within(LdPlaceKind.Armoury))
            return LocationResult.NotNear;
        if (!_lord.Own)
            return LocationResult.NotOwnBody;
        if (_ownedSets.Contains(set))
            return LocationResult.Owned;

        var price = FortPrice(set.Price);
        if (!_purse.TrySpend(price, PurseSink.Gear))
            return LocationResult.NoGold;

        _purchases.Add(new FortPurchase(_wave + 1, PurchaseKind.GearSet, set.Id, 0, set.Price, price, 0f) { Reach = ReachOf(LdPlaceKind.Armoury) });
        _ownedSets.Add(set);
        _pendingSwap = true;
        _pendingEvents.Add(new RunEvent(RunEventKind.Bought, _wave, "gear=" + set.Id));
        CloseErrandAt(LdPlaceKind.Armoury);
        return LocationResult.Ok;
    }

    /// <summary>Buys an engine of a kind at one of the yard's sites. The next tick hands the deploy to the director, which will carry it out from Slice 7 on; a site holds one engine, and a destroyed one spends its site for the run (OQ11).</summary>
    public LocationResult TryBuyEngine(string kind, int site)
    {
        if (ClosedForPlaces() is { } closed)
            return closed;
        if (!Delivers(kind))
            return LocationResult.NotOffered;

        var offer = YardSold ? _plan.Yard!.Engines.FirstOrDefault(e => string.Equals(e.Kind, kind, StringComparison.Ordinal)) : null;
        if (offer == null)
            return LocationResult.Hidden;

        var at = _sites.FirstOrDefault(s => s.Index == site);
        if (at == null || !at.Kinds.Contains(kind, StringComparer.Ordinal))
            return LocationResult.NoSite;
        if (!Within(LdPlaceKind.Yard))
            return LocationResult.NotNear;
        if (_engines.TryGetValue(site, out var held))
            return held.LostInWave != null ? LocationResult.SiteLost : LocationResult.SiteTaken;

        var price = FortPrice(offer.Price);
        if (!_purse.TrySpend(price, PurseSink.Engines))
            return LocationResult.NoGold;

        _purchases.Add(new FortPurchase(_wave + 1, PurchaseKind.Engine, offer.Kind, 0, offer.Price, price, 0f) { Site = site, Reach = ReachOf(LdPlaceKind.Yard) });
        _engines[site] = new EngineHeld(offer.Kind, null);
        _pendingEngines.Add(new EngineOrder(site, offer.Kind));
        _pendingEvents.Add(new RunEvent(RunEventKind.Bought, _wave, "engine=" + offer.Kind + " site=" + site));
        CloseErrandAt(LdPlaceKind.Yard);
        return LocationResult.Ok;
    }

    /// <summary>A bought engine was destroyed: it is lost, and its site with it. A site with no standing engine changes nothing.</summary>
    public void EngineLost(int site)
    {
        if (_engines.TryGetValue(site, out var held) && held.LostInWave == null)
            _engines[site] = held with { LostInWave = Math.Max(1, _wave) };
    }

    /// <summary>The bot sets out for a place: the director walks the lord there from the next tick. One errand runs at a time, and a place an errand failed to reach is not tried again before the next wave.</summary>
    public ErrandResult TryStartErrand(LdPlaceKind place)
    {
        if (Result != null)
            return ErrandResult.RunOver;
        if (!BetweenWaves)
            return ErrandResult.NotOpen;
        if (!Shown(place))
            return ErrandResult.NoPlace;
        if (_errand != null)
            return ErrandResult.Busy;
        if (_unreached.Contains(place))
            return ErrandResult.Unreached;

        _errand = new OpenErrand { Place = place };
        return ErrandResult.Ok;
    }

    /// <summary>
    /// A place whose recorded point the boot found unusable (no navmesh, or outside the boundary) is sold no more for the run (D46): the bot would teleport onto it and a person could never stand there.
    /// </summary>
    public void DropPlace(LdPlaceKind place) => _dropped.Add(place);

    /// <summary>The bot gives up an open errand (nothing it wanted there is for sale any more): the lord's scripted walk is released on the next tick.</summary>
    public void EndErrand()
    {
        if (_errand != null)
            CloseErrand();
    }

    private LocationResult? ClosedForPlaces() => Result != null ? LocationResult.RunOver : !BetweenWaves ? LocationResult.NotOpen : null;

    private bool Delivers(string kind) => kind switch
    {
        Mangonel => _plan.Locations.Mangonel,
        Trebuchet => _plan.Locations.Trebuchet,
        _ => false,
    };

    /// <summary>The armoury can sell: the build delivers it, the shop carries it, the fortress records its place and the lord plays a class (a lord in his own gear buys nothing, D35).</summary>
    private bool ArmourySold => _plan.Locations.Armoury && _plan.Armoury != null && _plan.ArmouryPoint != null && _plan.Loadout != null && !_dropped.Contains(LdPlaceKind.Armoury);

    private bool YardSold => _plan.Yard != null && _plan.YardPoint != null;

    /// <summary>The sets this run sells: those the lord's class is offered, less any the launch check hid (D34).</summary>
    private IEnumerable<GearSet> SetsOnSale() => ArmourySold
        ? _plan.Armoury!.Sets.Where(s => s.OfferedTo(_plan.Loadout!.ClassId) && !_plan.HiddenGearSets.Contains(s.Id, StringComparer.Ordinal))
        : Enumerable.Empty<GearSet>();

    /// <summary>One row for each site that takes a kind the yard lists and the build delivers; a site with none is not a row (D39).</summary>
    private List<SiteRow> SiteRowsNow()
    {
        var rows = new List<SiteRow>();
        if (!YardSold)
            return rows;

        foreach (var site in _sites)
        {
            var kinds = _plan.Yard!.Engines.Where(e => Delivers(e.Kind) && site.Kinds.Contains(e.Kind, StringComparer.Ordinal)).Select(e => e with { Price = FortPrice(e.Price) }).ToList();
            if (kinds.Count == 0)
                continue;

            if (_engines.TryGetValue(site.Index, out var held))
                rows.Add(new SiteRow(site, held.LostInWave != null ? SiteState.Lost : SiteState.Taken, held.Kind, kinds));
            else
                rows.Add(new SiteRow(site, SiteState.Free, string.Empty, kinds));
        }

        return rows;
    }

    /// <summary>A place the run shows: it can sell and has at least one row (D46).</summary>
    private bool Shown(LdPlaceKind place) => place switch
    {
        LdPlaceKind.Armoury => SetsOnSale().Any(),
        LdPlaceKind.Yard => SiteRowsNow().Count > 0,
        _ => false,
    };

    private MusterFrame? PointOf(LdPlaceKind place) => place switch
    {
        LdPlaceKind.Armoury => _plan.ArmouryPoint,
        LdPlaceKind.Yard => _plan.YardPoint,
        _ => null,
    };

    private float ReachMetres(LdPlaceKind place) => (place == LdPlaceKind.Armoury ? _plan.Armoury?.Reach : _plan.Yard?.Reach) ?? 0f;

    /// <summary>The near rule (D37): the lord stands, within the place's reach on the ground and within <see cref="PlaceHeightBand"/> of its height. A coordinate that is not a number is not near.</summary>
    private bool Within(LdPlaceKind place)
    {
        var point = PointOf(place);
        if (point == null || !_lord.Present)
            return false;

        var flat = LdSpace.Flat(_lord.At, new LdPoint(point.X, point.Y, point.Z));
        return flat <= ReachMetres(place) && Math.Abs(_lord.At.Z - point.Z) <= PlaceHeightBand;
    }

    /// <summary>How the buyer came to the place: by the open errand to it (on foot, or teleported), else a person's own walk.</summary>
    private ErrandReach ReachOf(LdPlaceKind place) =>
        _errand?.Place != place ? ErrandReach.Person : _errand.Teleported ? ErrandReach.Teleport : ErrandReach.Walk;

    private void CloseErrandAt(LdPlaceKind place)
    {
        if (_errand?.Place == place)
            CloseErrand();
    }

    private void CloseErrand()
    {
        _errand = null;
        _errandEnd = true;
    }

    /// <summary>
    /// The timer began a wave on a tick whose errand order (the walk's start, or its teleport) was already written. The order is withdrawn: the director serves the wave's own moves first (a rider's sally), and a
    /// teleport after them would carry the lord back to the place. The errand is closed by the wave's start, so its release goes out on the next tick.
    /// </summary>
    private static void WithdrawErrandOrder(RunEffects fx)
    {
        if (fx.Errand is { Step: not ErrandStep.End })
            fx.Errand = null;
    }

    /// <summary>A wave begins: an errand still open is over, and every place may be walked to again after it.</summary>
    private void ForgetErrands()
    {
        EndErrand();
        _unreached.Clear();
    }

    /// <summary>
    /// The errand's clock, counted from the snapshots' <c>dt</c> so a replay sees the same walk: the start goes out once, the clock stops while the lord stands at the place, the teleport goes out once when
    /// the budget is spent, and an errand whose teleport did not arrive is given up. A lord who fell on the way walks nowhere, so his errand is closed and no teleport is asked for a body that is
    /// gone. A closed errand's release goes out alone on the next tick. A run that ends hands out nothing more, so the director releases an open walk itself when it finishes.
    /// </summary>
    private void TickErrand(float dt, RunEffects fx)
    {
        if (_errandEnd)
        {
            _errandEnd = false;
            fx.Errand = new ErrandOrder(ErrandStep.End, LdPlaceKind.None, null);
            return;
        }

        if (_errand == null)
            return;

        var point = PointOf(_errand.Place);
        if (point == null || !_lord.Present)
        {
            CloseErrand();
            return;
        }

        var at = LdPlace.From(point);
        if (!_errand.Started)
        {
            _errand.Started = true;
            fx.Errand = new ErrandOrder(ErrandStep.Start, _errand.Place, at);
        }

        if (Within(_errand.Place))
            return;

        _errand.Seconds += dt;
        if (!_errand.Teleported && _errand.Seconds >= ErrandBudgetSeconds)
        {
            _errand.Teleported = true;
            fx.Errand = new ErrandOrder(ErrandStep.Teleport, _errand.Place, at);
        }
        else if (_errand.Teleported && _errand.Seconds >= ErrandBudgetSeconds + ErrandSettleSeconds)
        {
            _unreached.Add(_errand.Place);
            CloseErrand();
        }
    }

    /// <summary>
    /// Hands the director what was bought since the last tick. The loadout is read here, not at the buy, so two sets bought before one tick go out as one kit. Returns whether anything was handed out:
    /// the tick then starts no wave, so the swap and the engine stand before the wave's tactic reads the field.
    /// </summary>
    private bool TakePendingLocations(RunEffects fx)
    {
        var handed = _pendingSwap || _pendingEngines.Count > 0;
        if (_pendingSwap)
            fx.SwapGear = EffectiveLoadout;

        _pendingSwap = false;
        fx.DeployEngines.AddRange(_pendingEngines);
        _pendingEngines.Clear();
        return handed;
    }

    private void ObserveLord(LdLordFacts? lord) => _lord = lord ?? LdLordFacts.None;

    private LocationsView LocationsViewNow()
    {
        var offer = _plan.Locations;
        if (!offer.Armoury && !offer.Mangonel && !offer.Trebuchet)
            return LocationsView.None;

        var gear = SetsOnSale().Select(s => new GearRow(s, FortPrice(s.Price), _ownedSets.Contains(s) ? GearState.Owned : GearState.ForSale)).ToList();
        var sites = SiteRowsNow();
        var near = gear.Count > 0 && Within(LdPlaceKind.Armoury) ? LdPlaceKind.Armoury : sites.Count > 0 && Within(LdPlaceKind.Yard) ? LdPlaceKind.Yard : LdPlaceKind.None;
        return new LocationsView(near, _lord.Present && _lord.Own, gear, sites, _errand?.Place ?? LdPlaceKind.None, _unreached.ToList()) { LordStands = _lord.Present };
    }
}
