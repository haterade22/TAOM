using System;
using System.Collections.Generic;
using System.Linq;
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
/// then <c>SpawnTroopWithAgentBuildData</c>) with the lane's formation and an explicit spot. No unit spawns with the Player controller:
/// the mission has a main agent only while the commander controls a soldier (<c>FordsAgentControl</c>). Orders go through <c>Formation.SetMovementOrder</c> on the main thread. Agents are held by
/// object in the shared <see cref="FordsAgentRegistry"/> and evicted when the engine removes them, never keyed by <c>Agent.Index</c>.
/// </summary>
internal sealed class FordsBattlefieldAdapter : IFordsBattlefieldAdapter
{
    private readonly Mission _mission;
    private readonly FordsAgentRegistry _registry;
    private const int HousekeepPassesPerLine = 10;

    private LaneFaceBars? _laneBars;
    private int _thrownTopUps;
    private readonly Dictionary<string, int> _mountsRemoved = new();
    private readonly Dictionary<string, int> _mountsSpared = new();
    private int _housekeepPasses;
    private readonly Func<string, object?> _towerEntity;
    private readonly IModLogger _logger;
    private readonly Dictionary<(MatchSide, int), Formation> _formations = new();
    private readonly HashSet<(MatchSide, int, string)> _orderLogged = new();
    private bool _unplaceableLogged;

    public FordsBattlefieldAdapter(Mission mission, FordsAgentRegistry registry, Func<string, object?> towerEntity, IModLogger logger)
    {
        _mission = mission;
        _registry = registry;
        _towerEntity = towerEntity;
        _logger = logger;
    }

    public int MaxAgents => DefaultBattleMissionAgentSpawnLogic.MaxNumberOfAgentsForMission;

    /// <summary>The camps of this mission. They share the registry, which names the unit that killed a creature.</summary>
    public FordsCampAdapter NewCampAdapter() => new(_mission, _registry, _logger);

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

    public void SetLaneBars(LaneFaceBars? bars) => _laneBars = bars;

    public void Housekeep()
    {
        foreach (var entry in _registry.Snapshot())
        {
            var agent = (Agent)entry.Agent;
            if (!IsLive(agent))
                continue;

            for (var slot = EquipmentIndex.WeaponItemBeginSlot; slot < EquipmentIndex.NumAllWeaponSlots; slot++)
            {
                var weapon = agent.Equipment[slot];
                if (!weapon.IsEmpty && weapon.IsAnyConsumable() && !weapon.IsAnyAmmo() && weapon.Amount < weapon.ModifiedMaxAmount)
                {
                    agent.SetWeaponAmountInSlot(slot, weapon.ModifiedMaxAmount, false);
                    _thrownTopUps++;
                }
            }
        }

        RemoveRiderlessMounts();

        if (++_housekeepPasses % HousekeepPassesPerLine == 0)
            _logger.LogInfo($"[FordsOfWar][Housekeep] thrownTopUps={_thrownTopUps} mountsRemoved={ByKind(_mountsRemoved)} mountsSpared={ByKind(_mountsSpared)} (totals)");
    }

    private static string ByKind(Dictionary<string, int> counts) => counts.Count == 0 ? "-" : string.Join(",", counts.Select(p => p.Key + ":" + p.Value));

    private static void Count(Dictionary<string, int> counts, string kind) => counts[kind] = (counts.TryGetValue(kind, out var n) ? n : 0) + 1;

    /// <summary>
    /// Every riderless mount, of any kind (<see cref="RiderlessMountRule"/>). A warg in a rage keeps its rider, so an empty one has lost it. A mount a soldier holds or has chosen stays;
    /// the Housekeep line counts, per Monster id, the mounts removed and how often that guard held one back. The soft fade is the call vanilla makes for the same job (SpawningBehaviorBase).
    /// </summary>
    private void RemoveRiderlessMounts()
    {
        // A copy: removing a mount changes the engine's list.
        var candidates = _mission.MountsWithoutRiders
            .Where(p => p.Key.IsActive() && p.Key.RiderAgent == null && p.Value.ElapsedSeconds >= RiderlessMountRule.WaitSeconds)
            .ToList();
        if (candidates.Count == 0)
            return;

        var claimed = new HashSet<int>();
        foreach (var agent in _mission.Agents)
        {
            if (!agent.IsActive() || !agent.IsHuman)
                continue;

            if (agent.MountAgent is { } held)
                claimed.Add(held.Index);
            var chosen = agent.GetSelectedMountIndex();
            if (chosen >= 0)
                claimed.Add(chosen);
        }

        foreach (var pair in candidates)
        {
            var kind = pair.Key.Monster?.StringId ?? "?";
            if (RiderlessMountRule.MayFade(pair.Value.ElapsedSeconds, claimed.Contains(pair.Key.Index)))
            {
                pair.Key.FadeOut(false, false);
                Count(_mountsRemoved, kind);
            }
            else
            {
                Count(_mountsSpared, kind);
            }
        }
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
            // Alarmed, as vanilla field battles spawn their troops (MissionBattleSideSpawnContext): an unalarmed unit may not fight.
            var agent = _mission.SpawnTroopWithAgentBuildData(data, true, true);
            if (_laneBars != null)
            {
                // Same order on every agent of a lane: the engine keeps one exclusion set per exact ordered list.
                foreach (var face in _laneBars.BarsFor(lane))
                {
                    agent.SetAgentExcludeStateForFaceGroupId(face, true);
                    agent.MountAgent?.SetAgentExcludeStateForFaceGroupId(face, true);
                }
            }

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
                samples.Add(new FordsUnitSample(entry.Key, entry.Side, entry.Lane, position, agent.IsAlarmed(),
                    (agent.GetScriptedCombatFlags() & Agent.AISpecialCombatModeFlags.AttackEntity) != 0));
        }

