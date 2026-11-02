using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Features.FordsOfWar;

/// <summary>
/// Puts every point of a layout on the ground and on the navmesh, and reports what it had to change
/// (<see cref="FordsLayoutFit"/>). A point on a bad spot is searched for in rings of 2, 4, 8 and 12 m at eight bearings,
/// perpendicular to the lane first. A lane waypoint must also stay on its own side of the neighbouring lanes and at least half
/// of its original gap away from them, so lanes never collapse onto each other. A tower slides along its own lane
/// (10, 20 then 30 m either way, never past a
/// neighbour of its own side and lane: the chain locks by <c>Order</c>, so the outer tower stays at least 10 m outward of the inner one)
/// and keeps its id; an authored tower (the scene's own entity) stays where it stands, counted as neither moved nor dropped. Keeps and the two ends of a lane are never dropped (a match cannot run
/// without them): they stay on the ground at their own spot and are counted as unfixed. A tower that finds no spot is dropped,
/// unless it was its side's last one in that lane: then the fit is rejected, because the enemy would start with an open keep.
/// Pure: the scene adapter supplies the ground snap, the navmesh test and the optional segment test as delegates.
/// </summary>
internal static class FordsLayoutFitter
{
    private static readonly float[] Rings = { 2f, 4f, 8f, 12f };

    private static readonly float[] Bearings = { 90f, 270f, 45f, 315f, 135f, 225f, 0f, 180f };

    private static readonly float[] SlideArcs = { 10f, 20f, 30f };

    /// <summary>The least a slid tower keeps between itself and each other tower of its own side and lane, measured outward along the lane.</summary>
    private const float SlideSeparation = 10f;

    public static FordsLayoutFit Fit(MapLayout layout, Func<Vec3f, Vec3f> snap, Func<Vec3f, bool> walkable,
        Func<Vec3f, Vec3f, bool>? segmentClear = null)
    {
        var fitter = new Fitter(layout, snap, walkable, segmentClear);
        return fitter.Run();
    }

    /// <summary>
    /// Names every (owner, lane) group of towers (keeps excluded) in <paramref name="wanted"/> that has no member left in
    /// <paramref name="kept"/>, with <paramref name="cause"/> after the lost ids; an empty string when no group lost all its members.
    /// The one rule behind a rejected fit and a refused start: a side without a tower in a lane leaves its keep open to that lane.
    /// </summary>
    internal static string EmptiedGroups(IEnumerable<TowerSlot> wanted, IEnumerable<TowerSlot> kept, string cause)
    {
        var keptIds = new HashSet<string>(kept.Select(t => t.Id));
        var messages = new List<string>();
        foreach (var group in wanted.Where(t => t.Kind == TowerKind.Tower).GroupBy(t => (t.Owner, t.Lane)))
        {
            if (group.Any(t => keptIds.Contains(t.Id)))
                continue;

            var ids = string.Join(", ", group.OrderBy(t => t.Order).Select(t => t.Id));
            messages.Add($"lane {group.Key.Lane} has no tower left for {group.Key.Owner} ({ids} {cause})");
        }

        return string.Join("; ", messages);
    }

    private readonly record struct LaneProbe(float Distance, int Side);

    private sealed class Fitter
    {
        private readonly MapLayout _layout;
        private readonly Func<Vec3f, Vec3f> _snap;
        private readonly Func<Vec3f, bool> _walkable;
        private readonly Func<Vec3f, Vec3f, bool>? _segmentClear;
        private readonly Vec3f _axis;
        private readonly List<LaneLayout> _fittedLanes = new();
        private readonly List<FordsLaneFitStats> _stats = new();
        private readonly List<string> _droppedTowers = new();
        private readonly List<FordsTowerMove> _movedTowers = new();
        private readonly Dictionary<string, Vec3f?> _spots = new();
        private int _moved;
        private int _dropped;
        private int _unfixed;

