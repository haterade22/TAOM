using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TAOM.Features.FordsOfWar.Domain;
using TAOM.Features.LongDefeat.Diagnostics;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Features.LongDefeat;

/// <summary>
/// The Elven Volley at the director (phase3-plan.md D22 to D25, D30, Slice 6). The run arms a shot and hands it over in <see cref="RunEffects.FireVolleys"/>; this answers every order inside
/// <see cref="Apply"/> with exactly one of fired or withheld, so a ready source never orders twice and the run's cooldown starts only on a shot that left. The aim reads the field (targets, and the lord's
/// eye for a crosshair), the shots go through the adapter last in the tick, and the arrows' hits are watched for six seconds: the logic's hook may run off the main thread, so a hit is parked on the main
/// thread like the Blades' hits (A21) and gated by its own volatile flag, true only while a window is open. Each use writes one <c>volley</c> line when its window closes, is cut by the wave's clear, or ends with
/// the run. The tower volleys of Slice 8 are answered here too (their arrows are booked in the wave's tally); their own check line joins with that slice.
/// </summary>
internal sealed partial class LongDefeatDirector
{
    private const string VolleyHitSite = "LongDefeatDirector.OnVolleyHit";

    /// <summary>How far past a volley origin the boot looks for a parapet the arrow would strike: two metres out over the wall (R5).</summary>
    private const float ClearanceMetres = 2f;

    private volatile bool _volleyHits;
    private VolleyWatch? _volleyWatch;
    private bool _volleyByBot;
    private int _volleyWithheldWave;
    private VolleyWithheldReason? _volleyWithheldLogged;

    /// <summary>Whether a volley's hit window is open. Read in the engine's hit callbacks, which may run off the main thread, before anything else is done for a hit.</summary>
    public bool WantsVolleyHits => _volleyHits;

    /// <summary>Whether the id is the arrow of the Elven Volley or of the towers' volley, by the plan's shop. The plan never changes, so any thread may ask.</summary>
    public bool IsVolleyItem(string itemId) =>
        itemId.Length > 0 && (itemId == _plan.Shop.Fort?.Volley?.ItemId || itemId == _plan.Shop.Fort?.Towers?.Volley?.ItemId);

    /// <summary>An arrow of a volley struck something (the logic has read it, by item id and the victim's side). May run off the main thread.</summary>
    public void OnVolleyHit(LdVolleyHit hit)
    {
        if (!_started || !_volleyHits || !IsVolleyItem(hit.ItemId))
            return;

        _deferred.RunOrDefer(VolleyHitSite, () => BookVolleyHit(hit));
    }

    private void BookVolleyHit(LdVolleyHit hit)
    {
        var source = hit.ItemId == _plan.Shop.Fort?.Volley?.ItemId ? VolleySource.Elven : VolleySource.Tower(string.Empty);
        _run.VolleyHit(source, hit);
        if (source == VolleySource.Elven)
            _volleyWatch?.Add(hit);
    }

    /// <summary>
    /// The bot's fire: arm the Elven Volley, which the next tick looses. The bot asks only while the view shows the volley ready, which is never while a shot is armed (<see cref="ShopBot.Decide"/>), so the
    /// flag is the bot's own arming; a clear that drops an armed shot resets it (<see cref="OnWaveCleared"/>), so a person's next key is never labelled the bot's.
    /// </summary>
    private void ArmVolleyForBot()
    {
        if (_run.ArmVolley() == VolleyArm.Armed)
            _volleyByBot = true;
    }

    /// <summary>Answers every order the run handed over this tick, last in <see cref="Apply"/>: they read the attackers this tick's batches spawned.</summary>
    private void FireVolleys(IReadOnlyList<VolleyOrder> orders)
    {
        foreach (var order in orders)
            AnswerVolley(order);
    }

