using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Features.FordsOfWar;

/// <summary>A scene entity carrying a Fords of War tag: the tag text, where it stands, its yaw in the ground plane (radians), and whether it has a body (children or physics) of its own.</summary>
internal readonly record struct FordsMarker(string Tag, Vec3f Position, float Yaw, bool HasBody = false);

/// <summary>
/// Builds a <see cref="MapLayout"/> from the tagged entities of an authored scene (greybox-scene-spec.md): <c>fow_lane_&lt;lane&gt;_&lt;n&gt;</c>,
/// <c>fow_tower_&lt;west|east&gt;_&lt;lane&gt;_&lt;order&gt;</c>, <c>fow_keep_&lt;west|east&gt;</c>, <c>fow_capture_&lt;n&gt;</c> and <c>fow_camp_&lt;n&gt;</c>. Tags it does not
/// know are ignored. The layout is complete when lanes are numbered 0..n-1 (at most four, the default formation classes), each
/// has at least two waypoints, each lane has an outer tower (order 0) for both sides with the orders running on from 0, and both
/// keeps exist. Otherwise the first problem comes back as text so the caller can log one warning and generate instead.
/// Ids follow the generator's: <c>tower_&lt;side&gt;_&lt;lane&gt;_&lt;order&gt;</c>, <c>keep_&lt;side&gt;</c>, <c>point_&lt;n&gt;</c>, <c>camp_&lt;n&gt;</c>.
/// </summary>
internal static class FordsTagLayoutReader
{
    private const int KeepOrder = 99;

    private static readonly Regex LaneTag = new(@"^fow_lane_(\d+)_(\d+)$", RegexOptions.Compiled);
    private static readonly Regex TowerTag = new(@"^fow_tower_(west|east)_(\d+)_(\d+)$", RegexOptions.Compiled);
    private static readonly Regex GunnerTag = new(@"^fow_gunner_(west|east)_(\d+)_(\d+)_(\d+)$", RegexOptions.Compiled);
    private static readonly Regex KeepTag = new(@"^fow_keep_(west|east)$", RegexOptions.Compiled);
    private static readonly Regex CaptureTag = new(@"^fow_capture_(\d+)$", RegexOptions.Compiled);
    private static readonly Regex CampTag = new(@"^fow_camp_(\d+)$", RegexOptions.Compiled);

    public static bool TryRead(string sceneName, IEnumerable<FordsMarker> markers, CampDefaults camp,
        out MapLayout layout, out string problem)
    {
        layout = null!;
        var lanePoints = new SortedDictionary<int, SortedDictionary<int, Vec3f>>();
        var towers = new List<TowerSlot>();
        var points = new SortedDictionary<int, CapturePointSlot>();
        var camps = new SortedDictionary<int, CampSlot>();
        var gunners = new Dictionary<(string Side, int Lane, int Order), SortedDictionary<int, Vec3f>>();

        foreach (var marker in markers)
        {
            var tag = marker.Tag;
            Match m;
            if ((m = LaneTag.Match(tag)).Success)
            {
                var lane = Number(m, 1);
                if (!lanePoints.TryGetValue(lane, out var path))
                    lanePoints[lane] = path = new SortedDictionary<int, Vec3f>();
                path[Number(m, 2)] = marker.Position;
            }
            else if ((m = TowerTag.Match(tag)).Success)
            {
                var side = m.Groups[1].Value == "west" ? MatchSide.West : MatchSide.East;
                var lane = Number(m, 2);
                var order = Number(m, 3);
                towers.Add(new TowerSlot($"tower_{m.Groups[1].Value}_{lane}_{order}", side, lane, order, TowerKind.Tower,
                    marker.Position, marker.Yaw, marker.HasBody));
            }
            else if ((m = GunnerTag.Match(tag)).Success)
            {
                var key = (m.Groups[1].Value, Number(m, 2), Number(m, 3));
                if (!gunners.TryGetValue(key, out var spots))
                    gunners[key] = spots = new SortedDictionary<int, Vec3f>();
                spots[Number(m, 4)] = marker.Position;
            }
            else if ((m = KeepTag.Match(tag)).Success)
            {
                var side = m.Groups[1].Value == "west" ? MatchSide.West : MatchSide.East;
                towers.Add(new TowerSlot($"keep_{m.Groups[1].Value}", side, -1, KeepOrder, TowerKind.Keep, marker.Position, marker.Yaw, marker.HasBody));
            }
            else if ((m = CaptureTag.Match(tag)).Success)
            {
                var n = Number(m, 1);
                points[n] = new CapturePointSlot($"point_{n}", marker.Position);
            }
            else if ((m = CampTag.Match(tag)).Success)
            {
                var n = Number(m, 1);
                camps[n] = new CampSlot($"camp_{n}", marker.Position, camp.TroopId, camp.Count, camp.Reward);
            }
        }

        var lanes = new List<LaneLayout>();
        var issue = CheckLanes(lanePoints, lanes) ?? CheckTowers(towers, lanes.Count) ?? AttachGunners(towers, gunners);
        if (issue != null)
        {
            problem = issue;
            return false;
        }

        problem = string.Empty;
        layout = new MapLayout(sceneName, lanes, towers.GroupBy(t => t.Id).Select(g => g.First()).ToList(),
            points.Values.ToList(), camps.Values.ToList());
        return true;
    }

