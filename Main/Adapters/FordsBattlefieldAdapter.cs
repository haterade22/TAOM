using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Adapters;

/// <summary>
/// The troops of a Fords of War mission (<see cref="IFordsBattlefieldAdapter"/>). West is the mission's player team and East the
/// player's enemy team (the launcher builds exactly those two); lane <c>n</c> is the formation of class <c>n</c> on each (the four
/// default classes, so at most four lanes). Units are spawned the way vanilla spawns a troop (<c>Mission.GetAgentBuildDataToSpawnTroop</c>
/// then <c>SpawnTroopWithAgentBuildData</c>) with the lane's formation and an explicit spot. Nobody gets the Player controller:
/// the mission keeps no main agent. Orders go through <c>Formation.SetMovementOrder</c> on the main thread. Agents are held by
/// object in the shared <see cref="FordsAgentRegistry"/> and evicted when the engine removes them, never keyed by <c>Agent.Index</c>.
/// </summary>
internal sealed class FordsBattlefieldAdapter : IFordsBattlefieldAdapter
{
    private readonly Mission _mission;
    private readonly FordsAgentRegistry _registry;
    private readonly Func<string, object?> _towerEntity;
    private readonly IModLogger _logger;
    private readonly Dictionary<(MatchSide, int), Formation> _formations = new();
    private bool _unplaceableLogged;

    public FordsBattlefieldAdapter(Mission mission, FordsAgentRegistry registry, Func<string, object?> towerEntity, IModLogger logger)
    {
        _mission = mission;
        _registry = registry;
        _towerEntity = towerEntity;
        _logger = logger;
    }

    public int MaxAgents => DefaultBattleMissionAgentSpawnLogic.MaxNumberOfAgentsForMission;

    public bool Prepare(int laneCount)
    {
        if (laneCount < 1 || laneCount > LaneStreams.MaxLanes)
            return false;

        _formations.Clear();
        foreach (var side in new[] { MatchSide.West, MatchSide.East })
        {
            var team = FordsEngine.TeamOf(_mission, side);
            if (team == null)
                return false;

            for (var lane = 0; lane < laneCount; lane++)
                _formations[(side, lane)] = team.GetFormation((FormationClass)lane);
        }

        return true;
    }

    public void SetCorpseFadeOutSeconds(float seconds) => _mission.SetMissionCorpseFadeOutTimeInSeconds(seconds);

    public void CompleteDeployment() => _mission.OnInitialSpawnCompleted();

    public FordsSpawnedAgent? Spawn(string troopId, MatchSide side, int lane, Vec3f position, Vec3f facing)
    {
        if (!_formations.TryGetValue((side, lane), out var formation))
            return null;

        var character = FordsEngine.Character(troopId);
        if (character == null)
        {
            _logger.LogWarning($"[FordsOfWar] troop '{troopId}' does not exist in this game");
            return null;
        }

        try
        {
            var ground = new Vec3(position.X, position.Y, FordsEngine.GroundAt(_mission.Scene, position.X, position.Y), -1f);
            var direction = new Vec2(facing.X, facing.Y);
            direction.Normalize();
            var mounted = character.HasMount();
            // BasicBattleAgentOrigin carries no banner, so the build data's own copy is null: give the unit its team's.
            var data = _mission.GetAgentBuildDataToSpawnTroop(new BasicBattleAgentOrigin(character), side == MatchSide.West, false,
                mounted, false, 0, -1, ground, direction).Formation(formation).Banner(FordsEngine.TeamOf(_mission, side)?.Banner);
            var agent = _mission.SpawnTroopWithAgentBuildData(data, false, true);
            return new FordsSpawnedAgent(_registry.Register(agent, side, lane), character.Level, mounted);
        }
        catch (Exception ex)
        {
            _logger.LogError($"[FordsOfWar] spawning '{troopId}' failed ({ex.GetType().Name}: {ex.Message})");
            return null;
        }
    }

    public IReadOnlyList<FordsUnitSample> SampleUnits()
    {
        var registered = _registry.Snapshot();
        var samples = new List<FordsUnitSample>(registered.Count);
        foreach (var entry in registered)
        {
            var agent = (Agent)entry.Agent;
            if (IsLive(agent) && FordsEngine.FinitePosition(agent.Position) is { } position)
                samples.Add(new FordsUnitSample(entry.Key, entry.Side, entry.Lane, position));
        }

        return samples;
    }

    public Vec3f? PositionOf(AgentKey key)
    {
        if (!_registry.TryGetAgent(key, out var found, out _, out _))
            return null;

        var agent = (Agent)found;
        return IsLive(agent) ? FordsEngine.FinitePosition(agent.Position) : null;
    }

    public bool TryGetKey(object? agent, out AgentKey key) => _registry.TryGetKey(agent, out key);

    public MatchSide? SideOf(object? agent) => agent is Agent a ? FordsEngine.SideOf(_mission, a.Team) : null;

    public bool TryRemoveUnit(object affected, object? affector, bool killed, out AgentKey victim, out AgentKey? killer)
    {
        killer = null;
        if (!_registry.Evict(affected, out victim))
            return false;

        // The killer may have fallen in the same frame: the registry still knows the last few evicted agents.
        if (killed && _registry.TryGetRecentKey(affector, out var killerKey))
            killer = killerKey;
        return true;
    }

    public bool Move(MatchSide side, int lane, Vec3f target)
    {
        if (!_formations.TryGetValue((side, lane), out var formation))
            return true;

        // WorldPosition.IsValid tests only the 2D position and the scene, and this constructor leaves the navmesh unset, so the
        // navmesh itself is asked for, the way FordsSceneAdapter.IsWalkable does.
        var ground = new Vec3(target.X, target.Y, FordsEngine.GroundAt(_mission.Scene, target.X, target.Y), -1f);
        if (_mission.Scene.GetNavigationMeshForPosition(in ground) == UIntPtr.Zero)
        {
            if (!_unplaceableLogged)
                _logger.LogWarning($"[FordsOfWar] a move target ({target.X:F0}, {target.Y:F0}) is off the navmesh; the order is skipped (logged once)");
            _unplaceableLogged = true;
            return false;
        }

        formation.SetMovementOrder(MovementOrder.MovementOrderMove(new WorldPosition(_mission.Scene, ground)));
        return true;
    }

    public void Charge(MatchSide side, int lane)
    {
        if (_formations.TryGetValue((side, lane), out var formation))
            formation.SetMovementOrder(MovementOrder.MovementOrderCharge);
    }

    public bool AttackTower(MatchSide side, int lane, string towerId)
    {
        if (!_formations.TryGetValue((side, lane), out var formation) || _towerEntity(towerId) is not GameEntity entity)
            return false;

        formation.SetMovementOrder(MovementOrder.MovementOrderAttackEntity(entity, true));
        return true;
    }

    public string CharacterName(string characterId)
    {
        var name = FordsEngine.Character(characterId)?.Name?.ToString();
        return string.IsNullOrEmpty(name) ? characterId : FordsEngine.DisplayName(name!);
    }

    public bool CharacterExists(string characterId) => FordsEngine.Character(characterId) != null;

    public void Clear()
    {
        _registry.Clear();
        _formations.Clear();
    }

    private static bool IsLive(Agent agent) => agent.IsActive() && AgentSlotIdentity.IsCurrentOccupant(agent);
}
