using System.Collections.Generic;

namespace TAOM.Features.StagedSiege.Domain;

/// <summary>One fight area marker of the lord's hall scene, as plain values (the adapter reads the engine's <c>FightAreaMarker</c>).</summary>
public readonly record struct HallMarker(int AreaIndex, float X, float Y, float Z, float Radius);

/// <summary>
/// Point E of a staged siege (docs/features/staged-siege.md "The lord's hall"): every marker of the hall's highest fight area
/// index, a union of small spheres. An agent is inside when its 3D distance to any of them is within that marker's own radius,
/// vanilla's <c>IsPositionInRange</c> reading (<c>AreaMarker.cs:33-36</c>, used by <c>LordsHallFightMissionController.IsAgentInArea</c>).
/// The centroid places the flag and the HUD marker and decides nothing. A marker with a non-finite coordinate or a radius that is not
/// positive is dropped on build, and a NaN position fails the comparison, so it is never inside.
/// </summary>
public sealed class HallZone
{
    private static readonly HallZone None = new(-1, new HallMarker[0], default);

    private readonly HallMarker[] _markers;

    private HallZone(int areaIndex, HallMarker[] markers, StagePoint centroid)
    {
        AreaIndex = areaIndex;
        _markers = markers;
        Centroid = centroid;
    }

    /// <summary>No usable marker: there is no point E.</summary>
    public bool IsEmpty => _markers.Length == 0;

    /// <summary>The highest area index among the usable markers; -1 when there are none.</summary>
    public int AreaIndex { get; }

    /// <summary>How many markers make up E.</summary>
    public int MarkerCount => _markers.Length;

    public StagePoint Centroid { get; }

    public static HallZone Build(IReadOnlyList<HallMarker>? markers)
    {
        if (markers == null)
            return None;

        var usable = new List<HallMarker>(markers.Count);
        var highest = int.MinValue;
        foreach (var marker in markers)
        {
            if (!IsUsable(marker))
                continue;
            usable.Add(marker);
            if (marker.AreaIndex > highest)
                highest = marker.AreaIndex;
        }

        if (usable.Count == 0)
            return None;

        var last = new List<HallMarker>();
        float sumX = 0f, sumY = 0f, sumZ = 0f;
        foreach (var marker in usable)
        {
            if (marker.AreaIndex != highest)
                continue;
            last.Add(marker);
            sumX += marker.X;
            sumY += marker.Y;
            sumZ += marker.Z;
        }

        return new HallZone(highest, last.ToArray(), new StagePoint(sumX / last.Count, sumY / last.Count, sumZ / last.Count));
    }

    /// <summary>Whether a position is within the radius of any E marker. A non-finite position is never inside.</summary>
    public bool Contains(float x, float y, float z)
    {
        for (var i = 0; i < _markers.Length; i++)
        {
            var m = _markers[i];
            var dx = x - m.X;
            var dy = y - m.Y;
            var dz = z - m.Z;
            // A NaN anywhere makes this comparison false, so a bad position is never inside.
            if (dx * dx + dy * dy + dz * dz <= m.Radius * m.Radius)
                return true;
        }

        return false;
    }

    private static bool IsUsable(HallMarker m) =>
        IsFinite(m.X) && IsFinite(m.Y) && IsFinite(m.Z) && IsFinite(m.Radius) && m.Radius > 0f;

    private static bool IsFinite(float value) => !(float.IsNaN(value) || float.IsInfinity(value));
}
