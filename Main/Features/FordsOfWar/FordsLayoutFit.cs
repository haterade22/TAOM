using System;
using System.Collections.Generic;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Features.FordsOfWar;

/// <summary>
/// One lane's share of a fit: how many waypoints the lane had, how many it kept (moved ones included), how many were
/// dropped, the longest straight gap between two consecutive kept points, and how many of those pairs the segment check
/// called blocked (always 0 when no check ran).
/// </summary>
internal readonly record struct FordsLaneFitStats(int Lane, int Waypoints, int Kept, int Moved, int Dropped, float MaxGap, int BadSegments);

/// <summary>A tower that slid along its lane to reach walkable ground: its id and how far it slid along that lane (the arc offset, so a bend in the lane does not shorten it).</summary>
internal readonly record struct FordsTowerMove(string Id, float Distance);

/// <summary>
/// The result of fitting a layout to a scene: the fitted layout, how many points were moved to a walkable spot, how many were
/// dropped, and how many protected points (keeps, lane ends) stayed on an unwalkable spot because nothing nearby was walkable.
/// The rest is the report: per-lane stats, the tower ids that were dropped or slid, and whether the layout is
/// <see cref="Rejected"/>. A rejected fit must not be played: a lane lost every tower of one side, so its keep would stand open.
/// </summary>
internal readonly record struct FordsLayoutFit(MapLayout Layout, int Moved, int Dropped, int Unfixed)
{
    public IReadOnlyList<FordsLaneFitStats> Lanes { get; init; } = Array.Empty<FordsLaneFitStats>();

    public IReadOnlyList<string> DroppedTowers { get; init; } = Array.Empty<string>();

    public IReadOnlyList<FordsTowerMove> MovedTowers { get; init; } = Array.Empty<FordsTowerMove>();

    public bool Rejected { get; init; } = false;

    public string Reason { get; init; } = "";
}
