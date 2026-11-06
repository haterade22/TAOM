using System;
using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.LongDefeat.Domain;

/// <summary>
/// The volleys in the run (phase3-plan.md D30, Slice 3). The domain only arms and books: <see cref="ArmVolley"/> marks the Elven Volley's shot, a tower's level 3 volley arms itself on its own
/// <see cref="VolleyClock"/>, the next tick hands the director each armed shot in <see cref="RunEffects.FireVolleys"/>, and the director answers with <see cref="VolleyFired"/> or
/// <see cref="VolleyWithheld"/>. The cooldown starts only on a shot that fired. The clocks run on the wave's seconds and are made at each wave's start (the Elven Volley is ready at once, a tower's
/// volley after its first delay), so a cooldown never carries into the next wave. A wave's counts go into its report row at the clear; a run that ends inside a wave keeps them in
/// <see cref="VolleyAtEnd"/> (the report writes no row for that wave). The director books each foe hit and kill the engine reports through <see cref="VolleyHit"/> (Slice 6), and hands back the origins
/// that passed the boot check through <see cref="LimitVolleyOrigins"/>.
/// </summary>
internal sealed partial class SurvivalRun
{
    private readonly List<TowerVolley> _towerClocks = new();
    private readonly VolleyTally _elvenTally = new();
    private readonly VolleyTally _towerTally = new();
    private VolleyClock? _elven;
    private bool _elvenArmed;
    private bool _volleyOpen;
    private IReadOnlyList<LdPoint>? _usableOrigins;
    private VolleyWithheldReason? _elvenWithheld;
    private float _elvenWithheldAt;

    /// <summary>The volley counts of the wave the run ended in, or null when it ended outside a wave or at a clear (the wave's row then holds them). Set once, when the run ends.</summary>
    public VolleyFlush? VolleyAtEnd { get; private set; }

    /// <summary>The origins the Elven Volley looses from: the recorded ones, or the subset the boot check left usable. Empty when the fortress records none.</summary>
    private IReadOnlyList<LdPoint> VolleyOrigins => _usableOrigins ?? _plan.Volley?.Origins ?? Array.Empty<LdPoint>();

    /// <summary>
    /// The origins that passed the director's boot check (a navmesh, the boundary and a clear ray, phase3-plan.md section 5), in their recorded order. A bad point is dropped, as the Points rule says; with none
    /// left the Elven Volley row is hidden and nothing is sold for it. Called once, before any buy.
    /// </summary>
    public void LimitVolleyOrigins(IReadOnlyList<LdPoint> usable) => _usableOrigins = usable;

    /// <summary>
    /// A hit of a volley's arrow, from the engine's hit callback through the director. Only a foe hit is booked, and a kill when the foe fell; a shield block, a friendly hit and a cancelled one are the
    /// director's check line alone. Ignored outside a wave and after the run ended, so an arrow that lands after the clear is nobody's.
    /// </summary>
    public void VolleyHit(VolleySource source, LdVolleyHit hit)
    {
        if (!_volleyOpen || Result != null || hit.Kind != LdVolleyHitKind.Foe)
            return;

        var tally = source == VolleySource.Elven ? _elvenTally : _towerTally;
        tally.Hits++;
        if (hit.Kill)
            tally.Kills++;
    }

    /// <summary>
    /// Arms the Elven Volley for the next tick: the player's key or the bot's fire. <c>Armed</c> is also the answer to a second press before that tick (one shot). The cooldown is not touched until the
    /// director reports the shot fired; a shot it withheld holds the next ask for a second (<see cref="Cooldown"/> is the answer then).
    /// </summary>
    public VolleyArm ArmVolley()
    {
        if (Result != null)
            return VolleyArm.RunOver;
        if (ElvenLevel() == null)
            return OnSale() ? VolleyArm.NotOwned : VolleyArm.NotSold;
        if (!_volleyOpen)
            return VolleyArm.NotInWave;
        if (_elvenArmed)
            return VolleyArm.Armed;
        if (_elven == null || !_elven.Ready(_waveSeconds))
            return VolleyArm.Cooldown;

        _elvenArmed = true;
        return VolleyArm.Armed;
    }

