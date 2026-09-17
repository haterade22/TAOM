using TAOM.Features.CultureDoctrine.Doctrines;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.CultureDoctrine.Hooks.Behaviors;

/// <summary>The engine reads the braced walls share: the stance to arrangement map and the
/// cavalry-charge signal with its hold timer.</summary>
internal static class WallStances
{
    public static ArrangementOrder Arrangement(WallStance stance)
    {
        switch (stance)
        {
            case WallStance.Square: return ArrangementOrder.ArrangementOrderSquare;
            case WallStance.ShieldWall: return ArrangementOrder.ArrangementOrderShieldWall;
            case WallStance.Line: return ArrangementOrder.ArrangementOrderLine;
            default: return ArrangementOrder.ArrangementOrderLoose;
        }
    }

    /// <summary>Stamps the signal time while <paramref name="cavalryThreat"/> (from
    /// <see cref="EnemyScan.Pick"/>: a horse formation riding at us inside
    /// <c>CavalryMattersMetres</c>, facing-independent) and answers whether the square must be
    /// held this tick: the signal plus <see cref="BraceDecision.BraceHoldSeconds"/>, so the wall
    /// forms back up once the horse are past the distance. The engine's own
    /// <c>IsUnderCavalryChargeFromFront</c> is not read: it has no distance and one formation.</summary>
    public static bool Braced(bool cavalryThreat, ref float lastChargeSignalTime)
    {
        var now = Mission.Current.CurrentTime;
        if (cavalryThreat)
            lastChargeSignalTime = now;
        return BraceDecision.BracedRecently(now, lastChargeSignalTime);
    }

    /// <summary>The formation's own ground as a world position, the way <c>BehaviorDefend</c>
    /// builds its fallback.</summary>
    public static WorldPosition Here(Formation formation)
    {
        var position = formation.CachedMedianPosition;
        position.SetVec2(formation.CachedAveragePosition);
        return position;
    }

    /// <summary>Squared distance from the formation's average position to a point.</summary>
    public static float DistanceSquaredTo(Formation formation, Vec2 point) =>
        formation.CachedAveragePosition.DistanceSquared(point);
}
