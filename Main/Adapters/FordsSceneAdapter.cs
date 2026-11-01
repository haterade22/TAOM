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
/// A lane segment is "clear" when the navmesh has a straight line between its ends, or a walk around is at most 1.4 times
/// the straight distance plus 10 m. Main thread only.
/// </summary>
internal sealed class FordsSceneAdapter : IFordsSceneAdapter
{
    private const string PlayArea = "walk_area";
    private const string MarkerPrefix = "fow_";
    private const float AgentRadius = 0.6f;
    private const float PathDetourFactor = 1.4f;
    private const float PathDetourSlack = 10f;

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

    public bool TryGetBasePoints(float insetFraction, bool shortSide, out Vec3f westBase, out Vec3f eastBase)
    {
        westBase = default;
        eastBase = default;
        if (!FordsBaseFinder.TryFind(BoundaryPolygon(), insetFraction, shortSide, out var rawWest, out var rawEast))
            return false;

        westBase = Settle("West", rawWest, rawEast);
        eastBase = Settle("East", rawEast, rawWest);
        return true;
    }

    public FordsLayoutFit Fit(MapLayout layout)
    {
        var scene = _mission.Scene;
        return FordsLayoutFitter.Fit(layout, p => new Vec3f(p.X, p.Y, FordsEngine.GroundAt(scene, p.X, p.Y)), IsWalkable, SegmentClear);
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

    /// <summary>
    /// Snaps a base point to the ground and, when the navmesh is missing there, moves it along the base-to-base axis to the first
    /// ground (<see cref="FordsBaseFinder.SettleAlongAxis"/>), so the axis the layout was generated on survives. Logs the raw and
    /// the settled position on success too.
    /// </summary>
    private Vec3f Settle(string side, Vec3f raw, Vec3f other)
    {
        var scene = _mission.Scene;
        Vec3f Grounded(Vec3f p) => new(p.X, p.Y, FordsEngine.GroundAt(scene, p.X, p.Y));

        var found = FordsBaseFinder.SettleAlongAxis(raw, other, p => IsWalkable(Grounded(p)), out var settled);
        var result = Grounded(settled);
        var axis = raw.Distance2D(other);
        var moved = raw.Distance2D(result);
        var percent = axis > 1e-3f ? moved / axis * 100f : 0f;
        var detail = $"side={side} raw=({F(raw.X)}, {F(raw.Y)}) settled=({F(result.X)}, {F(result.Y)}) moved={F(moved)} m ({F(percent)}% of the axis)";
        if (found)
            _logger.LogInfo("[FordsOfWar][Layout] base " + detail);
        else
            _logger.LogWarning("[FordsOfWar][Layout] base " + detail + "; no navmesh along the axis, keeping the raw point");
        return result;
    }

    private static string F(float value) => value.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>True when the navmesh has a straight line from one point to the other, or a path around that is not much longer.</summary>
    private bool SegmentClear(Vec3f from, Vec3f to)
    {
        var scene = _mission.Scene;
        var start = FordsEngine.ToVec3(from);
        var end = FordsEngine.ToVec3(to);
        var startFace = scene.GetNavigationMeshForPosition(in start);
        var endFace = scene.GetNavigationMeshForPosition(in end);
        if (startFace == UIntPtr.Zero || endFace == UIntPtr.Zero)
            return false;

        if (scene.IsLineToPointClear(startFace, start.AsVec2, end.AsVec2, AgentRadius))
            return true;

        var startPosition = new WorldPosition(scene, startFace, start, false);
        var endPosition = new WorldPosition(scene, endFace, end, false);
        return scene.GetPathDistanceBetweenPositions(ref startPosition, ref endPosition, AgentRadius, out var walk)
            && walk <= from.Distance2D(to) * PathDetourFactor + PathDetourSlack;
    }

    private bool IsWalkable(Vec3f point)
    {
        var position = FordsEngine.ToVec3(point);
        return _mission.Scene.GetNavigationMeshForPosition(in position) != UIntPtr.Zero;
    }
}
