using System.Collections.Generic;

namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>One lane's waypoint path. Path[0] is at the West base end, the last point at the East base end.</summary>
internal sealed record LaneLayout(int Index, IReadOnlyList<Vec3f> Path);
