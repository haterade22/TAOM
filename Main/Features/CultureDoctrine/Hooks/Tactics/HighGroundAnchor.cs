using System.Collections.Generic;
using TAOM.Features.CultureDoctrine.Doctrines;
using TAOM.Features.CultureDoctrine.Domain;
using TAOM.Features.CultureDoctrine.Hooks.Behaviors;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.CultureDoctrine.Hooks.Tactics;

/// <summary>
/// Where a position-holding tactic (the Dwarven wall, the Elven ring) stands, decided the way a
/// captain would: march to the navmesh high ground only if the foot can get there and form up
/// before the enemy's foot arrives (<see cref="HighGroundRace"/>, cavalry not a racer), and only
/// when no enemy foot is already on us (<see cref="HighGroundRace.WorthGoing"/>,
/// <see cref="EngagementTunables"/>), otherwise form where it stands. The ground itself is the
/// best slope toward the enemy INSIDE the cap (<see cref="HighGroundOf"/>): the engine's own
/// query searches a square scaled to half the distance to the enemy, which at deployment range
/// names a hill 150 m away and never the knoll 30 m off; horse do not end a march, the wall
/// squares up against them where it is. While marching the race is re-checked once a second and a lost race
/// turns into a hold on the spot; once arrived, or once the closest enemy is inside
/// <c>BehaviorHoldHighGround</c>'s lock radius (<c>max(0.8 * archers' missile range, 30 m)</c>,
/// `BehaviorHoldHighGround.cs:35-46`), the position is locked for good. One instance per tactic,
/// touched only from that tactic's tick (the team-AI tick); the ETA list is reused, so a tick
/// allocates nothing.
/// </summary>
public sealed class HighGroundAnchor
{
    private enum State
    {
        Undecided,
        Marching,
        Holding,
        Arrived,
    }

    // BehaviorDefend's own "at position" radius (`BehaviorDefend.cs:50`, 100 m squared).
    private const float ArrivedDistance = 10f;
    private const float MinLockRadius = 30f;

    private readonly List<float> _enemyEtas = new List<float>(8);
    private State _state;
    private Formation? _for;
    private WorldPosition _position = WorldPosition.Invalid;

    public string Status => _state.ToString();

    /// <summary>The position to defend, decided or re-checked at a phase apply.
    /// <paramref name="anchor"/> is the formation whose high ground is meant (the archers for a
    /// ring, the infantry for a wall); <paramref name="infantry"/> is the formation that must
    /// get there.</summary>
    public WorldPosition Resolve(Formation infantry, Formation anchor, Formation? archers, Team team, in RaceTunables race, in EngagementTunables engagement)
    {
        if (_for != infantry)
        {
            _for = infantry;
            _state = State.Undecided;
        }
        switch (_state)
        {
            case State.Undecided:
                Decide(infantry, HighGroundOf(anchor, in engagement), team, in race, in engagement);
                break;
            case State.Marching:
                // Still tracking the high ground while the enemy is far, as BehaviorHoldHighGround does.
                if (EnemyBeyondLockRadius(infantry, archers))
                    Decide(infantry, HighGroundOf(anchor, in engagement), team, in race, in engagement);
                break;
        }
        return _position;
    }

    /// <summary>Once a second while marching: true when the race is now lost and the plan must
    /// re-apply with the hold position this returns through <see cref="Resolve"/>.</summary>
    public bool Tick(Formation infantry, Team team, in RaceTunables race, in EngagementTunables engagement)
    {
        if (_state != State.Marching || _for != infantry)
            return false;
        if (infantry.CachedAveragePosition.DistanceSquared(_position.AsVec2) < ArrivedDistance * ArrivedDistance)
        {
            _state = State.Arrived;
            return false;
        }
        if (Worth(infantry, _position.AsVec2, in engagement) && RaceWinnable(infantry, _position.AsVec2, team, in race))
            return false;
        _state = State.Holding;
        _position = Here(infantry);
        return true;
    }

    private void Decide(Formation infantry, WorldPosition ground, Team team, in RaceTunables race, in EngagementTunables engagement)
    {
        if (Worth(infantry, ground.AsVec2, in engagement) && RaceWinnable(infantry, ground.AsVec2, team, in race))
        {
            _state = State.Marching;
            _position = ground;
        }
        else
        {
            _state = State.Holding;
            _position = Here(infantry);
        }
    }

    // The ground is inside the cap and no enemy foot is on us yet.
    private static bool Worth(Formation infantry, Vec2 target, in EngagementTunables engagement) =>
        HighGroundRace.WorthGoing(infantry.CachedAveragePosition.Distance(target), EnemyScan.ClosestFootDistance(infantry), in engagement);

    // Every enemy foot and archer formation's time to the target against ours plus form-up.
    private bool RaceWinnable(Formation infantry, Vec2 target, Team team, in RaceTunables race)
    {
        _enemyEtas.Clear();
        var teams = team.Mission.Teams;
        for (var t = 0; t < teams.Count; t++)
        {
            var other = teams[t];
            if (other == team || !other.IsEnemyOf(team))
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
                _enemyEtas.Add(HighGroundRace.Eta(f.CachedMedianPosition.AsVec2.Distance(target), q.MovementSpeedMaximum));
            }
        }
        var ourDistance = infantry.CachedAveragePosition.Distance(target);
        return HighGroundRace.Decide(ourDistance, infantry.QuerySystem.MovementSpeedMaximum, infantry.CountOfUnits, in race, _enemyEtas);
    }

    private static bool EnemyBeyondLockRadius(Formation infantry, Formation? archers)
    {
        var enemy = infantry.CachedClosestEnemyFormation;
        if (enemy == null)
            return true;
        var radius = MathF.Max(archers != null ? archers.QuerySystem.MissileRangeAdjusted * 0.8f : 0f, MinLockRadius);
        return infantry.CachedAveragePosition.DistanceSquared(enemy.Formation.CachedMedianPosition.AsVec2) > radius * radius;
    }

    /// <summary>The best slope toward the enemy inside the cap, as a world position: the
    /// engine's own search (<c>Mission.FindPositionWithBiggestSlopeTowardsDirectionInSquare</c>,
    /// what <c>HighGroundCloseToForeseenBattleGround</c> calls, `FormationQuerySystem.cs:637-643`)
    /// on a square whose half-side is the cap over root two, so every candidate is inside it.
    /// With no enemy in view the formation's own ground. Native, called at a phase apply only.</summary>
    public static WorldPosition HighGroundOf(Formation formation, in EngagementTunables engagement)
    {
        var center = formation.CachedMedianPosition;
        center.SetVec2(formation.CachedAveragePosition);
        var reference = formation.QuerySystem.Team.MedianTargetFormationPosition;
        if (!reference.IsValid)
            return center;
        return formation.Team.Mission.FindPositionWithBiggestSlopeTowardsDirectionInSquare(ref center, engagement.HighGroundMaxMetres * 0.7071f, ref reference);
    }

    private static WorldPosition Here(Formation formation)
    {
        var position = formation.CachedMedianPosition;
        position.SetVec2(formation.CachedAveragePosition);
        return position;
    }
}
