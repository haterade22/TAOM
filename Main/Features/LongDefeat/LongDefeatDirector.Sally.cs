using TAOM.Features.LongDefeat.Diagnostics;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Features.LongDefeat;

/// <summary>
/// The mounted sally at the director (phase2-plan.md "The sally" and Slice 10, spike S9). At boot the home point (the recorded foot muster, else the keep) and the sally point each get a navmesh and boundary
/// check and a line. A sally point that fails means Cavalry rides from the start with no sally; a home point that fails refuses the boot, because the boot adds no cavalry to the deployment plan and
/// so proves no spawn frame for a lord with no place. At each wave's start a mounted lord whose body is his own is teleported to the sally point, or remounted there in one step when he is on foot (his horse
/// died, or he was knocked off or dismounted: the engine clears <c>HasMount</c> in all three, and a horse left riderless is never faded); at each clear he is teleported home. A taken-over soldier's body gets no
/// sally. The lines and the three checks (<c>sally</c>, <c>sally-return</c>, <c>sally-contact</c>) are what Mike reads to choose between the outside point, the courtyard point and "mounted from Begin, no
/// sally". No mounted class is on offer before Slice 11, so only the director's tests and a hand-edited <c>classes.json</c> row with <c>"mounted": true</c> reach any of this: the catalog swaps an unoffered
/// class for one on offer, with a warning, so the autorun's class key alone does not.
/// </summary>
internal sealed partial class LongDefeatDirector
{
    private const string ContactSite = "LongDefeatDirector.OnContactHit";

    private LdPlace? _homePoint;
    private LdPlace? _sallyPoint;
    private string _noSally = "";
    private int _sallyWave;
    private SallyWatch? _sally;
    private volatile bool _sallyHits;

    /// <summary>
    /// At the boot: checks the home point and the sally point, writes a line for each, and keeps the ones that stand. Only for a mounted class. Returns why the run is refused, or empty: a mounted lord is
    /// spawned at the home point, and with none the engine would place him from a cavalry deployment plan nobody built (the boot adds no cavalry troops and checks no cavalry frame).
    /// </summary>
    private string PrepareSally()
    {
        if (!LordRides)
            return "";

        var home = _plan.Muster?.Foot is { } foot ? LdPlace.From(foot) : _keepAt is { } keep ? LdPlace.At(keep) : null;
        if (home == null)
        {
            _logger.LogWarning($"{Tag}[Sally] no home point: the plan records no foot muster and the field gave no keep");
            return "the mounted class has no home point: the plan records no foot muster and the field gave no keep";
        }

        var facts = _field.SallyPointUsable(home);
        LogPoint("home point", home, facts);
        if (!facts.Usable)
            return $"the mounted class has no usable home point {PointText(home.Point)} (navmesh {Yes(facts.Navmesh)}, boundary {Yes(facts.Boundary)}): its lord has no place to spawn";

        _homePoint = home;
        if (_plan.Sally is not { } recorded)
        {
            _noSally = "no-sally-point";
            _logger.LogInfo($"{Tag}[Sally] no sally point recorded; Cavalry rides from Begin with no sally");
            return "";
        }

        var place = LdPlace.From(recorded);
        var sally = _field.SallyPointUsable(place);
        LogPoint("point", place, sally);
        if (sally.Usable)
        {
            _sallyPoint = place;
            return "";
        }

        _noSally = "point-unusable";
        _logger.LogWarning($"{Tag}[Sally] Cavalry rides from Begin with no sally (the sally point is unusable)");
        return "";
    }

    private void LogPoint(string label, LdPlace place, LdPointFacts facts) =>
        _logger.LogInfo($"{Tag}[Sally] {label} {PointText(place.Point)} navmesh {Yes(facts.Navmesh)} boundary {Yes(facts.Boundary)}");

