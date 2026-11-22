using System.Collections.Generic;

namespace TAOM.Features.TrainingGrounds.Waves.Domain;

/// <summary>The three stakes the master offers for a cap: a quarter, a half and the whole of it, each rounded down to a multiple of ten.</summary>
internal static class StakeOptions
{
    private const int Step = 10;

    /// <summary>Ascending, without a value under <paramref name="minStake"/> and without a duplicate. Empty when even the cap is under the minimum.</summary>
    public static IReadOnlyList<int> For(int cap, int minStake)
    {
        var choices = new List<int>(3);
        foreach (var raw in new[] { cap / 4L, cap / 2L, (long)cap })
        {
            var value = (int)(raw / Step * Step);
            if (value >= minStake && value > 0 && !choices.Contains(value))
                choices.Add(value);
        }

        return choices;
    }
}