    /// <summary>A shot the run armed left: its cooldown starts now and the wave books a use and its arrows. Ignored for an unknown source, outside a wave and after the run ended.</summary>
    public void VolleyFired(VolleySource source, int arrows)
    {
        if (!TryVolley(source, out var clock, out var tally))
            return;

        clock.Fired(_waveSeconds);
        tally.Uses++;
        _towerClocks.FirstOrDefault(t => t.Clock == clock)?.Used();
        tally.Arrows += Math.Max(0, arrows);
        if (source == VolleySource.Elven)
            _elvenWithheld = null;
    }

    /// <summary>
    /// A shot the run armed could not leave: the volley stays ready, the next ask waits a second, and the wave books the attempt. The Elven Volley keeps the reason for the HUD. A tower with no post
    /// archer standing stops for the rest of the wave (D22): the posts are manned again only at a clear, so asking each second would book a withheld shot a second to no end.
    /// </summary>
    public void VolleyWithheld(VolleySource source, VolleyWithheldReason reason)
    {
        if (!TryVolley(source, out var clock, out var tally))
            return;

        tally.Withheld++;
        if (source.IsTower && reason == VolleyWithheldReason.NoOwner)
        {
            clock.Stop();
            return;
        }

        clock.Withheld(_waveSeconds);
        if (source != VolleySource.Elven)
            return;

        _elvenWithheld = reason;
        _elvenWithheldAt = _waveSeconds;
    }

    private bool TryVolley(VolleySource source, out VolleyClock clock, out VolleyTally tally)
    {
        clock = null!;
        tally = _towerTally;
        if (!_volleyOpen || Result != null)
            return false;

        if (source == VolleySource.Elven)
        {
            tally = _elvenTally;
            if (_elven == null)
                return false;

            clock = _elven;
            return true;
        }

        var found = _towerClocks.FirstOrDefault(t => source.IsTower && t.Tower.Id == source.TowerId);
        if (found == null)
            return false;

        clock = found.Clock;
        return true;
    }

    /// <summary>Whether the shop lists the Elven Volley at all: the build offers a level, the shop carries the volley and a usable origin is recorded (the tests <see cref="QuoteVolley"/> opens with).</summary>
    private bool OnSale() => _plan.Offered.Volley > 0 && _plan.Shop.Fort?.Volley != null && VolleyOrigins.Count > 0;

    private VolleyLevelPlan? ElvenLevel()
    {
        var volley = _plan.Shop.Fort?.Volley;
        return _volleyLevel > 0 && VolleyOrigins.Count > 0 && volley != null && _volleyLevel <= volley.Levels.Count ? volley.Levels[_volleyLevel - 1] : null;
    }

    /// <summary>A wave begins: fresh clocks and tallies. The towers' volley exists from the tower level that carries it, for each tower with a recorded volley spot.</summary>
    private void InitVolley()
    {
        _elvenTally.Clear();
        _towerTally.Clear();
        _towerClocks.Clear();
        _elvenArmed = false;
        _elvenWithheld = null;
        _volleyOpen = true;
        _elven = ElvenLevel() is { } level ? new VolleyClock(_waveSeconds, 0f, level.CooldownSeconds) : null;

        var towers = _plan.Shop.Fort?.Towers;
        if (towers?.Volley is not { } volley)
            return;

        var carries = -1;
        for (var i = 0; i < towers.Levels.Count && carries < 0; i++)
            carries = towers.Levels[i].Volley ? i : -1;
        if (carries < 0 || _towerLevel <= carries)
            return;

        foreach (var tower in _plan.Towers.Where(t => t.VolleyOrigins.Count > 0))
            _towerClocks.Add(new TowerVolley(tower, new VolleyClock(_waveSeconds, volley.FirstSeconds, volley.CooldownSeconds)));
    }