    private static string? CheckLanes(SortedDictionary<int, SortedDictionary<int, Vec3f>> lanePoints, List<LaneLayout> lanes)
    {
        if (lanePoints.Count == 0)
            return "no fow_lane_<lane>_<n> waypoints found";
        if (lanePoints.Count > LaneStreams.MaxLanes)
            return $"{lanePoints.Count} lanes found, at most {LaneStreams.MaxLanes} are supported";

        var expected = 0;
        foreach (var pair in lanePoints)
        {
            var lane = pair.Key;
            var path = pair.Value;
            if (lane != expected)
                return $"lane {expected} has no waypoints (lanes must be numbered 0..{lanePoints.Count - 1} without gaps)";
            if (path.Count < 2)
                return $"lane {lane} has {path.Count} waypoint(s), at least 2 are needed";

            var order = 0;
            foreach (var n in path.Keys)
            {
                if (n != order)
                    return $"lane {lane} waypoint {order} is missing (waypoints must be numbered from 0 without gaps)";
                order++;
            }

            lanes.Add(new LaneLayout(lane, path.Values.ToList()));
            expected++;
        }

        return null;
    }

    // A tower with spots faces the enemy keep: the entity's own rotation says nothing about the march (all towers of a scene may share one).
    private static string? AttachGunners(List<TowerSlot> towers, Dictionary<(string Side, int Lane, int Order), SortedDictionary<int, Vec3f>> gunners)
    {
        foreach (var pair in gunners)
        {
            var (side, lane, order) = pair.Key;
            var index = towers.FindIndex(t => t.Id == $"tower_{side}_{lane}_{order}");
            if (index < 0)
                return $"fow_gunner_{side}_{lane}_{order}_* names a tower the scene does not have";

            var expected = 0;
            foreach (var n in pair.Value.Keys)
            {
                if (n != expected)
                    return $"fow_gunner_{side}_{lane}_{order}_{expected} is missing (spots must be numbered from 0 without gaps)";
                expected++;
            }

            var slot = towers[index];
            var enemyKeep = towers.First(t => t.Kind == TowerKind.Keep && t.Owner != slot.Owner);
            var yaw = (float)Math.Atan2(enemyKeep.Position.Y - slot.Position.Y, enemyKeep.Position.X - slot.Position.X);
            towers[index] = slot with { GunnerSpots = pair.Value.Values.ToList(), Yaw = yaw };
        }

        return null;
    }

    private static string? CheckTowers(List<TowerSlot> towers, int laneCount)
    {
        // A tower on a lane the scene has no waypoints for is a mistyped tag. Left in, the chain would count it as a lane the
        // owner had already lost and open the enemy keep at match start.
        var stray = towers.FirstOrDefault(t => t.Kind == TowerKind.Tower && t.Lane >= laneCount);
        if (stray != null)
        {
            var side = stray.Owner == MatchSide.West ? "west" : "east";
            return $"fow_tower_{side}_{stray.Lane}_{stray.Order} is on lane {stray.Lane}, but the scene has {laneCount} lane(s) numbered 0..{laneCount - 1}";
        }

        foreach (var side in new[] { "west", "east" })
        {
            var owner = side == "west" ? MatchSide.West : MatchSide.East;
            if (!towers.Any(t => t.Kind == TowerKind.Keep && t.Owner == owner))
                return $"fow_keep_{side} is missing";

            for (var lane = 0; lane < laneCount; lane++)
            {
                var orders = towers.Where(t => t.Kind == TowerKind.Tower && t.Owner == owner && t.Lane == lane)
                    .Select(t => t.Order).Distinct().OrderBy(o => o).ToList();
                if (orders.Count == 0 || orders[0] != 0)
                    return $"tower_{side}_{lane}_0 is missing (fow_tower_{side}_{lane}_0)";
                for (var i = 1; i < orders.Count; i++)
                {
                    if (orders[i] != i)
                        return $"tower_{side}_{lane}_{i} is missing (towers must be numbered from 0 without gaps)";
                }
            }
        }

        return null;
    }

    // A number too long for an int counts as a gap in the numbering, which the checks then report.
    private static int Number(Match match, int group) =>
        int.TryParse(match.Groups[group].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : int.MaxValue;
}
