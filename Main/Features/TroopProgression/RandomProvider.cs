using System;

namespace TAOM.Features.TroopProgression;

public class RandomProvider : IRandomProvider
{
    [ThreadStatic]
    private static Random _random;

    public int Next(int maxValue)
    {
        _random ??= new Random();
        return _random.Next(maxValue);
    }
}
