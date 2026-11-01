using System.Collections.Generic;
using System.Linq;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Features.FordsOfWar.Diagnostics;

/// <summary>
/// The layout the match started on, for the <c>[Layout]</c> line and check 4: the scene, the two base points, how many waypoints each
/// lane kept, which towers were built and which the layout expected but lost, and whether either keep could already be damaged.
/// </summary>
internal sealed record FordsLayoutFacts(string Scene, Vec3f? WestBase, Vec3f? EastBase, IReadOnlyList<int> LaneWaypoints,
    IReadOnlyList<string> BuiltTowerIds, IReadOnlyList<string> MissingTowerIds, bool KeepOpenWest, bool KeepOpenEast)
{
    /// <summary>
    /// Reads the layout after the towers were built. The base points are the ends of the first lane's path; a keep is open when the
    /// view (taken from the match's tower chain) says it is not locked; a tower is missing when the layout named it and it was not built.
    /// </summary>
    internal static FordsLayoutFacts Of(MapLayout layout, IReadOnlyList<string> expectedIds, MatchView view)
    {
        var built = layout.Towers.Select(t => t.Id).ToList();
        var builtSet = new HashSet<string>(built);
        var path = layout.Lanes.Count > 0 ? layout.Lanes[0].Path : null;
        return new FordsLayoutFacts(layout.SceneName, path is { Count: > 0 } ? path[0] : null, path is { Count: > 0 } ? path[path.Count - 1] : null,
            layout.Lanes.Select(l => l.Path.Count).ToList(), built, expectedIds.Where(id => !builtSet.Contains(id)).ToList(),
            view.Keeps.Any(k => k.Owner == MatchSide.West && !k.Locked), view.Keeps.Any(k => k.Owner == MatchSide.East && !k.Locked));
    }

    /// <summary>The body of the <c>[Layout]</c> line.</summary>
    internal string Describe() => "scene=" + Scene + " west=" + FordsLogLine.Point(WestBase) + " east=" + FordsLogLine.Point(EastBase)
        + " waypoints=" + List(LaneWaypoints.Select((n, lane) => lane + ":" + n)) + " built=" + List(BuiltTowerIds)
        + " missing=" + List(MissingTowerIds)
        + " keepOpenAtStart=West:" + Word(KeepOpenWest) + ",East:" + Word(KeepOpenEast);

    private static string List(IEnumerable<string> items)
    {
        var text = string.Join(",", items);
        return text == "" ? "-" : text;
    }

    private static string Word(bool value) => value ? "true" : "false";
}
