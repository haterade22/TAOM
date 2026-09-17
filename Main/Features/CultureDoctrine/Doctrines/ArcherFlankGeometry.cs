using System.Collections.Generic;
using TAOM.Core.Validation;
using TaleWorlds.Library;

namespace TAOM.Features.CultureDoctrine.Doctrines;

/// <summary>
/// Where the archers stand beside a wall. The players' answer to cavalry and to bows caught in
/// the open (Mike, 2026-09-17): the wall in front, the archers on one flank, level with its
/// rear rank, so the enemy AI goes for the closer wall and the bows keep a clear line; not
/// vanilla's skirmish ahead of the wall and back through it (the fourth A/B lost 116 of 122
/// archers behind the wall). The side is chosen once, where fewer enemy formations stand; the
/// archers fall back straight behind the wall while an enemy formation is closing on them
/// rather than on the wall, and return once it is well away (hysteresis). Pure; NaN holds the
/// current state.
/// </summary>
public static class ArcherFlankGeometry
{
    public enum FlankSide
    {
        Left,
        Right,
    }

    /// <summary>Metres between the wall's end and the archers' near end.</summary>
    public const float Gap = 10f;

    /// <summary>Metres the archers' centre stands behind the wall's centre line.</summary>
    public const float SetBack = 8f;

    /// <summary>An enemy formation inside this distance of the archers, and closer to them than
    /// to the wall, sends them behind the wall; they return beyond <see cref="ReturnFactor"/> times it.</summary>
    public const float FallBackTrigger = 30f;
    public const float ReturnFactor = 1.5f;

    /// <summary>Where they stand while fallen back: straight behind the wall's centre.</summary>
    public const float BehindDistance = 15f;

    public static Vec2 FlankPoint(Vec2 wallCentre, Vec2 facing, float wallWidth, float archersWidth, FlankSide side, float gap, float setBack)
    {
        var right = new Vec2(facing.y, -facing.x);
        var lateral = wallWidth * 0.5f + gap + archersWidth * 0.5f;
        var sign = side == FlankSide.Right ? 1f : -1f;
        return wallCentre + right * (lateral * sign) - facing * setBack;
    }

    public static Vec2 BehindPoint(Vec2 wallCentre, Vec2 facing, float behindDistance) =>
        wallCentre - facing * behindDistance;

    /// <summary>The side with fewer enemy formations across the wall's facing; a tie, and an
    /// enemy dead ahead, go right.</summary>
    public static FlankSide ChooseSide(Vec2 wallCentre, Vec2 facing, IReadOnlyList<Vec2> enemyPositions)
    {
        var right = new Vec2(facing.y, -facing.x);
        var onRight = 0;
        var onLeft = 0;
        for (var i = 0; i < enemyPositions.Count; i++)
        {
            var dot = (enemyPositions[i] - wallCentre).DotProduct(right);
            if (dot > 1e-3f)
                onRight++;
            else if (dot < -1e-3f)
                onLeft++;
        }
        return onLeft < onRight ? FlankSide.Left : FlankSide.Right;
    }

    /// <summary>True while the archers should stand behind the wall. Enter when the nearest
    /// enemy formation is inside <paramref name="trigger"/> of the archers and closer to them
    /// than to the wall; leave once it is beyond <see cref="ReturnFactor"/> times the trigger.</summary>
    public static bool ShouldFallBack(float nearestToArchers, float thatEnemysDistanceToWall, float trigger, bool fallingBack)
    {
        if (!FiniteFloatValidator.IsFinite(nearestToArchers))
            return fallingBack;
        if (fallingBack)
            return !(nearestToArchers > trigger * ReturnFactor);
        return nearestToArchers <= trigger && nearestToArchers < thatEnemysDistanceToWall;
    }
}
