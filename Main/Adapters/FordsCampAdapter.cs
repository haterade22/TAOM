using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Logging;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Adapters;

/// <summary>
/// The camps of a Fords of War mission (<see cref="IFordsCampAdapter"/>). The creatures play on a third team with no battle side
/// (the launcher's West and East stay the only two sides), hostile to both by explicit relations; the mission's behaviours index
/// nothing by a team's side. Camp <c>n</c> is one regular formation of that team, so a mission holds at most eight camps. The ring is
/// the decal the campaign map draws round a party (<c>map_circle_decal</c>, tile 5 of its strip), scaled to the camp.
/// </summary>
internal sealed class FordsCampAdapter : IFordsCampAdapter
{
    private const string RingMaterial = "map_circle_decal";
    private const uint RingColor = 0xFFD8A030;
    private const float RingHeight = 12f;
    private const int MaxCamps = 8;

    private readonly Mission _mission;
    private readonly FordsAgentRegistry _registry;
    private readonly IModLogger _logger;
    private readonly object _gate = new();
    private readonly Dictionary<Agent, string> _creatures = new();
    private readonly Dictionary<string, Formation> _formations = new();
    private Team? _team;

    public FordsCampAdapter(Mission mission, FordsAgentRegistry registry, IModLogger logger)
    {
        _mission = mission;
        _registry = registry;
        _logger = logger;
    }

    public bool Prepare()
    {
        var west = FordsEngine.TeamOf(_mission, MatchSide.West);
        var east = FordsEngine.TeamOf(_mission, MatchSide.East);
        if (west == null || east == null)
            return false;

        // The west banner stands in: the build data needs one, and no creature shows it.
        _team = _mission.Teams.Add(BattleSideEnum.None, uint.MaxValue, uint.MaxValue, west.Banner, false, false, false);
        foreach (var side in new[] { west, east })
        {
            _team.SetIsEnemyOf(side, true);
            side.SetIsEnemyOf(_team, true);
        }

        return true;
    }

    public bool Spawn(string campId, string troopId, Vec3f position)
    {
        var character = FordsEngine.Character(troopId);
        if (_team == null || character == null)
        {
            _logger.LogWarning($"[FordsOfWar][Camp] {campId}: troop '{troopId}' cannot be spawned (team {(_team == null ? "missing" : "ready")})");
            return false;
        }

        try
        {
            if (!_formations.TryGetValue(campId, out var formation))
            {
                if (_formations.Count >= MaxCamps)
                    return false;

                formation = _team.GetFormation((FormationClass)_formations.Count);
                _formations[campId] = formation;
            }

            var ground = new Vec3(position.X, position.Y, FordsEngine.GroundAt(_mission.Scene, position.X, position.Y), -1f);
            var direction = Vec2.Forward;
            var data = new AgentBuildData(character).Team(_team).Banner(_team.Banner).ClothingColor1(_team.Color).ClothingColor2(_team.Color2)
                .TroopOrigin(new BasicBattleAgentOrigin(character)).NoHorses(true).Formation(formation)
                .InitialPosition(in ground).InitialDirection(in direction);
            var agent = _mission.SpawnTroopWithAgentBuildData(data, true, true);
            lock (_gate)
                _creatures[agent] = campId;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"[FordsOfWar][Camp] spawning '{troopId}' at {campId} failed ({ex.GetType().Name}: {ex.Message})");
            return false;
        }
    }

    public void AddRing(string campId, Vec3f position, float radius)
    {
        var material = Material.GetFromResource(RingMaterial);
        if (material == null)
        {
            _logger.LogWarning($"[FordsOfWar][Camp] {campId}: the material {RingMaterial} is not loaded; no ring");
            return;
        }

        var entity = GameEntity.CreateEmpty(_mission.Scene);
        entity.Name = "fow_ring_" + campId;
        var decal = Decal.CreateDecal();
        decal.SetMaterial(material);
        decal.SetVectorArgument(0.166f, 1f, 0.83f, 0f);
        decal.SetFactor1Linear(RingColor);
        var frame = MatrixFrame.Identity;
        frame.origin = new Vec3(position.X, position.Y, FordsEngine.GroundAt(_mission.Scene, position.X, position.Y), -1f);
        var scale = new Vec3(radius * 2f, radius * 2f, RingHeight, -1f);
        frame.Scale(in scale);
        entity.SetGlobalFrame(in frame);
        _mission.Scene.AddDecalInstance(decal, "editor_set", true);
        entity.AddComponent(decal);
        entity.SetVisibilityExcludeParents(true);
    }

    public void SetAlerted(string campId, Vec3f home, bool alerted)
    {
        if (!_formations.TryGetValue(campId, out var formation))
            return;

        if (alerted)
        {
            formation.SetMovementOrder(MovementOrder.MovementOrderCharge);
            return;
        }

        var ground = new Vec3(home.X, home.Y, FordsEngine.GroundAt(_mission.Scene, home.X, home.Y), -1f);
        formation.SetMovementOrder(MovementOrder.MovementOrderMove(new WorldPosition(_mission.Scene, ground)));
    }

    public bool TryRemove(object affected, object? affector, bool killed, out string campId, out AgentKey? killer)
    {
        killer = null;
        campId = string.Empty;
        if (affected is not Agent agent)
            return false;

        lock (_gate)
        {
            if (!_creatures.TryGetValue(agent, out var camp))
                return false;

            _creatures.Remove(agent);
            campId = camp;
        }

        // The killer may have fallen in the same frame: the registry still knows the last few evicted agents.
        if (killed && _registry.TryGetRecentKey(affector, out var key))
            killer = key;
        return true;
    }

    public void Clear()
    {
        lock (_gate)
            _creatures.Clear();
        _formations.Clear();
        _team = null;
    }
}