    /// <summary>The wave's start: a mounted lord goes out. Before the wave's first attacker spawns.</summary>
    private void SallyOut(int wave)
    {
        _sallyWave = wave;
        if (_sallyPoint is not { } point || _plan.Loadout is not { } loadout)
        {
            SkipSally(wave, _noSally.Length > 0 ? _noSally : "point-unusable");
            return;
        }

        var before = _field.ReadLord(false);
        if (!before.Present || !before.Own)
        {
            SkipSally(wave, before.Present ? "taken-over" : "no-lord");
            return;
        }

        var deathsBefore = _run.Stats.PlayerDeaths;
        LdRemount? remount = null;
        bool moved;
        string how;
        if (before.HasMount)
        {
            how = "teleport";
            moved = _field.TeleportPlayer(point);
        }
        else
        {
            how = "remount";
            remount = _field.RemountPlayer(point, loadout);
            moved = remount.Done;
        }

        var after = _field.ReadLord(true);
        var deathsAfter = _run.Stats.PlayerDeaths;
        var dist = after.Present ? point.FlatDistanceTo(after.At) : float.NaN;
        _logger.LogInfo($"{Tag}[Sally] w{wave} out: {how} main={after.Id} mount={Yes(after.HasMount)} at={PointText(after.At)} dist={F1(dist)} formation={after.Formation} order_at={OrderText(after)}"
            + RemountTail(remount, after, deathsBefore, deathsAfter));
        LongDefeatChecks.Record(_outcomes, LongDefeatChecks.SallyId, "w" + wave, LongDefeatSallyChecks.Out(wave, how, after, point, remount, deathsBefore, deathsAfter));
        if (!moved)
            return;

        _sally = new SallyWatch(wave, point, after.Present ? after.At : point.Point, deathsBefore);
        _sallyHits = true;
    }

    // The horse of the new body was built before its rider and took no record (phase2-plan.md risk 7): AdoptIfNew derives the lord's stats again on the next tick, and DressNewBody refreshes the horse too.

    private static string RemountTail(LdRemount? remount, LdLordFacts after, int deathsBefore, int deathsAfter) =>
        remount == null
            ? ""
            : $" old_state={(remount.OldState.Length == 0 ? "-" : remount.OldState)} health_share={F2(remount.HealthShare)} main_after={Yes(after.Present)} deaths={deathsBefore}->{deathsAfter}"
            + (remount.Problem.Length == 0 ? "" : $" problem={remount.Problem}");

    private void SkipSally(int wave, string reason)
    {
        _logger.LogInfo($"{Tag}[Sally] w{wave} no sally: {reason}");
        LongDefeatChecks.Record(_outcomes, LongDefeatChecks.SallyId, "w" + wave, LongDefeatSallyChecks.Skipped(wave, reason));
    }

    /// <summary>A clear: a lord who went out this wave comes home. A wave that never went out has nothing to bring back.</summary>
    private void SallyHome()
    {
        var watch = _sally;
        if (watch == null)
            return;

        EndSallyWatch("none-before-clear");

        var lord = _field.ReadLord(false);
        if (!lord.Present || !lord.Own || _homePoint is not { } home)
        {
            var reason = !lord.Present ? "no-lord" : !lord.Own ? "taken-over" : "point-unusable";
            _logger.LogInfo($"{Tag}[Sally] w{watch.Wave} no return: {reason}");
            LongDefeatChecks.Record(_outcomes, LongDefeatChecks.SallyReturnId, "w" + watch.Wave, LongDefeatSallyChecks.Skipped(watch.Wave, reason));
            return;
        }

        _field.TeleportPlayer(home);
        var after = _field.ReadLord(false);
        var verdict = LongDefeatSallyChecks.Return(watch.Wave, after, home, _keepRadius, watch.DeathsAtOut, _run.Stats.PlayerDeaths);
        var dist = after.Present ? home.FlatDistanceTo(after.At) : float.NaN;
        _logger.LogInfo($"{Tag}[Sally] w{watch.Wave} home: teleport main={after.Id} at={PointText(after.At)} dist={F1(dist)} outside_boundary_max={F1(watch.MaxOutside)}");
        LongDefeatChecks.Record(_outcomes, LongDefeatChecks.SallyReturnId, "w" + watch.Wave, verdict);
    }

