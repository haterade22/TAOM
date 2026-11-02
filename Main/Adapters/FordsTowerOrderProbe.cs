using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TAOM.Features.FordsOfWar.Diagnostics;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Adapters;

/// <summary>
/// The log text for the first attack order a stream gives at a real tower: where the order's target point lies and whether the
/// navmesh holds a face under it (a real tower stands in a hole of the ground navmesh, so "none" is the expected answer and a path
/// from the formation to it is the open question). Main thread.
/// </summary>
internal static class FordsTowerOrderProbe
{
    public static string Describe(Scene scene, GameEntity tower, Formation formation, string towerId, MatchSide attacker, int lane)
    {
        var origin = tower.GetGlobalFrame().origin;
        var there = PathFaceRecord.NullFaceRecord;
        scene.GetNavMeshFaceIndex(ref there, origin, false);
        var here = PathFaceRecord.NullFaceRecord;
        scene.GetNavMeshFaceIndex(ref here, formation.CurrentPosition, false, false, true);
        var pathExists = there.IsValid() && here.IsValid() && scene.DoesPathExistBetweenFaces(here.FaceIndex, there.FaceIndex, false);
        return FordsTowerLines.Order(towerId, attacker, lane, FordsEngine.ToVec3f(origin), there.IsValid() ? there.FaceGroupIndex : -1, pathExists);
    }
}
