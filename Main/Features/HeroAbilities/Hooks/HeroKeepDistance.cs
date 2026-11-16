using System;
using System.Collections.Generic;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.HeroAbilities.Domain;
using TAOM.Features.HeroAbilities.Ranged;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.HeroAbilities.Hooks;

/// <summary>
/// The ranged hero steps away from a melee threat (#1415, docs/features/hero-abilities.md). Four times a second, for each
/// listed hero whose kit has keepDistanceMeters and stepBackMeters: an enemy human on his level within keepDistanceMeters is a
/// threat; KeepDistanceRule picks the direction and the points; the first usable point gets the engine's timed scripted move
/// (HumanAIComponent.SetScriptedPositionAndDirectionTimed, which HumanAIComponent.OnTick releases when the timer ends). At the
/// end of the hold the step counts as moved when his body covered half of it. With no usable point he stays and fights with
/// what the engine draws. Main thread only (OnMissionTick). Boundary code, game-tested (ADR-008).
/// </summary>
internal sealed class HeroKeepDistance
{
    private readonly Dictionary<Agent, float> _nextAllowed = new Dictionary<Agent, float>();
    private readonly Dictionary<Agent, Step> _walking = new Dictionary<Agent, Step>();
    private readonly List<Agent> _done = new List<Agent>();
    private readonly MBList<Agent> _near = new MBList<Agent>();
    private readonly List<(float X, float Y)> _threats = new List<(float X, float Y)>();
    private float _nextTick;

    internal void Tick(Mission mission, HeroRangedRoster<Agent> roster, HeroRangedTallies<Agent> tallies, float now)
    {
        if (!(now >= _nextTick))
            return;
        _nextTick = now + KeepDistanceRule.TickSeconds;
        CheckWalks(now);
        if (roster.IsEmpty)
            return;
        foreach (var hero in roster.Keys)
        {
            var kit = roster.KitOf(hero);
            if (kit == null || !(kit.KeepDistanceMeters > 0f) || !(kit.StepBackMeters > 0f) || !CanStep(hero))
                continue;
            _nextAllowed.TryGetValue(hero, out var next);
            var at = hero.Position;
            var nearest = ReadThreats(mission, hero, at, kit.KeepDistanceMeters);
            if (!KeepDistanceRule.ShouldStep(nearest, kit.KeepDistanceMeters, kit.StepBackMeters, now, next))
                continue;

            _nextAllowed[hero] = now + KeepDistanceRule.CooldownSeconds;
            var tally = tallies.Of(hero, kit);
            if (TryStep(mission, hero, at, kit.StepBackMeters) is { } to)
            {
                tally.Steps++;
                _walking[hero] = new Step(at.x, at.y, to.X, to.Y, now + KeepDistanceRule.HoldSeconds, tally);
            }
            else
            {
                tally.Cornered++;
            }
        }
    }

    // Once a second (HeroRangedMissionLogic.Sample): a second with a melee weapon in his hand, so a run shows that he draws the sword.
    internal static void NoteWeapon(Agent hero, HeroRangedTally tally)
    {
        if (AgentSlotIdentity.IsCurrentOccupant(hero) && hero.IsActive() && hero.WieldedWeapon.CurrentUsageItem?.IsMeleeWeapon == true)
            tally.MeleeSeconds++;
    }

    internal void Clear()
    {
        _nextAllowed.Clear();
        _walking.Clear();
        _done.Clear();
        _near.Clear();
        _threats.Clear();
        _nextTick = 0f;
    }

    // At the end of each hold: the step counts as moved when his body covered half of it. A body that fell or left is dropped.
    private void CheckWalks(float now)
    {
        if (_walking.Count == 0)
            return;
        _done.Clear();
        foreach (var pair in _walking)
        {
            if (!(now >= pair.Value.EndsAt))
                continue;
            _done.Add(pair.Key);
            var hero = pair.Key;
            var s = pair.Value;
            if (!AgentSlotIdentity.IsCurrentOccupant(hero) || !hero.IsActive())
                continue;
            var p = hero.Position;
            if (KeepDistanceRule.Walked(s.FromX, s.FromY, s.ToX, s.ToY, p.x, p.y))
                s.Tally.Moved++;
        }

        foreach (var hero in _done)
            _walking.Remove(hero);
    }

