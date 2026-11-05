using System;
using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat;

/// <summary>
/// The effects adapter with no engine. It writes into the field fake's call list when given one, so a test can read the order of a
/// publish against a spawn, and answers the scale read from a number the test sets.
/// </summary>
internal sealed class FakeLongDefeatEffects : ILongDefeatEffectsAdapter
{
    private readonly List<string> _calls;

    public FakeLongDefeatEffects(List<string>? calls = null) => _calls = calls ?? new List<string>();

    public List<LdStatTable> Published { get; } = new();

    public int Reads { get; private set; }

    /// <summary>What a scale read answers: a standing attacker at the factor, 40 blows amplified.</summary>
    public Func<int, LdScaleSample> SampleFor { get; set; } = _ => new LdScaleSample("orc", 110f, 100f, 40);

    public void PublishStats(LdStatTable table)
    {
        Published.Add(table);
        _calls.Add("Publish:" + table.AttackerDamage.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
    }

    public LdScaleSample ReadScale()
    {
        Reads++;
        _calls.Add("ReadScale");
        return SampleFor(Reads);
    }
}
