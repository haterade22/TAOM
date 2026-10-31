using System.Collections.Generic;

namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>
/// Knobs for <see cref="LaneLayoutGenerator"/>. LaneSpread is the gap between neighbouring lanes at full bow, so the two
/// side lanes of a three-lane map sit LaneSpread metres either side of the middle line. TowerFractions are fractions of
/// the lane length from a side's own base.
/// </summary>
internal sealed record LayoutOptions
{
    private static readonly float[] DefaultTowerFractions = { 0.36f, 0.20f };

    public LayoutOptions(int LaneCount = 3, float LaneSpread = 45f, float WaypointStep = 12f,
        IReadOnlyList<float>? TowerFractions = null, float KeepInset = 0.04f)
    {
        this.LaneCount = LaneCount;
        this.LaneSpread = LaneSpread;
        this.WaypointStep = WaypointStep;
        this.TowerFractions = TowerFractions ?? DefaultTowerFractions;
        this.KeepInset = KeepInset;
    }

    public int LaneCount { get; init; }
    public float LaneSpread { get; init; }
    public float WaypointStep { get; init; }
    public IReadOnlyList<float> TowerFractions { get; init; }
    public float KeepInset { get; init; }
}
