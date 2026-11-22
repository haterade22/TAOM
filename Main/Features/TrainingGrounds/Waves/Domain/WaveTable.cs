using System.Collections.Generic;

namespace TAOM.Features.TrainingGrounds.Waves.Domain;

/// <summary>
/// The bet table: the waves in order and the rules around them. <see cref="Default"/> is what ships in
/// <c>training_grounds/training_waves.json</c> and what every bad or missing file reverts to; a test pins the two equal.
/// </summary>
internal sealed class WaveTable
{
    public WaveTable(IReadOnlyList<WaveRow> waves, WaveRules rules)
    {
        Waves = waves;
        Rules = rules;
    }

    public IReadOnlyList<WaveRow> Waves { get; }

    public WaveRules Rules { get; }

    public static WaveTable Default { get; } = new(new[]
    {
        Row(3, 1, 1, 0, 0, 1, 500, 15),
        Row(5, 2, 1, 0, 0, 1, 1000, 16),
        Row(6, 2, 2, 1, 0, 2, 2000, 17),
        Row(8, 3, 1, 2, 0, 2, 4000, 18),
        Row(10, 3, 0, 2, 1, 2, 7500, 19),
        Row(12, 4, 0, 1, 1, 3, 12000, 20),
        Row(14, 4, 0, 1, 2, 3, 15000, 20),
        Row(16, 5, 0, 0, 1, 3, 15000, 20),
    }, WaveRules.Default);

    private static WaveRow Row(int count, int atOnce, int tier3, int tier4, int tier5, int stage, int cap, int oddsTenths) =>
        new(count, atOnce, new Dictionary<int, int> { [3] = tier3, [4] = tier4, [5] = tier5 }, stage, cap, oddsTenths);
}