    private void AnswerVolley(VolleyOrder order)
    {
        var byBot = _volleyByBot;
        _volleyByBot = false;
        if (_fort == null)
        {
            Withheld(order, VolleyWithheldReason.NoOrigin);
            return;
        }

        var item = _fort.VolleyItem(order.ItemId, order.Range);
        if (item == null)
        {
            Withheld(order, VolleyWithheldReason.NoItem);
            return;
        }

        var targets = _fort.ReadVolleyTargets(order.Origins, order.OriginHeight, order.Range);
        var hit = order.Aim == VolleyAimMode.Crosshair ? CrosshairHit(order, item, targets, byBot) : null;
        var plan = VolleyAim.Plan(order.Origins, order.OriginHeight, targets, order.Aim, hit, order.Arrows, order.Radius, order.Range, item.Speed);
        if (plan.Why is { } why)
        {
            Withheld(order, why);
            return;
        }

        var fire = _fort.FireVolley(order.ItemId, plan.Shots, order.Source.TowerId);
        if (fire.Withheld is { } refused)
        {
            Withheld(order, refused);
            return;
        }

        _run.VolleyFired(order.Source, fire.Added);
        _volleyWithheldLogged = null;
        var gap = hit is { } aimed && targets.Count > 0 ? targets.Min(t => t.Chest.Distance2D(aimed)) : (float?)null;
        var distinct = plan.Shots.Select(s => s.Target).Distinct().Count();
        var use = new VolleyUse(order.Wave, order.Use, order.Source, order.Aim, byBot, gap, order.Arrows, plan.Shots.Count, plan.Candidates, distinct, fire.Added,
            fire.AgentsAfter - fire.AgentsBefore, fire.OwnerTroop, fire.OwnerMultiplier);
        _logger.LogInfo($"{Tag}[Volley] w{order.Wave} use{order.Use} {order.Source.Id} {(order.Aim == VolleyAimMode.Crosshair ? (byBot ? "crosshair by the bot" : "crosshair by a person") : "auto")}: "
            + $"{fire.Added} of {order.Arrows} arrows at {distinct} of {plan.Candidates} attackers in reach, centre {(plan.Centre is { } c ? $"({F1(c.X)}, {F1(c.Y)}, {F1(c.Z)})" : "none")}, owner {fire.OwnerTroop} x{F2(fire.OwnerMultiplier)}");
        if (order.Source == VolleySource.Elven)
            OpenVolleyWatch(use);
    }

    /// <summary>
    /// The point a crosshair volley is aimed at, or null when there is none to aim at (D23). A person's ray is the lord's eye ray; the bot's runs from his eye toward the automatic centre, because nothing turns
    /// an AI lord's look onto the attackers. Both stop at the scene's first hit within the volley's range of the walls (the ray runs that far past the nearest origin).
    /// </summary>
    private Vec3f? CrosshairHit(VolleyOrder order, LdVolleyItem item, IReadOnlyList<LdVolleyTarget> targets, bool byBot)
    {
        if (_fort!.EyeRay() is not { } eye)
            return null;

        var direction = eye.Direction;
        if (byBot)
        {
            var auto = VolleyAim.Plan(order.Origins, order.OriginHeight, targets, VolleyAimMode.Auto, null, order.Arrows, order.Radius, order.Range, item.Speed);
            if (auto.Centre is not { } centre || Toward(eye.Origin, centre) is not { } toward)
                return null;

            direction = toward;
        }

        return _fort.RayHit(eye.Origin, direction, order.Range + NearestOriginDistance(eye.Origin, order));
    }

    /// <summary>
    /// How far the eye stands from the nearest raised origin. The targets lie within <c>Range</c> of an origin, not of the eye, so a ray from the eye must run that much further or a lord standing back withholds
    /// the volley while attackers are in reach of the walls (the triangle inequality: anything within the range of an origin is within the range plus this of the eye).
    /// </summary>
    private static float NearestOriginDistance(Vec3f eye, VolleyOrder order)
    {
        var nearest = float.PositiveInfinity;
        foreach (var origin in order.Origins)
        {
            var distance = eye.Distance(new Vec3f(origin.X, origin.Y, origin.Z + order.OriginHeight));
            if (distance < nearest)
                nearest = distance;
        }

        return float.IsInfinity(nearest) || float.IsNaN(nearest) ? 0f : nearest;
    }

    private static Vec3f? Toward(Vec3f from, Vec3f to)
    {
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        var dz = to.Z - from.Z;
        var length = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        return length > 1e-3f && !float.IsInfinity(length) ? new Vec3f(dx / length, dy / length, dz / length) : null;
    }

    /// <summary>The shot could not leave. The run keeps the volley ready and holds the next ask a second (D30); the line is written when the reason or the wave changes, so a wave with no one in reach is not a line a second.</summary>
    private void Withheld(VolleyOrder order, VolleyWithheldReason why)
    {
        _run.VolleyWithheld(order.Source, why);
        if (_volleyWithheldLogged == why && _volleyWithheldWave == order.Wave)
            return;

        _volleyWithheldLogged = why;
        _volleyWithheldWave = order.Wave;
        _logger.LogInfo($"{Tag}[Volley] w{order.Wave} use{order.Use} {order.Source.Id} withheld: {why}");
    }