    // The engine's own gate for a scripted move (Agent.CanBeAssignedForScriptedMovement: active, AI-controlled, not detached, not
    // fleeing, no scripted move set, not using a game object, not in a ladder queue), so an item pickup or a ladder keeps him.
    // HumanAIComponent first: the engine's gate reads it. The main agent and a mounted body are never moved.
    private static bool CanStep(Agent hero) =>
        AgentSlotIdentity.IsCurrentOccupant(hero) && hero.HumanAIComponent != null && hero.Team != null && !hero.IsMainAgent
        && !hero.HasMount && hero.CanBeAssignedForScriptedMovement();

    // Fills _threats and returns the nearest threat's distance, NaN with none. The query returns the active humanoids of hostile
    // teams inside the 2D radius (Mission.GetNearbyEnemyAgents), so only one on another level needs filtering.
    private float ReadThreats(Mission mission, Agent hero, Vec3 at, float radius)
    {
        _threats.Clear();
        _near.Clear();
        mission.GetNearbyEnemyAgents(at.AsVec2, radius, hero.Team, _near);
        var nearest = float.NaN;
        foreach (var enemy in _near)
        {
            var p = enemy.Position;
            if (!(Math.Abs(p.z - at.z) <= KeepDistanceRule.ThreatHeightMeters))
                continue;
            var d = p.AsVec2.Distance(at.AsVec2);
            _threats.Add((p.x, p.y));
            if (float.IsNaN(nearest) || d < nearest)
                nearest = d;
        }

        return nearest;
    }

    // The point he was sent to, or null when no candidate is usable.
    private (float X, float Y)? TryStep(Mission mission, Agent hero, Vec3 at, float stepBack)
    {
        var away = KeepDistanceRule.AwayFrom(at.x, at.y, _threats);
        if (away is not { } dir)
            return null;
        var from = hero.GetWorldPosition();
        foreach (var (x, y) in KeepDistanceRule.Candidates(at.x, at.y, dir.X, dir.Y, stepBack))
        {
            if (!Usable(mission, ref from, at, x, y, stepBack))
                continue;
            hero.HumanAIComponent.SetScriptedPositionAndDirectionTimed(new Vec2(x, y), KeepDistanceRule.FacingRadians(dir.X, dir.Y), KeepDistanceRule.HoldSeconds);
            return (x, y);
        }

        return null;
    }

    // On the navmesh, inside the boundary, on his level, and reached by a walking path under PathFactor times the step, so a
    // stair down off a wall is refused. The point is his own position moved to x, y, as the engine builds the move's target
    // (HumanAIComponent.SetScriptedPositionAndDirectionTimed), so both resolve the same navmesh face.
    private static bool Usable(Mission mission, ref WorldPosition from, Vec3 at, float x, float y, float stepBack)
    {
        var to = from;
        to.SetVec2(new Vec2(x, y));
        if (to.GetNavMesh() == UIntPtr.Zero || !mission.IsPositionInsideBoundaries(to.AsVec2))
            return false;
        if (!(Math.Abs(to.GetNavMeshZ() - at.z) <= KeepDistanceRule.MaxHeightMeters))
            return false;
        return mission.Scene.GetPathDistanceBetweenPositions(ref from, ref to, 0f, out var path) && path < stepBack * KeepDistanceRule.PathFactor;
    }

    private readonly struct Step
    {
        internal Step(float fromX, float fromY, float toX, float toY, float endsAt, HeroRangedTally tally)
        {
            FromX = fromX;
            FromY = fromY;
            ToX = toX;
            ToY = toY;
            EndsAt = endsAt;
            Tally = tally;
        }

        internal float FromX { get; }
        internal float FromY { get; }
        internal float ToX { get; }
        internal float ToY { get; }
        internal float EndsAt { get; }
        internal HeroRangedTally Tally { get; }
    }
}
