using System.Collections.Generic;
using TAOM.Features.FordsOfWar.Domain;
using TAOM.Features.LongDefeat.Diagnostics;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Features.LongDefeat;

/// <summary>
/// Morgul Blades at the director (phase2-plan.md section 2 and Slice 9). A hit may arrive off the main thread, already filtered by the logic; the director turns it into the registry's two keys
/// there and on that thread, then parks the dose on the main thread (<see cref="DeferredCallbackQueue.RunOrDefer"/>), since the run holds plain dictionaries. The doses come back from the run as poison
/// ticks, which go to the effects adapter (the only code with a kill besides <c>Fell</c>; it gives each one a living owner), and what the adapter did is booked in the run's report. The wave's
/// <c>affix-effect</c> check is written from the run's own counts when the wave ends.
/// </summary>
internal sealed partial class LongDefeatDirector
{
    // The first ticks of a wave are logged one by one; later ones only when they killed, were withheld, found no owner or went to a substitute owner. The wave's totals come in the check line.
    private const int LoggedTicks = 3;

    private volatile bool _wantsHits;
    private BladesWatch? _blades;

    /// <summary>
    /// Whether the running wave wants the engine's hits: a Morgul Blades wave is running. Read in the engine's hit callback, which may run off the main thread, before anything else is done for a hit, so any other wave costs one
    /// volatile read per hit.
    /// </summary>
    public bool WantsHits => _wantsHits;

    /// <summary>An attacker struck a defender (the logic has dropped null, own, blocked and zero-damage hits, and a mount's blow is its rider's). May run off the main thread.</summary>
    public void OnHit(object affected, object attacker)
    {
        if (!_started || !_wantsHits || !_field.TryHitKeys(affected, attacker, out var victim, out var hitter))
            return;

        _deferred.RunOrDefer(HitSite, () => _run.DefenderHit(victim, hitter));
    }

    private void ApplyPoison(IReadOnlyList<PoisonTick> ticks)
    {
        if (_effects == null)
            return;

        foreach (var tick in ticks)
        {
            var result = _effects.ApplyPoison(tick.Victim, tick.Attacker, tick.Damage);
            _run.PoisonApplied(result.Damage, result.OwnerMissing, result.Withheld);
            if (_blades is not { } watch)
                continue;

            var odd = result.Killed || result.Withheld || result.OwnerMissing || result.Substituted;
            if (++watch.Ticks <= LoggedTicks || odd)
            {
                _logger.LogInfo($"{Tag}[Blades] w{watch.Wave} tick damage={result.Damage}/{tick.Damage}{(result.Killed ? " killed" : "")}{(result.Withheld ? " withheld" : "")}"
                    + $"{(result.OwnerMissing ? " owner_missing" : "")}{(result.Substituted ? " substituted" : "")}");
            }
        }
    }

    /// <summary>A wave that rolled Morgul Blades: remember where the run's poison tally stood, so the check reads this wave's share. Called from <see cref="BeginAffixes"/>.</summary>
    private void WatchBlades(int wave)
    {
        _blades = _effects != null ? new BladesWatch(wave, _run.Stats.Poison.Copy()) : null;
    }

    /// <summary>Writes the wave's blades check once, at its clear or at the end of a run inside it. A wave without the power writes nothing.</summary>
    private void FlushBlades()
    {
        var watch = _blades;
        _blades = null;
        if (watch == null)
            return;

        var verdict = LongDefeatAffixChecks.Blades(watch.Wave, _run.Stats.Poison.Since(watch.Before));
        LongDefeatChecks.Record(_outcomes, LongDefeatChecks.AffixEffectId, AffixPlan.Blades, verdict);
        _logger.LogInfo($"{Tag}[Blades] w{watch.Wave} {verdict.Detail}");
    }

    private sealed class BladesWatch
    {
        public BladesWatch(int wave, PoisonTally before)
        {
            Wave = wave;
            Before = before;
        }

        public int Wave { get; }
        public PoisonTally Before { get; }
        public int Ticks { get; set; }
    }
}
