using System;
using System.Collections.Generic;

namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>
/// Everything the engine layer measures for one tick. A lane, tower or point missing from the lists has nothing to
/// report. LiveBySide may be left empty: the match then sums the lane counts.
/// </summary>
internal sealed class MatchSnapshot
{
    public float Dt { get; init; }

    public IReadOnlyList<LaneSnapshot> Lanes { get; init; } = Array.Empty<LaneSnapshot>();

    public IReadOnlyList<TowerTargets> TowerTargets { get; init; } = Array.Empty<TowerTargets>();

    public IReadOnlyList<CaptureCounts> Capture { get; init; } = Array.Empty<CaptureCounts>();

    public IReadOnlyDictionary<MatchSide, int> LiveBySide { get; init; } = new Dictionary<MatchSide, int>();
}
