using System.Collections.Generic;
using System.Linq;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Features.HeroDefence.Domain;

/// <summary>Whether a hero can stand at a clicked point. It takes only what the engine layer already read, so it needs no engine type.</summary>
internal static class PlacementRules
{
    /// <summary><see cref="PlaceResult.Placed"/> when the point is legal. Height does not count in a distance, because a plateau is above the road.</summary>
    public static PlaceResult Check(HeroClass heroClass, PlacementProbe probe, IEnumerable<Vec3f> otherPosts, DefenceMap map, DefenceRules rules)
    {
        var point = probe.Point;
        if (!IsFinite(point) || probe.FaceGroup < 0)
            return PlaceResult.OffNavmesh;

        if (!probe.InsideBoundary)
            return PlaceResult.OutOfBounds;

        if (!map.Zones.TryGetValue(probe.FaceGroup, out var zone))
            return PlaceResult.WrongGround;

        if (heroClass == HeroClass.Melee && (zone != GroundZone.Road || !probe.LinksToRoad))
            return PlaceResult.WrongGround;

        if (point.Distance2D(map.Entry) < rules.RoadEndClearMetres || point.Distance2D(map.Exit) < rules.RoadEndClearMetres)
            return PlaceResult.TooCloseToRoadEnd;

        if (otherPosts.Any(post => post.Distance2D(point) < rules.MinPostGapMetres))
            return PlaceResult.TooCloseToPost;

        return PlaceResult.Placed;
    }

    private static bool IsFinite(Vec3f p) => IsFinite(p.X) && IsFinite(p.Y) && IsFinite(p.Z);

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
