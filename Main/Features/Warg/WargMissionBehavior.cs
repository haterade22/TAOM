using BehaviorTrees;
using BehaviorTreeWrapper;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.AdvancedCombat.Services;
using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.MissionViews;

namespace TAOM.Features.Warg;

public class WargMissionBehavior : MissionLogic
{
    private readonly IBoneCollisionService _boneCollisionService;
    private readonly IModLogger _logger;
    private readonly HashSet<string> _loggedErrors = new();
    private readonly List<(Agent agent, BehaviorTreeAgentComponent comp)> _wargComponents = new();
    private float _timeSinceStart = 0f;
    private bool _initialized = false;
    private bool _treesAdded = false;
    private bool _managesCombatInfrastructure = false;
    private float _gridUpdateTimer = 0f;
    // 2026-05-24: restored to ~100ms cadence to match LOTRAOM. The original was
    // `const int GridUpdateInterval = 5` ticks (≈83ms at 60fps); the TAOM refactor
    // changed the field to a time-based float but defaulted to 2f seconds, making
    // grid updates 24× less frequent. At 2s intervals, agent positions in the
    // SpatialGrid drift up to 20m at 10 m/s combat speeds — making the grid
    // useless as a target filter (both for BT decorators and for WargAttack's
    // CustomAttack range filter). 0.1f time-based form integrates cleanly with
    // the existing _gridUpdateTimer accumulator and matches LOTRAOM's frequency.
    // See issue #219.
    private const float GridUpdateInterval = 0.1f;

    public WargMissionBehavior()
    {
        _boneCollisionService = IoC.Resolve<IBoneCollisionService>();
        _logger = IoC.Resolve<IModLogger>();
    }

    private void Initialize()
    {
        _initialized = true;

        BTRegister.RegisterClass("WargTree", (object[] objects) => WargBehaviorTree.BuildTree(objects));
        BTRegister.AddLogger(new TaomBTLogger());

        var autonomousMovementController = Mission.Current.GetMissionBehavior<AutonomousMovementPlayerController>();
        if (autonomousMovementController != null)
            autonomousMovementController.Disable();
        else
            _logger.LogError("[Warg] AutonomousMovementPlayerController not found");

        if (Mission.Current.GetMissionBehavior<AdvancedCombatBehavior>() == null)
        {
            _managesCombatInfrastructure = true;
            SpatialGrid.Instance ??= new SpatialGrid();
        }

        _logger.LogInfo("[Warg] Initialized");
    }

    public override void OnAgentDismount(Agent agent)
    {
        if (agent == Agent.Main)
        {
            WargRiderHandManager.OnMainAgentDismount();
        }
    }

    public override void OnMissionTick(float dt)
    {
        try
        {
            if (!_initialized)
                Initialize();

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

            if (!_treesAdded)
            {
                _treesAdded = true;
                int wargCount = 0;

                foreach (Agent agent in Mission.Current.AllAgents)
                {
                    if (TryAttachWargTree(agent)) wargCount++;
                }
                _logger.LogInfo($"[Warg] Added behavior trees to {wargCount} wargs");
            }

            WargRiderHandManager.Tick();

            // Trees tick from BehaviorTreeMissionLogic.OnMissionTick, on the main thread (#592):
            // the engine's Agent.Tick component call runs on its asynchronous AI thread in
            // single-player and is a no-op for our component. The pruning below is ours.
            // The engine's IsActive() on a deleted agent answers for whoever inherited its slot (#592).
            for (int i = _wargComponents.Count - 1; i >= 0; i--)
            {
                var (warg, _) = _wargComponents[i];
                if (!warg.IsActive() || !AgentSlotIdentity.IsCurrentOccupant(warg))
                    _wargComponents.RemoveAt(i);
            }
        }
        catch (Exception ex)
        {
            var errorKey = $"{ex.GetType().Name}:{ex.TargetSite?.Name}";
            if (_loggedErrors.Add(errorKey))
                _logger.LogError($"[Warg] OnMissionTick error: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
        }
    }

    public override void OnAgentBuild(Agent agent, Banner banner)
    {
        base.OnAgentBuild(agent, banner);

        if (!_treesAdded || agent == null) return;

        TryAttachWargTree(agent);
    }

    /// <summary>
    /// Build and attach a warg tree, deciding from the agent's own Monster and never from a cached
    /// adapter (a cache entry can outlive its agent, and the engine recycles indices, #592). The
    /// component schedules itself with the tree logic in its constructor, before it is attached, so a
    /// throw between the two mirrors OnAgentRemoved and unschedules it; the caller's loop, whether the
    /// engine's unguarded spawn loop or the first-tick scan, goes on to the next agent (#595).
    /// </summary>
    private bool TryAttachWargTree(Agent agent)
    {
        if (!WargConfig.IsWargMonster(agent.Monster?.StringId)) return false;
        BehaviorTreeAgentComponent comp = null;
        try
        {
            comp = new BehaviorTreeAgentComponent(agent, "WargTree", Array.Empty<object>());
            agent.AddComponent(comp);
            if (comp.Tree != null)
            {
                _wargComponents.Add((agent, comp));
                return true;
            }
            _logger.LogError($"[Warg] Tree build failed for {agent.Name} (Rider={agent.RiderAgent?.Name ?? "null"})");
        }
        catch (Exception ex)
        {
            if (comp != null)
            {
                BehaviorTreeBannerlordWrapper.Instance.CurrentMissionLogic?.Unschedule(comp);
                BehaviorTreeBannerlordWrapper.Instance.DisposeTree(agent);
            }
            var errorKey = $"AttachWargTree:{ex.GetType().Name}";
            if (_loggedErrors.Add(errorKey))
                _logger.LogError($"[Warg] tree attach threw {ex.GetType().Name}: {ex.Message}");
        }
        return false;
    }

    // The adapter cache's lifecycle (build, delete, mission end) is AdvancedCombatBehavior's (#592).
    public override void OnRemoveBehavior()
    {
        _wargComponents.Clear();
        if (_managesCombatInfrastructure)
        {
            _boneCollisionService.Clear();
        }
        base.OnRemoveBehavior();
    }

    public static void SwitchMainAgentController(bool switchToSpecialController)
    {
        MissionMainAgentController baseController = Mission.Current.GetMissionBehavior<MissionMainAgentController>();
        AutonomousMovementPlayerController noMovementController = Mission.Current.GetMissionBehavior<AutonomousMovementPlayerController>();
        if (switchToSpecialController)
        {
            baseController.Disable();
            noMovementController.Enable();
        }
        else
        {
            baseController.Enable();
            noMovementController.Disable();
        }
    }
}
