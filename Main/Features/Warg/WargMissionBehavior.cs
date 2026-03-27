using BehaviorTrees;
using BehaviorTreeWrapper;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.AdvancedCombat.Services;
using System;
using System.Collections.Generic;
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
    private int _gridUpdateTick = 0;
    private const int GridUpdateInterval = 5;

    public WargMissionBehavior()
    {
        _adapterFactory = IoC.Resolve<IMissionAdapterFactory>();
        _boneCollisionService = IoC.Resolve<IBoneCollisionService>();
        _logger = IoC.Resolve<IModLogger>();
    }

    private void Initialize()
    {
        _initialized = true;
        _logger.LogInfo("[Warg] WargMissionBehavior initializing");

        try
        {
            BTRegister.RegisterClass("WargTree", (object[] objects) => WargBehaviorTree.BuildTree(objects));
            _logger.LogInfo("[Warg] WargTree registered with BTRegister");
        }
        catch (Exception ex)
        {
            _logger.LogError($"[Warg] Failed to register WargTree: {ex.GetType().Name}: {ex.Message}");
        }

        BTRegister.AddLogger(new TaomBTLogger());

        var autonomousMovementController = Mission.Current.GetMissionBehavior<AutonomousMovementPlayerController>();
        if (autonomousMovementController != null)
        {
            autonomousMovementController.Disable();
            _logger.LogInfo("[Warg] AutonomousMovementPlayerController disabled");
        }
        else
        {
            _logger.LogError("[Warg] AutonomousMovementPlayerController NOT FOUND in mission behaviors");
        }

        if (Mission.Current.GetMissionBehavior<AdvancedCombatBehavior>() == null)
        {
            _managesCombatInfrastructure = true;
            SpatialGrid.Instance ??= new SpatialGrid();
            _logger.LogInfo("[Warg] Managing combat infrastructure (no AdvancedCombatBehavior present)");
        }

        _logger.LogInfo("[Warg] WargMissionBehavior initialization completed");
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
                _gridUpdateTick++;
                if (_gridUpdateTick >= GridUpdateInterval)
                {
                    _gridUpdateTick = 0;
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
                int failCount = 0;

                foreach (Agent agent in Mission.Current.AllAgents)
                {
                    if (_adapterFactory.GetAgentAdapter(agent).IsWarg())
                    {
                        try
                        {
                            var testTree = WargBehaviorTree.BuildTree(new object[] { agent });
                            if (testTree == null)
                            {
                                _logger.LogError($"[Warg] BuildTree returned null for {agent.Name} (IsMount={agent.IsMount}, Rider={agent.RiderAgent?.Name ?? "null"})");
                                failCount++;
                                continue;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"[Warg] BuildTree FAILED for {agent.Name}: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
                            failCount++;
                            continue;
                        }

                        var comp = new BehaviorTreeAgentComponent(agent, "WargTree", Array.Empty<object>());
                        agent.AddComponent(comp);
                        _wargComponents.Add((agent, comp));

                        _logger.LogInfo($"[Warg] BT added: {agent.Name} (IsMount={agent.IsMount}, Rider={agent.RiderAgent?.Name ?? "null"}, Tree={((comp.Tree != null) ? "OK" : "NULL")})");
                        wargCount++;
                    }
                }
                _logger.LogInfo($"[Warg] Added behavior trees to {wargCount} wargs ({failCount} failed)");
            }

            WargRiderHandManager.Tick();

            for (int i = _wargComponents.Count - 1; i >= 0; i--)
            {
                var (warg, comp) = _wargComponents[i];
                if (!warg.IsActive())
                {
                    _wargComponents.RemoveAt(i);
                    continue;
                }
                if (comp.Tree != null)
                    comp.OnTickAsAI(dt);
            }
        }
        catch (Exception ex)
        {
            var errorKey = $"{ex.GetType().Name}:{ex.TargetSite?.Name}";
            if (_loggedErrors.Add(errorKey))
                _logger.LogError($"[Warg] OnMissionTick error: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
        }
    }

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
