using System;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Features.FordsOfWar;

internal readonly record struct SpawnSpot(Vec3f Position, Vec3f Facing);

/// <summary>
/// Where the n-th unit of a wave appears: at its side's end of the lane, in a block eight wide so a pack does not
/// stack on one point, facing down the lane toward the enemy. Rows run forward from the base, away from the scene edge.
/// </summary>
internal static class FordsSpawnPlacement
{
    private const int Columns = 8;
    private const int Rows = 6;
    private const float Spacing = 1.4f;
    private const float FirstRowOffset = 2f;

    public static SpawnSpot Place(LaneLayout lane, MatchSide side, int rank)
    {
        var path = lane.Path;
        if (path.Count == 0)
            return new SpawnSpot(default, new Vec3f(side == MatchSide.West ? 1f : -1f, 0f, 0f));

        var west = side == MatchSide.West;
        var baseIndex = west ? 0 : path.Count - 1;
        var aheadIndex = west ? Math.Min(2, path.Count - 1) : Math.Max(path.Count - 3, 0);
        var origin = path[baseIndex];
        var facing = Direction(origin, path[aheadIndex], west);

        var slot = Math.Max(0, rank) % (Columns * Rows);
        var column = slot % Columns;
        var row = slot / Columns;
        var across = (column - (Columns - 1) / 2f) * Spacing;
        var forward = FirstRowOffset + row * Spacing;
        var side2 = new Vec3f(-facing.Y, facing.X, 0f);
        return new SpawnSpot(origin + facing * forward + side2 * across, facing);
    }

    private static Vec3f Direction(Vec3f from, Vec3f to, bool west)
    {
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        var length = (float)Math.Sqrt(dx * dx + dy * dy);
        return length < 1e-3f ? new Vec3f(west ? 1f : -1f, 0f, 0f) : new Vec3f(dx / length, dy / length, 0f);
    }
}
