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

    /// <summary>Reads the engine's 2 s cached charge query and, whatever it says, every enemy
    /// cavalry formation's velocity against us (<see cref="CavalryThreat"/>, facing-independent);
    /// stamps the signal time and answers whether the square must be held this tick. The scan is
    /// O(enemy formations) over cached positions and velocities, once per active tick.</summary>
    public static bool Braced(Formation formation, ref float lastChargeSignalTime)
    {
        var now = Mission.Current.CurrentTime;
        if (formation.QuerySystem.IsUnderCavalryChargeFromFront || AnyCavalryInbound(formation))
            lastChargeSignalTime = now;
        return BraceDecision.BracedRecently(now, lastChargeSignalTime);
    }

    private static bool AnyCavalryInbound(Formation formation)
    {
        var team = formation.Team;
        var ours = formation.CachedAveragePosition;
        var teams = team.Mission.Teams;
        for (var t = 0; t < teams.Count; t++)
        {
            var other = teams[t];
            if (other == team || !other.IsEnemyOf(team))
                continue;
            var formations = other.FormationsIncludingEmpty;
            for (var i = 0; i < formations.Count; i++)
            {
                var f = formations[i];
                if (f.CountOfUnits <= 0 || !f.QuerySystem.IsCavalryFormation)
                    continue;
                if (CavalryThreat.IsInbound(ours, f.CachedMedianPosition.AsVec2, f.CachedCurrentVelocity))
                    return true;
            }
        }
        return false;
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