    /// <summary>Hands the director every shot due this tick: the Elven Volley's armed one, and each tower whose clock is ready. A source stays ready until the director reports it.</summary>
    private void TickVolleys(RunEffects fx)
    {
        if (!_volleyOpen)
            return;

        if (_elvenArmed && ElvenLevel() is { } level)
        {
            var volley = _plan.Shop.Fort!.Volley!;
            fx.FireVolleys.Add(new VolleyOrder(VolleySource.Elven, volley.ItemId, level.Aim, level.Arrows, level.Radius, level.Range, volley.OriginHeight, VolleyOrigins, _wave, _elvenTally.Uses + 1));
        }

        _elvenArmed = false;
        var towers = _plan.Shop.Fort?.Towers?.Volley;
        if (towers == null)
            return;

        foreach (var tower in _towerClocks)
        {
            if (tower.Clock.Ready(_waveSeconds))
                fx.FireVolleys.Add(new VolleyOrder(VolleySource.Tower(tower.Tower.Id), towers.ItemId, VolleyAimMode.Auto, towers.Arrows, towers.Radius, towers.Range, towers.OriginHeight, tower.Tower.VolleyOrigins, _wave, tower.Uses + 1));
        }
    }

    /// <summary>The wave is over (cleared, or the run ended): every clock stops for good and an armed shot is dropped.</summary>
    private void CloseVolleys()
    {
        _elven?.Stop();
        foreach (var tower in _towerClocks)
            tower.Clock.Stop();
        _towerClocks.Clear();
        _elven = null;
        _elvenArmed = false;
        _elvenWithheld = null;
        _volleyOpen = false;
    }

    /// <summary>The run ends: a wave still open keeps its counts for the director's end-of-run line, and a wave already closed into its row has none to give.</summary>
    private void FlushVolleys()
    {
        if (!_volleyOpen)
            return;

        VolleyAtEnd = new VolleyFlush(_wave, _elvenTally.Record(), _towerTally.Record());
        CloseVolleys();
    }

    private VolleyWaveRecord ElvenWaveNow() => _elvenTally.Record();

    private VolleyWaveRecord TowerWaveNow() => _towerTally.Record();

    private VolleyView VolleyViewNow()
    {
        var owned = ElvenLevel() != null;
        if (!_volleyOpen)
            return owned ? VolleyView.None with { Owned = true } : VolleyView.None;

        var ready = !_elvenArmed && _elven != null && _elven.Ready(_waveSeconds);
        var towers = _towerClocks.Select(t => new VolleySourceState(VolleySource.Tower(t.Tower.Id), t.Clock.Ready(_waveSeconds), t.Clock.SecondsLeft(_waveSeconds))).ToList();
        return new VolleyView(owned, ready, _elven?.SecondsLeft(_waveSeconds) ?? 0f, _elvenWithheld, _elvenWithheld != null ? _waveSeconds - _elvenWithheldAt : 0f, towers);
    }

    /// <summary>One tower's volley in the running wave: the tower, its clock and the shots it has fired (the number of its next use is one more).</summary>
    private sealed class TowerVolley
    {
        public TowerVolley(LdTower tower, VolleyClock clock)
        {
            Tower = tower;
            Clock = clock;
        }

        public LdTower Tower { get; }
        public VolleyClock Clock { get; }
        public int Uses { get; private set; }

        public void Used() => Uses++;
    }

    /// <summary>A wave's running counts, kept apart for the Elven Volley and the towers' volleys. Hits and kills are booked by the director through <see cref="VolleyHit"/>.</summary>
    private sealed class VolleyTally
    {
        public int Uses { get; set; }
        public int Arrows { get; set; }
        public int Withheld { get; set; }
        public int Hits { get; set; }
        public int Kills { get; set; }

        public void Clear()
        {
            Uses = 0;
            Arrows = 0;
            Withheld = 0;
            Hits = 0;
            Kills = 0;
        }

        public VolleyWaveRecord Record() => new(Uses, Arrows, Hits, Kills, Withheld);
    }
}
