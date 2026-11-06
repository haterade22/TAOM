using System;
using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.LongDefeat.Domain;

/// <summary>The affix side of the run (phase2-plan.md section 2): the roll is a function of the seed and the wave number, so a preview and its wave cannot disagree.</summary>
internal sealed partial class SurvivalRun
{
    private IReadOnlyList<string> _waveAffixes = Array.Empty<string>();

    private LdTerror? _terror;
    private float _terrorClock;

    private ulong Seed => _plan.Options.Seed ?? 0UL;

    /// <summary>The power ids of the running wave, rolled when its horn sounds; between waves, those of the wave just cleared; empty before the first wave.</summary>
    public IReadOnlyList<string> WaveAffixes => _waveAffixes;

    /// <summary>The power ids the next wave will carry: what the intermission and the preparation show, and what the wave will take at its start.</summary>
    public IReadOnlyList<string> NextAffixes => CutRoll(_plan.Affixes.Roll(Seed, _wave + 1));

    /// <summary>The next wave's spec, for the factors the intermission's line shows (<c>WaveStart</c> no longer carries them).</summary>
    private WaveSpec NextSpec => _plan.Waves.ForWave(_wave + 1);

    /// <summary>The roll the wave carries: a prefix of the stored roll, shortened by the affix cut of the boons owned when the wave begins (a Legend boon), never below none.</summary>
    private IReadOnlyList<string> CutRoll(IReadOnlyList<string> roll) =>
        _affixCut == 0 ? roll : roll.Take(Math.Max(0, roll.Count + _affixCut)).ToArray();

    /// <summary>
    /// Winged Terror's clock: the wave's fighting seconds, from the tick its first batch has closed. A pulse is due each time the clock passes the interval, once per tick however long the
    /// tick was (the remainder carries, a missed interval is not owed). The tick that clears the wave returns before this, so nobody is feared with no attacker standing.
    /// </summary>
    private void TickTerror(float dt, RunEffects fx)
    {
        if (_terror is not { } terror)
            return;

        _terrorClock += dt;
        if (_terrorClock < terror.PulseSeconds)
            return;

        _terrorClock %= terror.PulseSeconds;
        fx.MoralePulse = terror.Pulse;
    }

    private IReadOnlyList<AffixLabel> WaveLabels => _phase == RunPhase.Wave ? _plan.Affixes.Labels(_waveAffixes) : Array.Empty<AffixLabel>();
}
