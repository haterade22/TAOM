using System;
using TAOM.Features.CultureDoctrine.Doctrines;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.CultureDoctrine.Hooks.Tactics;

/// <summary>
/// Boundary conversion from a team's engine query systems to the pure
/// <see cref="TeamQuerySnapshot"/>. Side-effect free, unlike vanilla's
/// <c>CheckAndDetermineFormation</c>: a weight query runs for every registered tactic every 5 s,
/// current or not, and must not reassign a tactic's formation slots. Team-AI tick.
/// </summary>
public static class TeamQuerySnapshotFactory
{
    public static TeamQuerySnapshot Take(Team team, MBList<Formation> formations, float notEngagingAdvantage)
    {
        var q = team.QuerySystem;
        var infantry = Largest(formations, f => f.QuerySystem.IsInfantryFormation, out var infantryTotal);
        var archers = Largest(formations, f => f.QuerySystem.IsRangedFormation, out _);
        var cavalry = Largest(formations, f => f.QuerySystem.IsCavalryFormation, out _);
        var ringFits = infantry != null && archers != null && RingGeometry.Fits(
            infantry.CountOfUnits, infantry.MaximumInterval, infantry.UnitDiameter,
            archers.CountOfUnits, archers.UnitDiameter, archers.Interval);
        return new TeamQuerySnapshot(
            q.MemberCount, q.EnemyUnitCount, q.InfantryRatio, q.RangedRatio, q.CavalryRatio, q.RangedCavalryRatio,
            q.RemainingPowerRatio, notEngagingAdvantage,
            hasInfantry: infantry != null, hasArchers: archers != null, hasCavalry: cavalry != null,
            isDefenseApplicable: team.TeamAI.IsDefenseApplicable, ringFits: ringFits,
            isDefender: team.Side == BattleSideEnum.Defender,
            infantryCount: infantry?.CountOfUnits ?? 0,
            throwingRatio: infantry?.QuerySystem.HasThrowingUnitRatio ?? 0f,
            hasVanguard: FormationSlots.VanguardOf(formations) != null,
            infantryTotal: infantryTotal);
    }

    // The largest formation of the class and the class's total; one pass, no allocation.
    private static Formation? Largest(MBList<Formation> formations, Func<Formation, bool> isClass, out int total)
    {
        Formation? best = null;
        total = 0;
        for (var i = 0; i < formations.Count; i++)
        {
            var f = formations[i];
            if (f.CountOfUnits <= 0 || !isClass(f))
                continue;
            total += f.CountOfUnits;
            if (best == null || f.CountOfUnits > best.CountOfUnits)
                best = f;
        }
        return best;
    }
}
