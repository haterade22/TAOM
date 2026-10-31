using System.Collections.Generic;
using System.Linq;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Tests.Features.FordsOfWar.Domain;

/// <summary>
/// A hand-written two-lane map along the X axis (West base at x 0, East base at x 1000) so the tower and lane tests
/// read exact numbers. Tower ids: tower_{west|east}_{lane}_{order}, keep_{west|east}.
/// </summary>
internal static class FordsTestLayout
{
    public const string WestOuter0 = "tower_west_0_0";
    public const string WestInner0 = "tower_west_0_1";
    public const string WestOuter1 = "tower_west_1_0";
    public const string WestInner1 = "tower_west_1_1";
    public const string EastOuter0 = "tower_east_0_0";
    public const string EastInner0 = "tower_east_0_1";
    public const string EastOuter1 = "tower_east_1_0";
    public const string EastInner1 = "tower_east_1_1";
    public const string WestKeep = "keep_west";
    public const string EastKeep = "keep_east";

    public static MapLayout Create()
    {
        var towers = new List<TowerSlot>();
        for (var lane = 0; lane < 2; lane++)
        {
            var y = lane * 50f;
            towers.Add(new TowerSlot($"tower_west_{lane}_0", MatchSide.West, lane, 0, TowerKind.Tower, new Vec3f(360f, y, 0f), 0f));
            towers.Add(new TowerSlot($"tower_west_{lane}_1", MatchSide.West, lane, 1, TowerKind.Tower, new Vec3f(200f, y, 0f), 0f));
            towers.Add(new TowerSlot($"tower_east_{lane}_0", MatchSide.East, lane, 0, TowerKind.Tower, new Vec3f(640f, y, 0f), 3.14159f));
            towers.Add(new TowerSlot($"tower_east_{lane}_1", MatchSide.East, lane, 1, TowerKind.Tower, new Vec3f(800f, y, 0f), 3.14159f));
        }

        towers.Add(new TowerSlot(WestKeep, MatchSide.West, -1, 99, TowerKind.Keep, new Vec3f(40f, 25f, 0f), 0f));
        towers.Add(new TowerSlot(EastKeep, MatchSide.East, -1, 99, TowerKind.Keep, new Vec3f(960f, 25f, 0f), 3.14159f));

        var lanes = Enumerable.Range(0, 2)
            .Select(lane => new LaneLayout(lane, Enumerable.Range(0, 11).Select(i => new Vec3f(i * 100f, lane * 50f, 0f)).ToList()))
            .ToList();

        return new MapLayout("test_scene", lanes, towers, new List<CapturePointSlot>(), new List<CampSlot>());
    }
}
