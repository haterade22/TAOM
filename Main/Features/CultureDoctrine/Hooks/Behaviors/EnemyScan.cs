using TAOM.Features.CultureDoctrine.Doctrines;
using TAOM.Features.CultureDoctrine.Domain;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.CultureDoctrine.Hooks.Behaviors;

/// <summary>
/// The engine side of <see cref="TargetSelection"/>: one walk over every enemy team's
/// <c>FormationsIncludingSpecialAndEmpty</c> (all ten slots, the general's and the bodyguard's
/// included, which is the list vanilla's own <c>CacheClosestEnemyFormation</c> walks,
/// `Formation.cs:1496-1519`; a lord's mounted escort is a real formation in a field battle),
/// reading cached positions, velocities and the 5 s class flags of each
/// <c>FormationQuerySystem</c> (exactly one of the four is true for a formation with units,
/// `FormationQuerySystem.cs:440-445`). A horse formation is a threat when it is melee cavalry
/// (horse archers wheel at range and never charge; a square under arrows is the wrong stance),
/// of real size (<see cref="CavalryThreat.IsSignificant"/>), and either riding at us
/// (<see cref="CavalryThreat.IsInbound"/>) or already among us (<see cref="CavalryThreat.IsOnUs"/>:
/// a formation in the melee has no velocity). Run from the owning behaviour's tick, so
/// O(enemy formations) per call and no allocation: the selection object is the behaviour's own.
/// </summary>
internal static class EnemyScan
{
    /// <summary>The formation a foot line should go for and whether horse are on it now;
    /// <paramref name="braced"/> widens the threat distance for a wall already in its square.</summary>
    public static Formation? Pick(Formation ours, TargetSelection selection, in EngagementTunables tunables, bool braced, out bool cavalryThreat)
    {
        selection.Reset();
        Formation? chosen = null;
        var ourPosition = ours.CachedAveragePosition;
        var ourCount = ours.CountOfUnits;
        var index = 0;
        var teams = ours.Team.Mission.Teams;
        for (var t = 0; t < teams.Count; t++)
        {
            var other = teams[t];
            if (other == ours.Team || !other.IsEnemyOf(ours.Team))
                continue;
            var formations = other.FormationsIncludingSpecialAndEmpty;
            for (var i = 0; i < formations.Count; i++, index++)
            {
                var f = formations[i];
                if (f.CountOfUnits <= 0)
                    continue;
                var q = f.QuerySystem;
                var theirPosition = f.CachedMedianPosition.AsVec2;
                var distance = ourPosition.Distance(theirPosition);
                var melee = q.IsCavalryFormation;
                var horse = melee || q.IsRangedCavalryFormation;
                var threat = melee && CavalryThreat.IsSignificant(f.CountOfUnits, ourCount)
                    && (CavalryThreat.IsOnUs(distance) || CavalryThreat.IsInbound(ourPosition, theirPosition, f.CachedCurrentVelocity));
                selection.Consider(index, horse ? TargetClass.Horse : q.IsRangedFormation ? TargetClass.Archers : TargetClass.Infantry, distance, threat);
                // The target only ever moves to the candidate just considered, so the last one
                // that took it is the winner: one walk, no index lookup.
                if (selection.Target == index)
                    chosen = f;
            }
        }
        cavalryThreat = selection.CavalryThreat(in tunables, braced);
        return chosen;
    }

    /// <summary>Distance to the nearest enemy infantry or archer formation; infinity when
    /// none stands. Horse are left out on purpose: they do not end a march, the wall squares
    /// up against them where it is.</summary>
    public static float ClosestFootDistance(Formation ours)
    {
        var best = float.PositiveInfinity;
        var ourPosition = ours.CachedAveragePosition;
        var teams = ours.Team.Mission.Teams;
        for (var t = 0; t < teams.Count; t++)
        {
            var other = teams[t];
            if (other == ours.Team || !other.IsEnemyOf(ours.Team))
                continue;
            var formations = other.FormationsIncludingSpecialAndEmpty;
            for (var i = 0; i < formations.Count; i++)
            {
                var f = formations[i];
                if (f.CountOfUnits <= 0)
                    continue;
                var q = f.QuerySystem;
                if (!q.IsInfantryFormation && !q.IsRangedFormation)
                    continue;
                var distance = ourPosition.Distance(f.CachedMedianPosition.AsVec2);
                if (distance < best)
                    best = distance;
            }
        }
        return best;
    }
}
