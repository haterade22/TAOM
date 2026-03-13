using System;

namespace TAOM.Features.InitialChildGeneration;

public class SystemRandomSource : IRandomSource
{
    private readonly Random _random = new Random();

    public double NextDouble() => _random.NextDouble();

    public int Next(int minInclusive, int maxExclusive) => _random.Next(minInclusive, maxExclusive);
}