        return samples;
    }

    public IReadOnlyList<FordsFaceSample> SampleFaces()
    {
        var registered = _registry.Snapshot();
        var samples = new List<FordsFaceSample>(registered.Count);
        foreach (var entry in registered)
        {
            var agent = (Agent)entry.Agent;
            if (!IsLive(agent) || FordsEngine.FinitePosition(agent.Position) is not { } position)
                continue;

            var terrain = FordsEngine.TerrainAt(_mission.Scene, position.X, position.Y);
            var navZ = new WorldPosition(_mission.Scene, agent.Position).GetNavMeshZ();
            samples.Add(new FordsFaceSample(entry.Side, entry.Lane, agent.GetCurrentNavigationFaceId(), agent.HasMount,
                position.Z - terrain, navZ - position.Z));
        }

        return samples;
    }

    public void ApplyPoison(AgentKey victim, AgentKey attacker, int damage)
    {
        if (!_registry.TryGetAgent(victim, out var found, out _, out _))
            return;

        var agent = (Agent)found;
        if (!IsLive(agent) || agent.CurrentMortalityState != Agent.MortalityState.Mortal)
            return;

        if (agent.Health > damage)
        {
            agent.Health -= damage;
            return;
        }

        // Owner -1 lets the engine credit the last blow, which is what Agent.Die does when the shooter has gone.
        var owner = _registry.TryGetAgent(attacker, out var shooter, out _, out _) && IsLive((Agent)shooter) ? ((Agent)shooter).Index : -1;
        var blow = new Blow(owner)
        {
            DamageType = DamageTypes.Pierce,
            BoneIndex = agent.Monster.HeadLookDirectionBoneIndex,
            GlobalPosition = agent.Position,
            BaseMagnitude = damage,
            InflictedDamage = damage,
            SwingDirection = agent.LookDirection,
            DamageCalculated = true,
        };
        blow.GlobalPosition.z += agent.GetEyeGlobalHeight();
        blow.WeaponRecord.FillAsMeleeBlow(null, null, -1, -1);
        blow.Direction = blow.SwingDirection;
        agent.Die(blow);
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

    public bool SetArrangement(MatchSide side, int lane, LaneArrangement arrangement)
    {
        if (!_formations.TryGetValue((side, lane), out var formation))
            return false;

        formation.SetArrangementOrder(arrangement switch
        {
            LaneArrangement.Loose => ArrangementOrder.ArrangementOrderLoose,
            LaneArrangement.ShieldWall => ArrangementOrder.ArrangementOrderShieldWall,
            LaneArrangement.Square => ArrangementOrder.ArrangementOrderSquare,
            LaneArrangement.Column => ArrangementOrder.ArrangementOrderColumn,
            LaneArrangement.Skein => ArrangementOrder.ArrangementOrderSkein,
            _ => ArrangementOrder.ArrangementOrderLine,
        });
        return true;
    }

    public bool AttackTower(MatchSide side, int lane, string towerId)
    {
        if (!_formations.TryGetValue((side, lane), out var formation) || _towerEntity(towerId) is not GameEntity entity)
            return false;

        formation.SetMovementOrder(MovementOrder.MovementOrderAttackEntity(entity, true));
        if (entity.GetFirstScriptOfType<FordsTowerComponent>() != null && _orderLogged.Add((side, lane, towerId)))
            LogOrder(side, lane, towerId, entity, formation);
        return true;
    }

    private void LogOrder(MatchSide side, int lane, string towerId, GameEntity entity, Formation formation)
    {
        try
        {
            _logger.LogInfo("[FordsOfWar][Tower] " + FordsTowerOrderProbe.Describe(_mission.Scene, entity, formation, towerId, side, lane));
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"[FordsOfWar][Tower] order probe of {towerId} failed ({ex.GetType().Name}: {ex.Message})");
        }
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
        _orderLogged.Clear();
    }

    private static bool IsLive(Agent agent) => agent.IsActive() && AgentSlotIdentity.IsCurrentOccupant(agent);
}
