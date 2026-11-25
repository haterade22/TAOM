using System.Collections.Generic;

namespace TAOM.Features.TrainingGrounds.Waves.Domain;

/// <summary>
/// The stakes the master offers for a cap, at most three: a quarter, a half and the whole of it, each rounded down to a multiple
/// of ten, without those under the table's minimum. The run's small stake (the smallest wager of the first wave) goes in front
/// when it is legal and below all of them, and takes the quarter's place when that makes four. So the smallest wager is never above
/// the first wave's, and a player who could enter can always go on.
/// </summary>
internal static class StakeOptions
{
    private const int Step = 10;

    /// <summary>
    /// Ascending, without a value under <paramref name="minStake"/> and without a duplicate. Empty when even the cap is under the
    /// minimum, and then the small stake does not go in either. A <paramref name="smallStake"/> that is 0, under the minimum once
    /// rounded, or not below the first of the other choices is not offered.
    /// </summary>
    public static IReadOnlyList<int> For(int cap, int minStake, int smallStake = 0)
    {
        var choices = new List<int>(4);
        foreach (var raw in new[] { cap / 4L, cap / 2L, (long)cap })
        {
            var value = (int)(raw / Step * Step);
            if (value >= minStake && value > 0 && !choices.Contains(value))
                choices.Add(value);
        }

        var small = smallStake / Step * Step;
        if (small > 0 && small >= minStake && choices.Count > 0 && small < choices[0])
        {
            choices.Insert(0, small);
            if (choices.Count > 3)
                choices.RemoveAt(1);
        }

        return choices;
    }
}
