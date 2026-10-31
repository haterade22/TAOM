using System;

namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>The random source the rules draw from, so a test or a replay can pin it.</summary>
internal interface IFordsRandom
{
    /// <summary>An integer in 0..maxExclusive-1; 0 when the bound is not positive.</summary>
    int Next(int maxExclusive);

    /// <summary>A float in [0, 1).</summary>
    float NextFloat();
}

internal sealed class SeededFordsRandom : IFordsRandom
{
    private readonly Random _random;

    public SeededFordsRandom(int seed)
    {
        _random = new Random(seed);
    }

    public int Next(int maxExclusive) => maxExclusive > 0 ? _random.Next(maxExclusive) : 0;

    public float NextFloat()
    {
        // NextDouble is below 1.0, but the float cast of a value just under 1.0 can round up to 1.0.
        var f = (float)_random.NextDouble();
        return f >= 1f ? 0.99999994f : f;
    }
}