    private void OpenVolleyWatch(VolleyUse use)
    {
        CloseVolleyWatch("wave");
        _volleyWatch = new VolleyWatch(use);
        _volleyHits = true;
    }

    /// <summary>The window's clock: it closes when its six seconds have run. Called each tick after the order is answered, so the tick that fired counts its own dt.</summary>
    private void TickVolleyWatch(float dt)
    {
        if (_volleyWatch is not { } watch)
            return;

        watch.Seconds += dt;
        if (watch.Seconds >= LongDefeatVolleyChecks.WindowSeconds)
            CloseVolleyWatch(null);
    }

    /// <summary>Writes the open window's <c>volley</c> line once. <paramref name="ended"/> is null for a window that ran its length, <c>wave</c> for the clear and <c>run</c> for the run's end.</summary>
    private void CloseVolleyWatch(string? ended)
    {
        _volleyHits = false;
        if (_volleyWatch is not { } watch)
            return;

        _volleyWatch = null;
        var use = watch.Use;
        var verdict = LongDefeatVolleyChecks.Volley(use, watch, watch.Seconds, ended);
        LongDefeatChecks.Record(_outcomes, LongDefeatChecks.VolleyId, $"{use.Source.Id}-w{use.Wave.ToString(CultureInfo.InvariantCulture)}u{use.Use.ToString(CultureInfo.InvariantCulture)}", verdict);
        _logger.LogInfo($"{Tag}[Volley] {use.Source.Id} w{use.Wave} use{use.Use} {verdict.Result}: {verdict.Detail}");
    }

    /// <summary>The run ended: an open window is cut and written, and the wave the run ended in keeps the counts its report row never got.</summary>
    private void FinishVolleys()
    {
        CloseVolleyWatch("run");
        if (_run.VolleyAtEnd is not { } end)
            return;

        _logger.LogInfo($"{Tag}[Volley] run ended in wave {end.Wave}: elven uses={end.Elven.Uses} arrows={end.Elven.Arrows} hits={end.Elven.Hits} kills={end.Elven.Kills} withheld={end.Elven.Withheld}; "
            + $"towers uses={end.Tower.Uses} arrows={end.Tower.Arrows} hits={end.Tower.Hits} kills={end.Tower.Kills} withheld={end.Tower.Withheld}");
    }

    /// <summary>
    /// At the boot: the Elven Volley's arrow item exists and is an arrow (phase3-plan.md section 5, "item"). The lookup also writes the item's thrust, speed and reach line before the first volley. A missing or
    /// wrong item hides the volley row with one warning, so nobody pays for a volley that can never fire.
    /// </summary>
    private void CheckVolleyArrow()
    {
        if (_fort == null || _plan.Volley == null || _plan.Shop.Fort?.Volley is not { } volley)
            return;

        if (_fort.VolleyItem(volley.ItemId, volley.Levels.Max(l => l.Range)) != null)
            return;

        _run.LimitVolleyOrigins(Array.Empty<LdPoint>());
        _logger.LogWarning($"{Tag}[Boot] the Elven Volley's arrow '{volley.ItemId}' is missing or is not an arrow; the volley row is hidden");
    }

    /// <summary>
    /// Whether the ray from a volley origin raised by <paramref name="height"/>, out over the wall (away from the keep), hits nothing within <see cref="ClearanceMetres"/>: an arrow that starts in the
    /// parapet strikes it (R5). With no keep to turn away from there is no outside to cast toward and the origin is taken as clear.
    /// </summary>
    private bool OriginClear(LdPoint origin, float height)
    {
        if (_fort == null || _keepAt is not { } keep)
            return true;

        var dx = origin.X - keep.X;
        var dy = origin.Y - keep.Y;
        var length = (float)Math.Sqrt(dx * dx + dy * dy);
        if (!(length > 1e-3f) || float.IsInfinity(length))
            return true;

        return _fort.RayHit(new Vec3f(origin.X, origin.Y, origin.Z + height), new Vec3f(dx / length, dy / length, 0f), ClearanceMetres) == null;
    }
}
