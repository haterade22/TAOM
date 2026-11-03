using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>A place a lane can seize: a camp with units left, or a capture point its side does not own.</summary>
internal sealed record SeizeTarget(string Id, Vec3f Position, bool IsCamp);

/// <summary>
/// What each lane may seize and which objective each seizing lane is on. A lane only goes for an objective when it is one of the
/// two lanes nearest to it: lane bars keep a lane off its neighbour's ground, so it cannot walk across a lane to a far objective.
/// The objective is chosen when the order is given and stays until it is held, so a lane does not turn round on its way there.
/// </summary>
internal sealed class SeizeRules
{
    private const int LanesPerObjective = 2;

    private readonly MapLayout _layout;
    private readonly CampRules _camps;
    private readonly CapturePointRules _capture;
    private readonly List<(SeizeTarget Target, HashSet<int> Lanes)> _objectives = new();
    private readonly Dictionary<(MatchSide, int), SeizeTarget> _seizing = new();

    public SeizeRules(MapLayout layout, CampRules camps, CapturePointRules capture)
    {
        _layout = layout;
        _camps = camps;
        _capture = capture;
        foreach (var camp in layout.Camps)
            Add(new SeizeTarget(camp.Id, camp.Position, true));
        foreach (var point in layout.CapturePoints)
            Add(new SeizeTarget(point.Id, point.Position, false));
    }

    /// <summary>Whether the lane has an open objective within reach.</summary>
    public bool HasOpen(MatchSide side, int lane) => _objectives.Any(o => o.Lanes.Contains(lane) && IsOpen(o.Target, side));

    /// <summary>Puts the lane on the open objective nearest to <paramref name="from"/> (its own base when null). No-op when it is already on one.</summary>
    public void Start(MatchSide side, int lane, Vec3f? from)
    {
        if (_seizing.ContainsKey((side, lane)))
            return;

        var point = from ?? BaseOf(side, lane);
        var nearest = _objectives
            .Where(o => o.Lanes.Contains(lane) && IsOpen(o.Target, side))
            .OrderBy(o => point.Distance2D(o.Target.Position))
            .Select(o => o.Target)
            .FirstOrDefault();
        if (nearest != null)
            _seizing[(side, lane)] = nearest;
    }

    public void Stop(MatchSide side, int lane) => _seizing.Remove((side, lane));

    public SeizeTarget? TargetOf(MatchSide side, int lane) => _seizing.TryGetValue((side, lane), out var target) ? target : null;

    /// <summary>The lanes whose objective is no longer open (held, or the camp cleared). They are off their objective after this call.</summary>
    public IReadOnlyList<(MatchSide Side, int Lane)> TakeFinished()
    {
        var finished = _seizing.Where(e => !IsOpen(e.Value, e.Key.Item1)).Select(e => e.Key).ToList();
        foreach (var key in finished)
            _seizing.Remove(key);

        return finished;
    }

    private void Add(SeizeTarget target)
    {
        var lanes = _layout.Lanes
            .Select(l => (l.Index, Distance: DistanceToPath(l, target.Position)))
            .Where(l => l.Distance < float.MaxValue)
            .OrderBy(l => l.Distance)
            .ThenBy(l => l.Index)
            .Take(LanesPerObjective)
            .Select(l => l.Index);
        _objectives.Add((target, new HashSet<int>(lanes)));
    }

    private bool IsOpen(SeizeTarget target, MatchSide side) =>
        target.IsCamp ? _camps.IsOpen(target.Id) : _capture.Get(target.Id)?.Owner != side;

    private Vec3f BaseOf(MatchSide side, int lane)
    {
        var path = _layout.Lanes.First(l => l.Index == lane).Path;
        return side == MatchSide.West ? path[0] : path[path.Count - 1];
    }

    private static float DistanceToPath(LaneLayout lane, Vec3f point)
    {
        var index = LaneGeometry.NearestIndex(lane.Path, point);
        return index < 0 ? float.MaxValue : lane.Path[index].Distance2D(point);
    }
}
