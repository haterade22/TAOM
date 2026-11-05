using System.Linq;
using TAOM.Adapters;
using TAOM.Features.LongDefeat.Diagnostics;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Features.LongDefeat;

/// <summary>
/// The endless scale's proof (phase2-plan.md section 6, <c>scale</c>): for a wave past the authored list the director samples one standing
/// attacker through the effects adapter at spawn, again every state line while the wave runs, and writes the check when the wave clears
/// (or the run ends in it). It also counts any spawn beyond the live room the engine's own numbers allow.
/// </summary>
internal sealed partial class LongDefeatDirector
{
    private ScaleWatch? _scale;
    private int _lastRoom = int.MaxValue;

    private string ScaleText(WaveStart start)
    {
        var spec = _plan.Waves.ForWave(start.Wave);
        return IsScaled(spec) ? $", health x{F2(spec.HealthFactor)} damage x{F2(spec.DamageFactor)} tier steps {spec.TierSteps}" : "";
    }

    private static bool IsScaled(WaveSpec spec) => spec.HealthFactor != 1f || spec.DamageFactor != 1f || spec.TierSteps > 0;

    private void BeginScaleWatch(WaveStart start)
    {
        _scale = null;
        var spec = _plan.Waves.ForWave(start.Wave);
        if (_effects != null && IsScaled(spec))
            _scale = new ScaleWatch(start.Wave, spec);
    }

    private void SampleScaleAtSpawn()
    {
        if (_scale != null && _effects != null)
            _scale.AtSpawn = _effects.ReadScale();
    }

    private void SampleLateScale()
    {
        if (_scale?.AtSpawn == null || _effects == null || _run.Phase != RunPhase.Wave)
            return;

        var sample = _effects.ReadScale();
        if (sample.HasAgent)
            _scale.Late = sample;
    }

    /// <summary>
    /// What the engine counts standing now must fit the room the last tick spawned into (the run spawns only up to that room, so a count above it
    /// is an attacker nothing queued, or a spawn the run did not count). The room is kept every tick, so the tick that opens a wave is the one
    /// the next read measures; a room that shrank since (a bought pack) cannot raise a count and is no overspawn.
    /// </summary>
    private void WatchRoom(LdReading read)
    {
        if (_scale != null)
            _scale.OverRoom = System.Math.Max(_scale.OverRoom, read.AttackersStanding - _lastRoom);

        _lastRoom = SurvivalRun.LiveRoom(_plan.Rules, read.SiegeBattleSize, read.DefendersStanding);
    }

    /// <summary>Writes the wave's check once. A wave whose first batch never closed has no sample at spawn and writes nothing.</summary>
    private void FlushScale()
    {
        var watch = _scale;
        _scale = null;
        if (watch?.AtSpawn == null || _effects == null)
            return;

        var spec = watch.Spec;
        var verdict = LongDefeatChecks.Scale(watch.Wave, spec.HealthFactor, spec.DamageFactor, spec.TierSteps, watch.AtSpawn, watch.Late,
            spec.Mix.Select(m => m.TroopId).ToList(), _effects.ReadScale().AmplifiedBlows, watch.OverRoom);
        LongDefeatChecks.Record(_outcomes, LongDefeatChecks.ScaleId, "w" + watch.Wave, verdict);
        _logger.LogInfo($"{Tag}[Scale] w{watch.Wave} {verdict.Detail}");
    }

    private sealed class ScaleWatch
    {
        public ScaleWatch(int wave, WaveSpec spec)
        {
            Wave = wave;
            Spec = spec;
        }

        public int Wave { get; }
        public WaveSpec Spec { get; }
        public LdScaleSample? AtSpawn { get; set; }
        public LdScaleSample? Late { get; set; }
        public int OverRoom { get; set; }
    }
}
