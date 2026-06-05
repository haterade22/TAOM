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
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Spider;

/// <summary>
/// Mission lifecycle manager for spider agents. Attaches the SpiderTree behavior tree to every agent
/// whose body is the spider Monster — recruitable spider troops swapped at spawn time by
/// Patch45_SpiderTroopSpawn. Vanilla formation AI moves the agent; the BT is what drives its bite
/// attacks (a non-humanoid monster body has no vanilla melee logic). Owns SpatialGrid + bone-collision
/// ticking only when no other combat behavior (AdvancedCombat, Warg) is already doing so.
/// </summary>
public class SpiderMissionBehavior : MissionLogic
{
    private readonly IMissionAdapterFactory _adapterFactory;
    private readonly IBoneCollisionService _boneCollisionService;
    private readonly IModLogger _logger;
    private readonly HashSet<string> _loggedErrors = new();
    private readonly List<(Agent agent, BehaviorTreeAgentComponent comp)> _spiderComponents = new();
    private bool _initialized = false;
    private bool _treesAdded = false;
    private bool _managesCombatInfrastructure = false;
    private float _gridUpdateTimer = 0f;
    private const float GridUpdateInterval = 2f;

    public SpiderMissionBehavior()
    {
        _adapterFactory = IoC.Resolve<IMissionAdapterFactory>();
        _boneCollisionService = IoC.Resolve<IBoneCollisionService>();
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

    public override void OnMissionTick(float dt)
    {
        try
        {
            if (!_initialized) Initialize();

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

            // One-shot: BT-attach spiders present at mission start; later spawns caught by OnAgentBuild.
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

            // v1.4.5 Agent.Tick auto-calls component.OnTick(dt) each frame (Agent.cs:4768; see WargMissionBehavior, Codex 2026-05-24 F1). Just prune dead spiders here.
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
        // Clear error dedup so a fresh mission can re-log genuinely new occurrences.
        _loggedErrors.Clear();
        if (_managesCombatInfrastructure)
        {
            _boneCollisionService.Clear();
        }
        base.OnRemoveBehavior();
    }
}