    /// <summary>Closes the contact watch of a wave that sallied: no more hits are wanted, and a wave with no contact judged yet gets its skip, with the reason (a clear, or the run's end).</summary>
    private void EndSallyWatch(string reason)
    {
        var watch = _sally;
        if (watch == null)
            return;

        _sally = null;
        _sallyHits = false;
        if (watch.ContactJudged)
            return;

        watch.ContactJudged = true;
        LongDefeatChecks.Record(_outcomes, LongDefeatChecks.SallyContactId, "w" + watch.Wave, LongDefeatSallyChecks.NoContact(watch.Wave, reason, watch.Seconds, watch.Counts));
    }

    /// <summary>One tick while a lord is out: the clock, the boundary stretch, the 180 s contact window, and every 5 s the line while he is more than 20 m outside the keep.</summary>
    private void TickSally(float dt)
    {
        var watch = _sally;
        if (watch == null)
            return;

        var lineDue = watch.Advance(dt, _field.ReadLord(false), _keepAt);
        if (watch.Silent)
        {
            watch.ContactJudged = true;
            LongDefeatChecks.Record(_outcomes, LongDefeatChecks.SallyContactId, "w" + watch.Wave, LongDefeatSallyChecks.NoContact(watch.Wave, "none-in-180s", watch.Seconds, watch.Counts));
        }

        if (!lineDue)
            return;

        var look = _field.ReadLord(true);
        _logger.LogInfo($"{Tag}[Sally] w{watch.Wave} t={F1(watch.Seconds)} {watch.Counts} nearest_attacker={F1(look.NearestAttacker)} to_formation={F1(look.ToFormation)} gate={look.Gate} outside_boundary={F1(watch.Outside)}");
    }

    /// <summary>The engine raised a hit that may touch the lord or his horse. May run off the main thread; only a sally wave looks at it.</summary>
    public void OnContactHit(object? affected, object? affector, float victimHealth)
    {
        if (!_started || !_sallyHits || !_field.TryContact(affected, affector, victimHealth, out var contact))
            return;

        _deferred.RunOrDefer(ContactSite, () => CountContact(contact));
    }

    private void CountContact(LdContact contact)
    {
        if (_sally is not { } watch || !watch.Add(contact))
            return;

        LongDefeatChecks.Record(_outcomes, LongDefeatChecks.SallyContactId, "w" + watch.Wave, LongDefeatSallyChecks.Contact(watch.Wave, watch.Seconds, watch.Counts));
        _logger.LogInfo($"{Tag}[Sally] w{watch.Wave} contact at t={F1(watch.Seconds)} {watch.Counts}");
    }

    /// <summary>A removal or deletion reached an agent: the field keeps it if it is a body of the lord's. May run off the main thread.</summary>
    public void OnBodyEvent(object agent, string callback, string state)
    {
        if (_started)
            _field.NoteBodyEvent(agent, callback, state);
    }

    // The callbacks that reached the lord's bodies since the last tick, logged with their state. A planned body reported killed or knocked out fails the wave's sally check after the fact: the step looked clean
    // when it was checked, and native only said otherwise later.
    private void DrainBodyEvents()
    {
        if (!LordRides)
            return;

        foreach (var e in _field.TakeBodyEvents())
        {
            _logger.LogInfo($"{Tag}[Sally] {(e.Planned ? "old body" : "lord body")} {e.Callback} state={e.State}");
            if (e.Planned && e.Loss)
                LongDefeatChecks.Record(_outcomes, LongDefeatChecks.SallyId, "w" + _sallyWave, LongDefeatSallyChecks.OldBodyLost(_sallyWave, e));
        }
    }

    private static string PointText(LdPoint p) => $"({F1(p.X)}, {F1(p.Y)}, {F1(p.Z)})";

    private static string OrderText(LdLordFacts lord) => lord.OrderAt is { } at ? PointText(at) : "none";

    private static string Yes(bool v) => v ? "yes" : "no";
}