        public Fitter(MapLayout layout, Func<Vec3f, Vec3f> snap, Func<Vec3f, bool> walkable, Func<Vec3f, Vec3f, bool>? segmentClear)
        {
            _layout = layout;
            _snap = snap;
            _walkable = walkable;
            _segmentClear = segmentClear;
            _axis = AxisOf(layout);
        }

        public FordsLayoutFit Run()
        {
            for (var i = 0; i < _layout.Lanes.Count; i++)
                _fittedLanes.Add(FitLane(i));

            var towers = FitTowers(out var reason);
            var points = new List<CapturePointSlot>();
            foreach (var point in _layout.CapturePoints)
            {
                if (Free(point.Position, out var spot))
                    points.Add(point with { Position = spot });
                else
                    _dropped++;
            }

            var camps = new List<CampSlot>();
            foreach (var camp in _layout.Camps)
            {
                if (Free(camp.Position, out var spot))
                    camps.Add(camp with { Position = spot });
                else
                    _dropped++;
            }

            return new FordsLayoutFit(_layout with { Lanes = _fittedLanes, Towers = towers, CapturePoints = points, Camps = camps },
                _moved, _dropped, _unfixed)
            {
                Lanes = _stats,
                DroppedTowers = _droppedTowers,
                MovedTowers = _movedTowers,
                Rejected = reason.Length > 0,
                Reason = reason,
            };
        }

        /// <summary>Places a point that belongs to no lane (rings oriented along the base-to-base axis); false, with the ground spot, when nothing nearby is walkable.</summary>
        private bool Free(Vec3f position, out Vec3f spot)
        {
            if (!TryPlace(position, _axis, null, out spot, out var moved))
                return false;

            _moved += moved ? 1 : 0;
            return true;
        }

        private LaneLayout FitLane(int laneIndex)
        {
            var lane = _layout.Lanes[laneIndex];
            var original = lane.Path;
            var path = new List<Vec3f>();
            var moved = 0;
            var dropped = 0;
            for (var i = 0; i < original.Count; i++)
            {
                var isEnd = i == 0 || i == original.Count - 1;
                var accept = KeepsToItsSide(laneIndex, original[i]);
                if (TryPlace(original[i], DirectionAt(original, i), accept, out var spot, out var wasMoved))
                {
                    path.Add(spot);
                    moved += wasMoved ? 1 : 0;
                }
                else if (isEnd)
                {
                    path.Add(spot);
                    _unfixed++;
                }
                else
                {
                    dropped++;
                }
            }

            _moved += moved;
            _dropped += dropped;
            _stats.Add(Measure(lane.Index, original.Count, path, moved, dropped));
            return lane with { Path = path };
        }

        private FordsLaneFitStats Measure(int lane, int waypoints, IReadOnlyList<Vec3f> kept, int moved, int dropped)
        {
            var maxGap = 0f;
            var bad = 0;
            for (var i = 1; i < kept.Count; i++)
            {
                maxGap = Math.Max(maxGap, kept[i].Distance2D(kept[i - 1]));
                if (_segmentClear != null && !_segmentClear(kept[i - 1], kept[i]))
                    bad++;
            }

            return new FordsLaneFitStats(lane, waypoints, kept.Count, moved, dropped, maxGap, bad);
        }

        private List<TowerSlot> FitTowers(out string reason)
        {
            var placed = new List<(TowerSlot Slot, Vec3f? Spot)>();
            foreach (var tower in _layout.Towers)
            {
                Vec3f? spot = null;
                if (tower.Kind == TowerKind.Keep)
                {
                    _unfixed += Free(tower.Position, out var keepSpot) ? 0 : 1;
                    spot = keepSpot;
                }
                else if (tower.Authored)
                {
                    // A tower the scene built stands in a hole in the ground navmesh by design; sliding it would put a barricade beside it.
                    spot = tower.Position;
                }
                else if (TrySlide(tower, out var slid))
                {
                    spot = slid;
                }

                _spots[tower.Id] = spot;
                placed.Add((tower, spot));
            }

            var towers = new List<TowerSlot>();
            foreach (var (slot, spot) in placed)
            {
                if (spot is { } fitted)
                {
                    towers.Add(slot with { Position = fitted });
                    continue;
                }

                _dropped++;
                _droppedTowers.Add(slot.Id);
            }

            reason = EmptiedGroups(_layout.Towers, towers, "found no walkable spot");
            return towers;
        }

