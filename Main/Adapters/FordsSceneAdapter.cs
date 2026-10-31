using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Logging;
using TAOM.Features.FordsOfWar;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Adapters;

/// <summary>
/// The scene of a Fords of War mission (<see cref="IFordsSceneAdapter"/>). The playable polygon is the mission boundary
/// <c>walk_area</c> that <c>MissionBoundaryPlacer.EarlyStart</c> adds from the scene's soft boundary (the first boundary when
/// that name is absent); ground heights come from the scene, and "walkable" means the navmesh has a face under the point.
/// Main thread only.
/// </summary>
internal sealed class FordsSceneAdapter : IFordsSceneAdapter
{
    private const string PlayArea = "walk_area";
    private const string MarkerPrefix = "fow_";
    private const int SettleSteps = 10;

    private readonly Mission _mission;
    private readonly IModLogger _logger;
    private List<Vec3f>? _polygon;

    public FordsSceneAdapter(Mission mission, IModLogger logger)
    {
        _mission = mission;
        _logger = logger;
    }

    public IReadOnlyList<Vec3f> BoundaryPolygon()
    {
        if (_polygon != null)
            return _polygon;

        var points = new List<Vec3f>();
        try
        {
            ICollection<Vec2>? chosen = null;
            foreach (var pair in _mission.Boundaries)
            {
                if (pair.Key == PlayArea)
                {
                    chosen = pair.Value;
                    break;
                }

                chosen ??= pair.Value;
            }

            if (chosen != null)
                points.AddRange(chosen.Select(p => new Vec3f(p.x, p.y, 0f)));
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"[FordsOfWar] could not read the mission boundary ({ex.GetType().Name}: {ex.Message})");
        }

        _polygon = points;
        return points;
    }

    public bool TryGetBasePoints(float insetFraction, out Vec3f westBase, out Vec3f eastBase)
    {
        westBase = default;
        eastBase = default;
        var polygon = BoundaryPolygon();
        if (!FordsBaseFinder.TryFind(polygon, insetFraction, out var west, out var east))
            return false;

        var center = new Vec3f(polygon.Average(p => p.X), polygon.Average(p => p.Y), 0f);
        westBase = Settle(west, center);
        eastBase = Settle(east, center);
        return true;
    }

    public FordsLayoutFit Fit(MapLayout layout)
    {
        var scene = _mission.Scene;
        return FordsLayoutFitter.Fit(layout, p => new Vec3f(p.X, p.Y, FordsEngine.GroundAt(scene, p.X, p.Y)), IsWalkable);
    }

    public bool TryReadTaggedLayout(string sceneName, CampDefaults camp, out MapLayout layout, out string problem)
    {
        layout = null!;
        var markers = new List<FordsMarker>();
        try
        {
            var entities = new List<GameEntity>();
            _mission.Scene.GetEntities(ref entities);
            foreach (var entity in entities)
            {
                var tags = entity.Tags.Where(t => t.StartsWith(MarkerPrefix, StringComparison.Ordinal)).ToList();
                if (tags.Count == 0)
                    continue;

                var frame = entity.GetGlobalFrame();
                foreach (var tag in tags)
                    markers.Add(new FordsMarker(tag, FordsEngine.ToVec3f(frame.origin), FordsEngine.YawOf(frame.rotation.f)));
            }
        }
        catch (Exception ex)
        {
            problem = $"the scene entities could not be read ({ex.GetType().Name}: {ex.Message})";
            return false;
        }

        return FordsTagLayoutReader.TryRead(sceneName, markers, camp, out layout, out problem);
    }

    /// <summary>Snaps a base point to the ground and, when the navmesh is missing there, walks it toward the polygon's center.</summary>
    private Vec3f Settle(Vec3f point, Vec3f center)
    {
        var scene = _mission.Scene;
        for (var step = 0; step <= SettleSteps; step++)
        {
            var candidate = Vec3f.Lerp(point, center, step / (float)SettleSteps);
            var grounded = new Vec3f(candidate.X, candidate.Y, FordsEngine.GroundAt(scene, candidate.X, candidate.Y));
            if (IsWalkable(grounded))
                return grounded;
        }

        _logger.LogWarning($"[FordsOfWar] no navmesh found between a base point ({point.X:F0}, {point.Y:F0}) and the scene center; keeping the point");
        return new Vec3f(point.X, point.Y, FordsEngine.GroundAt(scene, point.X, point.Y));
    }

    private bool IsWalkable(Vec3f point)
    {
        var position = FordsEngine.ToVec3(point);
        return _mission.Scene.GetNavigationMeshForPosition(in position) != UIntPtr.Zero;
    }
}
