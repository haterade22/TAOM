using System;
using System.Collections.Generic;

namespace TAOM.Features.LongDefeat.Domain;

/// <summary>The affix side of the run (phase2-plan.md section 2): the roll is a function of the seed and the wave number, so a preview and its wave cannot disagree.</summary>
internal sealed partial class SurvivalRun
{
    private IReadOnlyList<string> _waveAffixes = Array.Empty<string>();

    private ulong Seed => _plan.Options.Seed ?? 0UL;

    /// <summary>The power ids of the running wave, rolled when its horn sounds; between waves, those of the wave just cleared; empty before the first wave.</summary>
    public IReadOnlyList<string> WaveAffixes => _waveAffixes;

    /// <summary>The power ids the next wave will carry: what the intermission and the preparation show, and what the wave will take at its start.</summary>
    public IReadOnlyList<string> NextAffixes => _plan.Affixes.Roll(Seed, _wave + 1);

    /// <summary>The next wave's spec, for the factors the intermission's line shows (<c>WaveStart</c> no longer carries them).</summary>
    private WaveSpec NextSpec => _plan.Waves.ForWave(_wave + 1);

    private IReadOnlyList<AffixLabel> WaveLabels => _phase == RunPhase.Wave ? _plan.Affixes.Labels(_waveAffixes) : Array.Empty<AffixLabel>();
}