        /// <summary>
        /// The tower's own spot when walkable, else the first walkable spot sliding along its fitted lane that keeps the chain order
        /// (see <see cref="KeepsChainOrder"/>); false when none.
        /// </summary>
        private bool TrySlide(TowerSlot tower, out Vec3f spot)
        {
            spot = _snap(tower.Position);
            if (_walkable(spot))
                return true;

            var lane = _fittedLanes.FirstOrDefault(l => l.Index == tower.Lane);
            if (lane == null || lane.Path.Count < 2)
                return Free(tower.Position, out spot);

            var length = LaneGeometry.PathLength(lane.Path);
            var arc = LaneGeometry.Progress(lane.Path, tower.Position) * length;
            var towardOwnBase = tower.Owner == MatchSide.West ? -1f : 1f;
            foreach (var offset in SlideOffsets(towardOwnBase))
            {
                var candidate = _snap(LaneGeometry.PointAt(lane.Path, Math.Max(0f, Math.Min(length, arc + offset))));
                if (!_walkable(candidate) || !KeepsChainOrder(tower, candidate, lane.Path, length, -towardOwnBase))
                    continue;

                spot = candidate;
                _moved++;
                _movedTowers.Add(new FordsTowerMove(tower.Id, Math.Abs(offset)));
                return true;
            }

            return false;
        }

        /// <summary>
        /// Whether a tower at <paramref name="candidate"/> still lies outward (<paramref name="outward"/> is +1 or -1 along the lane) of
        /// every tower of its side and lane with a higher <c>Order</c>, and inward of every one with a lower, by at least
        /// <see cref="SlideSeparation"/>. A neighbour already fitted counts where it stands, one not yet fitted where the layout put it,
        /// and one that was dropped not at all.
        /// </summary>
        private bool KeepsChainOrder(TowerSlot tower, Vec3f candidate, IReadOnlyList<Vec3f> path, float length, float outward)
        {
            var own = outward * LaneGeometry.Progress(path, candidate) * length;
            foreach (var other in _layout.Towers)
            {
                if (other.Id == tower.Id || other.Kind != TowerKind.Tower || other.Owner != tower.Owner || other.Lane != tower.Lane)
                    continue;

                Vec3f there = other.Position;
                if (_spots.TryGetValue(other.Id, out var fitted))
                {
                    if (fitted is not { } kept)
                        continue;

                    there = kept;
                }

                var theirs = outward * LaneGeometry.Progress(path, there) * length;
                var gap = other.Order > tower.Order ? own - theirs : theirs - own;
                if (gap < SlideSeparation)
                    return false;
            }

            return true;
        }

        /// <summary>The lane itself at the tower's own arc, then 10, 20 and 30 m toward its own base first and toward the enemy second.</summary>
        private static IEnumerable<float> SlideOffsets(float towardOwnBase)
        {
            yield return 0f;
            foreach (var arc in SlideArcs)
            {
                yield return arc * towardOwnBase;
                yield return -arc * towardOwnBase;
            }
        }

        /// <summary>The point itself when walkable, else the first walkable ring spot the optional test also accepts.</summary>
        private bool TryPlace(Vec3f point, Vec3f direction, Func<Vec3f, bool>? accept, out Vec3f spot, out bool moved)
        {
            moved = false;
            spot = _snap(point);
            if (_walkable(spot))
                return true;

            var ground = spot;
            foreach (var ring in Rings)
            {
                foreach (var bearing in Bearings)
                {
                    var radians = bearing * Math.PI / 180d;
                    var cos = (float)Math.Cos(radians);
                    var sin = (float)Math.Sin(radians);
                    var dx = direction.X * cos - direction.Y * sin;
                    var dy = direction.X * sin + direction.Y * cos;
                    var candidate = _snap(new Vec3f(point.X + dx * ring, point.Y + dy * ring, point.Z));
                    if (!_walkable(candidate) || (accept != null && !accept(candidate)))
                        continue;

                    spot = candidate;
                    moved = true;
                    return true;
                }
            }

            spot = ground;
            return false;
        }

