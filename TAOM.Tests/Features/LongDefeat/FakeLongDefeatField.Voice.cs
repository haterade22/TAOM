using System;
using System.Collections.Generic;
using TAOM.Adapters;

namespace TAOM.Tests.Features.LongDefeat;

/// <summary>The hero voice half of the field fake (#1569): every line asked for and every stop is recorded, and a test sets what a play answers.</summary>
internal sealed partial class FakeLongDefeatField : ILongDefeatHeroVoiceAdapter
{
    /// <summary>Every full path <see cref="PlayHeroLine"/> was asked for, in order.</summary>
    public List<string> HeroLinesPlayed { get; } = new();

    /// <summary>How many times <see cref="StopHeroLine"/> was called.</summary>
    public int HeroLineStops { get; private set; }

    /// <summary>What a play answers; true unless a test says the engine refused the sound.</summary>
    public bool HeroLineAnswer { get; set; } = true;

    /// <summary>When set, a play throws it after recording the path.</summary>
    public Exception? HeroLineFault { get; set; }

    public bool PlayHeroLine(string fullPath)
    {
        HeroLinesPlayed.Add(fullPath);
        if (HeroLineFault != null)
            throw HeroLineFault;

        return HeroLineAnswer;
    }

    public void StopHeroLine() => HeroLineStops++;
}
