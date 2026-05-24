using BehaviorTrees;
using BehaviorTreeWrapper;
using System;
using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.AdvancedCombat.Services;
using TAOM.Features.Warg;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Spider;

/// <summary>
/// Mission lifecycle manager for spider agents. Spawns SpiderConfig.SpawnCount
/// spiders one second after mission start (Custom Battle only, gated to avoid
/// affecting campaign battles), then attaches the SpiderTree behavior tree to each.
/// Late-spawned spiders (e.g. via console) also get a BT via OnAgentBuild.
/// </summary>
public class SpiderMissionBehavior : MissionLogic
{
    private readonly IMissionAdapterFactory _adapterFactory;
    private readonly IBoneCollisionService _boneCollisionService;
    private readonly ISpiderSpawnerService _spawnerService;
    private readonly IModLogger _logger;
    private readonly HashSet<string> _loggedErrors = new();
    private readonly List<(Agent agent, BehaviorTreeAgentComponent comp)> _spiderComponents = new();
    private float _timeSinceStart = 0f;
    private bool _initialized = false;
    private bool _spawned = false;
    private bool _treesAdded = false;
    private bool _managesCombatInfrastructure = false;
    private float _gridUpdateTimer = 0f;
    private const float GridUpdateInterval = 2f;

    public SpiderMissionBehavior()
    {
        _adapterFactory = IoC.Resolve<IMissionAdapterFactory>();
        _boneCollisionService = IoC.Resolve<IBoneCollisionService>();
        _spawnerService = IoC.Resolve<ISpiderSpawnerService>();
        _logger = IoC.Resolve<IModLogger>();
    }

    private void Initialize()
    {
        _initialized = true;
        BTRegister.RegisterClass("SpiderTree", (object[] objects) => SpiderBehaviorTree.BuildTree(objects));

        if (Mission.Current.GetMissionBehavior<AdvancedCombatBehavior>() == null
            && Mission.Current.GetMissionBehavior<WargMissionBehavior>() == null)
        {
            // Only manage SpatialGrid if no other behavior is doing it.
            _managesCombatInfrastructure = true;
            SpatialGrid.Instance ??= new SpatialGrid();
        }

        _logger.LogInfo("[Spider] Initialized");
    }

    private bool ShouldSpawnInThisMission()
    {
        // Only spawn in custom battles for v1 — avoid leaking into campaign battles
        // until we have proper triggers (e.g. Mirkwood scene gating).
        if (Mission.Current == null) return false;
        if (Mission.Current.Mode != MissionMode.Battle) return false;
        return Mission.Current.GetMissionBehavior<CustomBattleAgentLogic>() != null;
    }

    private Team PickEnemyTeam()
    {
        // Find a team that's hostile to the player.
        Team playerTeam = Mission.Current.PlayerTeam;
        if (playerTeam == null) return null;

        foreach (Team team in Mission.Current.Teams)
        {
            if (team == playerTeam) continue;
            if (team.IsEnemyOf(playerTeam)) return team;
        }
        return null;
    }

    public override void OnMissionTick(float dt)
    {
        try
        {
            if (!_initialized) Initialize();

            _timeSinceStart += dt;
            if (_timeSinceStart < 1f) return;

            if (_managesCombatInfrastructure)
            {
                _gridUpdateTimer += dt;
                if (_gridUpdateTimer >= GridUpdateInterval)
                {
                    _gridUpdateTimer = 0f;
                    if (Mission.Current != null)
                    {
                        SpatialGrid.Instance.UpdateGrid(Mission.Current.AllAgents);
                    }
                    _boneCollisionService.TickBoneChecks(dt);
                }
            }

            if (!_spawned)
            {
                _spawned = true;
                if (ShouldSpawnInThisMission())
                {
                    Team enemyTeam = PickEnemyTeam();
                    if (enemyTeam != null)
                    {
                        Vec3 reference = Mission.Current.MainAgent?.Position ?? Vec3.Zero;
                        var spawned = _spawnerService.SpawnSpiders(
                            enemyTeam, reference, SpiderConfig.SpawnCount, SpiderConfig.SpawnRadius);
                        _logger.LogInfo($"[Spider] Custom Battle spider spawn: {spawned.Count} agents");
                    }
                    else
                    {
                        _logger.LogWarning("[Spider] No enemy team found for spider spawn");
                    }
                }
            }

            if (!_treesAdded)
            {
                _treesAdded = true;
                int spiderCount = 0;
                foreach (Agent agent in Mission.Current.AllAgents)
                {
                    if (_adapterFactory.GetAgentAdapter(agent).IsSpider())
                    {
                        AttachBehaviorTree(agent);
                        spiderCount++;
                    }
                }
                _logger.LogInfo($"[Spider] Attached behavior trees to {spiderCount} spiders");
            }

            // v1.4.5 Agent.Tick auto-calls component.OnTick(dt) every frame
            // (Agent.cs:4768). See WargMissionBehavior for the same fix —
            // Codex review 2026-05-24 F1. Manual call removed; only prune dead spiders.
            for (int i = _spiderComponents.Count - 1; i >= 0; i--)
            {
                var (spider, _) = _spiderComponents[i];
                if (!spider.IsActive())
                    _spiderComponents.RemoveAt(i);
            }
        }
        catch (Exception ex)
        {
            var errorKey = $"{ex.GetType().Name}:{ex.TargetSite?.Name}";
            if (_loggedErrors.Add(errorKey))
                _logger.LogError($"[Spider] OnMissionTick error: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
        }
    }

    public override void OnAgentBuild(Agent agent, Banner banner)
    {
        base.OnAgentBuild(agent, banner);
        if (!_treesAdded || agent == null) return;

        try
        {
            if (_adapterFactory.GetAgentAdapter(agent).IsSpider())
                AttachBehaviorTree(agent);
        }
        catch (Exception ex)
        {
            var errorKey = $"OnAgentBuild:{ex.GetType().Name}";
            if (_loggedErrors.Add(errorKey))
                _logger.LogError($"[Spider] OnAgentBuild error: {ex.Message}");
        }
    }

    private void AttachBehaviorTree(Agent agent)
    {
        var comp = new BehaviorTreeAgentComponent(agent, "SpiderTree", Array.Empty<object>());
        agent.AddComponent(comp);
        if (comp.Tree != null)
            _spiderComponents.Add((agent, comp));
        else
            _logger.LogError($"[Spider] BT build failed for {agent.Name}");
    }

    public override void OnRemoveBehavior()
    {
        _spiderComponents.Clear();
        // Clear error dedup so a fresh Custom Battle session can re-log
        // genuinely new occurrences of the same exception type.
        _loggedErrors.Clear();
        if (_managesCombatInfrastructure)
        {
            _boneCollisionService.Clear();
        }
        base.OnRemoveBehavior();
    }
}