        /// <summary>
        /// Accepts a candidate that is on the same side of each neighbouring lane as the original point and at least half as far
        /// from it; null for a lane with no neighbour. The neighbours are the unfitted lanes, so the order of fitting does not matter.
        /// </summary>
        private Func<Vec3f, bool>? KeepsToItsSide(int laneIndex, Vec3f original)
        {
            var neighbours = new List<(IReadOnlyList<Vec3f> Path, LaneProbe Origin)>();
            foreach (var other in new[] { laneIndex - 1, laneIndex + 1 })
            {
                if (other < 0 || other >= _layout.Lanes.Count)
                    continue;

                var path = _layout.Lanes[other].Path;
                neighbours.Add((path, Probe(path, original)));
            }

            if (neighbours.Count == 0)
                return null;

            return candidate => neighbours.All(n =>
            {
                var now = Probe(n.Path, candidate);
                return now.Distance >= n.Origin.Distance / 2f && (n.Origin.Side == 0 || now.Side == n.Origin.Side);
            });
        }

        private static LaneProbe Probe(IReadOnlyList<Vec3f> path, Vec3f point)
        {
            if (path.Count == 0)
                return new LaneProbe(float.MaxValue, 0);
            if (path.Count == 1)
                return new LaneProbe(point.Distance2D(path[0]), 0);

            var best = float.MaxValue;
            var side = 0;
            for (var i = 1; i < path.Count; i++)
            {
                var a = path[i - 1];
                var b = path[i];
                var dx = b.X - a.X;
                var dy = b.Y - a.Y;
                var lengthSquared = dx * dx + dy * dy;
                var t = lengthSquared < 1e-6f ? 0f : Math.Max(0f, Math.Min(1f, ((point.X - a.X) * dx + (point.Y - a.Y) * dy) / lengthSquared));
                var distance = point.Distance2D(new Vec3f(a.X + dx * t, a.Y + dy * t, 0f));
                if (distance >= best)
                    continue;

                best = distance;
                var cross = dx * (point.Y - a.Y) - dy * (point.X - a.X);
                side = cross > 1e-3f ? 1 : cross < -1e-3f ? -1 : 0;
            }

            return new LaneProbe(best, side);
        }

        private Vec3f DirectionAt(IReadOnlyList<Vec3f> path, int index)
        {
            var from = path[Math.Max(0, index - 1)];
            var to = path[Math.Min(path.Count - 1, index + 1)];
            return Unit(to.X - from.X, to.Y - from.Y) ?? _axis;
        }

        private static Vec3f AxisOf(MapLayout layout)
        {
            var path = layout.Lanes.Select(l => l.Path).FirstOrDefault(p => p.Count >= 2);
            if (path != null)
                return Unit(path[path.Count - 1].X - path[0].X, path[path.Count - 1].Y - path[0].Y) ?? new Vec3f(1f, 0f, 0f);

            var west = layout.Towers.FirstOrDefault(t => t.Kind == TowerKind.Keep && t.Owner == MatchSide.West);
            var east = layout.Towers.FirstOrDefault(t => t.Kind == TowerKind.Keep && t.Owner == MatchSide.East);
            if (west != null && east != null)
                return Unit(east.Position.X - west.Position.X, east.Position.Y - west.Position.Y) ?? new Vec3f(1f, 0f, 0f);

            return new Vec3f(1f, 0f, 0f);
        }

        private static Vec3f? Unit(float x, float y)
        {
            var length = (float)Math.Sqrt(x * x + y * y);
            return length < 1e-4f ? null : new Vec3f(x / length, y / length, 0f);
        }
    }
}
