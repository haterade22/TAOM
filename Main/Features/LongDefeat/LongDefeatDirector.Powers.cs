using TAOM.Features.LongDefeat.Diagnostics;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Features.LongDefeat;

/// <summary>
/// The two affixes that act on the field instead of on an attacker (phase2-plan.md section 2, Slice 8). The Hammer of the Underworld sets the live rams' and towers' hit points at each wave start (factor 1
/// restores the vanilla value) and proves the write. Winged Terror pulses the defenders' morale on the run's clock and proves the pulses from their own counts when the wave ends. Both record under the
/// <c>affix-effect</c> id with their power as key, and neither reads a soldier, so a wave whose only powers are these asks the stat read for nothing.
/// </summary>
internal sealed partial class LongDefeatDirector
{
    private LdEngineReport? _engineReport;
    private TerrorWatch? _terror;

    /// <summary>Writes the engines' hit points for the wave that is starting and keeps what they read back, for the hammer's check in <see cref="BeginAffixes"/>.</summary>
    private void SetEngineHitPoints(float factor)
    {
        if (_effects == null)
            return;

        _engineReport = _effects.SetEngineHitPoints(factor);
        var engines = string.Join(", ", System.Linq.Enumerable.Select(_engineReport.Engines, e => $"{e.Kind} {F1(e.MaxHitPoints)} of {F1(e.Vanilla)} (now {F1(e.HitPoints)})"));
        _logger.LogInfo($"{Tag}[Hammer] w{_run.Wave} engine hit points x{F2(factor)}: {_engineReport.Engines.Count} engine(s){(engines.Length == 0 ? "" : " " + engines)}; "
            + $"gates {_engineReport.Gates} visible, {_engineReport.GatesDestroyed} destroyed");
    }

    /// <summary>
    /// The hammer's check, from the write this wave's start just made. Only a hammer wave records one. The factor it expects is worked out from the plan's percent here, apart from
    /// <see cref="RunModifiers.EngineHitPoints"/>, so a change in that rule fails the check instead of being read back as itself (the reader keeps the percent within -90 to 300, so no clamp applies).
    /// </summary>
    private void RecordHammer(int wave)
    {
        if (_engineReport == null)
            return;

        var expected = 1f + _plan.Affixes.Value(AffixPlan.Hammer, "engineHitPointsPercent") / 100f;
        var verdict = LongDefeatAffixChecks.Hammer(expected, _engineReport);
        LongDefeatChecks.Record(_outcomes, LongDefeatChecks.AffixEffectId, AffixPlan.Hammer, verdict);
        _logger.LogInfo($"{Tag}[Affix] w{wave} effect {verdict.Detail}");
    }

    private void PulseMorale(LdMoralePulse pulse)
    {
        if (_effects == null)
            return;

        var result = _effects.PulseMorale(pulse);
        _terror ??= new TerrorWatch(_run.Wave, pulse.Floor);
        _terror.Tally.Add(result);
        var slow = result.Milliseconds >= LongDefeatAffixChecks.PulseBudgetMilliseconds;
        if (_terror.Tally.Pulses <= LoggedPulses || slow || result.PushedBelow > 0)
            _logger.LogInfo($"{Tag}[Terror] pulse n={result.Drained} ms={F2((float)result.Milliseconds)} defenders={result.Defenders} lost={F1(result.Lost)} pushed_below={result.PushedBelow}{(slow ? " SLOW" : "")}");
    }

    /// <summary>Writes the wave's terror check once, at its clear or at the end of a run inside it. A wave without terror writes nothing; a terror wave with no pulse in it writes a skip.</summary>
    private void FlushTerror()
    {
        var watch = _terror;
        _terror = null;
        if (watch == null)
            return;

        var verdict = LongDefeatAffixChecks.Terror(watch.Wave, watch.Floor, watch.Tally);
        LongDefeatChecks.Record(_outcomes, LongDefeatChecks.AffixEffectId, AffixPlan.Terror, verdict);
        _logger.LogInfo($"{Tag}[Terror] w{watch.Wave} {verdict.Detail}");
    }

    // The first pulses of a wave are logged one by one; later ones only when slow or when they left someone below the floor. The wave's totals come in the check line.
    private const int LoggedPulses = 3;

    private sealed class TerrorWatch
    {
        public TerrorWatch(int wave, float floor)
        {
            Wave = wave;
            Floor = floor;
        }

        public int Wave { get; }
        public float Floor { get; }
        public LdTerrorTally Tally { get; } = new();
    }
}
