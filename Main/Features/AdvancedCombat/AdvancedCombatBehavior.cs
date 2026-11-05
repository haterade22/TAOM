using System;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat.Services;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.AdvancedCombat;

public class AdvancedCombatBehavior : MissionLogic
{
    private readonly IBoneCollisionService _boneCollisionService;
    private readonly ISpatialGridDebugService _debugService;
    private readonly IMissionAdapterFactory _adapterFactory;
    private readonly IModLogger _logger;
    private const float GridUpdateInterval = 2f;
    private float _timeSinceLastUpdate = 0f;
    private bool _buildFailureLogged;

    public AdvancedCombatBehavior()
    {
        SpatialGrid.Instance = new();
        _boneCollisionService = IoC.Resolve<IBoneCollisionService>();
        _debugService = IoC.Resolve<ISpatialGridDebugService>();
        _adapterFactory = IoC.Resolve<IMissionAdapterFactory>();
        _logger = IoC.Resolve<IModLogger>();
    }

    public override void OnMissionTick(float dt)
    {
        // This is the main mission thread; every TAOM blow and creature action must run on it (#592).
        MissionThreadGuard.MarkMainThread();
        SpatialGrid.Instance?.ApplyPendingRemovals();

        // Bone checks must tick every frame to catch short animation windows (0.5-0.7s)
        _boneCollisionService.TickBoneChecks(dt);

        // Grid rebuild and debug rendering are throttled to reduce overhead
        _timeSinceLastUpdate += dt;
        if (_timeSinceLastUpdate < GridUpdateInterval) return;
        _timeSinceLastUpdate = 0f;

        if (Mission.Current != null)
        {
            SpatialGrid.Instance.UpdateGrid(Mission.Current.AllAgents);
            _debugService.RenderDebugVisualization();
        }
    }

    // The adapter cache's lifecycle lives here, with the infrastructure every creature feature
    // shares, so no single feature's registration decides whether dead handles are dropped (#592).
    // Built: count a build that lands on a freed index. Deleted: the engine may hand this agent's
    // index to a new agent from here on, and the dead managed object keeps pointing at the slot.
    public override void OnAgentBuild(Agent agent, Banner banner)
    {
        base.OnAgentBuild(agent, banner);
        // Inside Mission.SpawnAgent's unguarded loop over behaviors: never let an exception out (#595).
        try
        {
            _adapterFactory.OnAgentBuilt(agent);
        }
        catch (Exception ex)
        {
            if (!_buildFailureLogged)
            {
                _buildFailureLogged = true;
                _logger.LogError($"[AdvancedCombat] OnAgentBuild threw {ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    public override void OnAgentDeleted(Agent affectedAgent)
    {
        base.OnAgentDeleted(affectedAgent);
        _adapterFactory.Evict(affectedAgent);
        SpatialGrid.Instance?.Remove(affectedAgent);
    }

    public void AddBoneCheckComponent(BoneCheck component)
    {
        _boneCollisionService.AddBoneCheckComponent(component);
    }

    public override void OnRemoveBehavior()
    {
        _boneCollisionService.Clear();
        _adapterFactory.ClearCache();
        base.OnRemoveBehavior();
    }
}
