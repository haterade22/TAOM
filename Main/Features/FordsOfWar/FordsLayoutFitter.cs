using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Features.FordsOfWar;

/// <summary>
/// The result of fitting a layout to a scene: the fitted layout, how many points were moved to a walkable spot, how many were
/// dropped, and how many protected points (keeps, lane ends) stayed on an unwalkable spot because nothing nearby was walkable.
/// </summary>
internal readonly record struct FordsLayoutFit(MapLayout Layout, int Moved, int Dropped, int Unfixed);

/// <summary>
/// Puts every point of a layout on the ground and on the navmesh. A point whose ground spot is not walkable steps toward the
/// base-to-base center line (eight steps) and keeps the first walkable one; if none is, it is dropped. Keeps and the two ends
/// of a lane are never dropped (a match cannot run without them): they stay on the ground at their own spot and are counted
/// as unfixed. Pure: the scene adapter supplies the ground snap and the navmesh test as delegates.
/// </summary>
internal static class FordsLayoutFitter
{
    private const int Steps = 8;

    public static FordsLayoutFit Fit(MapLayout layout, Func<Vec3f, Vec3f> snap, Func<Vec3f, bool> walkable)
    {
        var fitter = new Fitter(layout, snap, walkable);
        return fitter.Run();
    }

    private sealed class Fitter
    {
        private readonly MapLayout _layout;
        private readonly Func<Vec3f, Vec3f> _snap;
        private readonly Func<Vec3f, bool> _walkable;
        private readonly (Vec3f From, Vec3f To)? _line;
        private int _moved;
        private int _dropped;
        private int _unfixed;

        public Fitter(MapLayout layout, Func<Vec3f, Vec3f> snap, Func<Vec3f, bool> walkable)
        {
            _layout = layout;
            _snap = snap;
            _walkable = walkable;
            _line = CenterLine(layout);
        }

        public FordsLayoutFit Run()
        {
            var lanes = _layout.Lanes.Select(FitLane).ToList();
            var towers = new List<TowerSlot>();
            foreach (var tower in _layout.Towers)
            {
                var spot = Place(tower.Position, tower.Kind == TowerKind.Keep);
                if (spot is { } placed)
                    towers.Add(tower with { Position = placed });
            }

            var points = new List<CapturePointSlot>();
            foreach (var point in _layout.CapturePoints)
            {
                if (Place(point.Position, false) is { } placed)
                    points.Add(point with { Position = placed });
            }

            var camps = new List<CampSlot>();
            foreach (var camp in _layout.Camps)
            {
                if (Place(camp.Position, false) is { } placed)
                    camps.Add(camp with { Position = placed });
            }

            return new FordsLayoutFit(_layout with { Lanes = lanes, Towers = towers, CapturePoints = points, Camps = camps },
                _moved, _dropped, _unfixed);
        }

        private LaneLayout FitLane(LaneLayout lane)
        {
            var path = new List<Vec3f>();
            for (var i = 0; i < lane.Path.Count; i++)
            {
                var isEnd = i == 0 || i == lane.Path.Count - 1;
                if (Place(lane.Path[i], isEnd) is { } spot)
                    path.Add(spot);
            }

            return lane with { Path = path };
        }

        private Vec3f? Place(Vec3f point, bool isProtected)
        {
            var ground = _snap(point);
            if (_walkable(ground))
                return ground;

            if (_line is { } line)
            {
                var target = Nearest(line.From, line.To, point);
                for (var step = 1; step <= Steps; step++)
                {
                    var candidate = _snap(Vec3f.Lerp(point, target, step / (float)Steps));
                    if (_walkable(candidate))
                    {
                        _moved++;
                        return candidate;
                    }
                }
            }

            if (isProtected)
            {
                _unfixed++;
                return ground;
            }

            _dropped++;
            return null;
        }

        private static (Vec3f From, Vec3f To)? CenterLine(MapLayout layout)
        {
            var path = layout.Lanes.Select(l => l.Path).FirstOrDefault(p => p.Count >= 2);
            if (path != null)
                return (path[0], path[path.Count - 1]);

            var west = layout.Towers.FirstOrDefault(t => t.Kind == TowerKind.Keep && t.Owner == MatchSide.West);
            var east = layout.Towers.FirstOrDefault(t => t.Kind == TowerKind.Keep && t.Owner == MatchSide.East);
            return west != null && east != null ? (west.Position, east.Position) : null;
        }

        private static Vec3f Nearest(Vec3f from, Vec3f to, Vec3f point)
        {
            var dx = to.X - from.X;
            var dy = to.Y - from.Y;
            var lengthSquared = dx * dx + dy * dy;
            if (lengthSquared < 1e-6f)
                return from;

            var t = ((point.X - from.X) * dx + (point.Y - from.Y) * dy) / lengthSquared;
            t = Math.Max(0f, Math.Min(1f, t));
            return new Vec3f(from.X + dx * t, from.Y + dy * t, point.Z);
        }
    }
}
