using BehaviorTrees;
using BehaviorTreeWrapper;
using TAOM.Adapters;
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
    private readonly IMissionAdapterFactory _adapterFactory;
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
        _adapterFactory = IoC.Resolve<IMissionAdapterFactory>();
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
                    if (_adapterFactory.GetAgentAdapter(agent).IsWarg())
                    {
                        var comp = new BehaviorTreeAgentComponent(agent, "WargTree", Array.Empty<object>());
                        agent.AddComponent(comp);

                        if (comp.Tree != null)
                        {
                            _wargComponents.Add((agent, comp));
                            wargCount++;
                        }
                        else
                        {
                            _logger.LogError($"[Warg] Tree build failed for {agent.Name} (Rider={agent.RiderAgent?.Name ?? "null"})");
                        }
                    }
                }
                _logger.LogInfo($"[Warg] Added behavior trees to {wargCount} wargs");
            }

            WargRiderHandManager.Tick();

            // v1.4.5 Agent.Tick auto-calls component.OnTick(dt) on every active
            // agent's components (Agent.cs:4768). The manual OnTick call that lived
            // here pre-2026-05-24 was needed in v1.3.15 where component ticking was
            // gated to AI-controlled agents (OnTickAsAI); after the v1.3->v1.4.5 rename
            // it would have caused 2x ticks per frame (Codex review 2026-05-24 F1).
            // The IsActive pruning still belongs to us — vanilla Tick doesn't drop
            // dead wargs from our shadow list.
            for (int i = _wargComponents.Count - 1; i >= 0; i--)
            {
                var (warg, _) = _wargComponents[i];
                if (!warg.IsActive())
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

        try
        {
            if (_adapterFactory.GetAgentAdapter(agent).IsWarg())
            {
                var comp = new BehaviorTreeAgentComponent(agent, "WargTree", Array.Empty<object>());
                agent.AddComponent(comp);

                if (comp.Tree != null)
                {
                    _wargComponents.Add((agent, comp));
                }
            }
        }
        catch (Exception ex)
        {
            var errorKey = $"OnAgentBuild:{ex.GetType().Name}";
            if (_loggedErrors.Add(errorKey))
                _logger.LogError($"[Warg] OnAgentBuild error: {ex.Message}");
        }
    }

    public override void OnRemoveBehavior()
    {
        _wargComponents.Clear();
        _adapterFactory.ClearCache();
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
