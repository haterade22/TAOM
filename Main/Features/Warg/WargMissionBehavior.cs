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
    private float _timeSinceStart = 0f;
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

    public override void OnBehaviorInitialize()
    {
        base.OnBehaviorInitialize();
        _logger.LogInfo("[Warg] WargMissionBehavior initializing");
        BTRegister.RegisterClass("WargTree", (object[] objects) => WargBehaviorTree.BuildTree(objects));
        BTRegister.AddLogger(new TaomBTLogger());
        AutonomousMovementPlayerController autonomousMovementController = Mission.Current.GetMissionBehavior<AutonomousMovementPlayerController>();
        autonomousMovementController.Disable();

        if (Mission.Current.GetMissionBehavior<AdvancedCombatBehavior>() == null)
        {
            _managesCombatInfrastructure = true;
            SpatialGrid.Instance ??= new SpatialGrid();
            _logger.LogInfo("[Warg] Managing combat infrastructure (no AdvancedCombatBehavior present)");
        }
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
                foreach (Agent agent in Mission.Current.AllAgents)
                {
                    if (_adapterFactory.GetAgentAdapter(agent).IsWarg())
                    {
                        agent.AddComponent(new BehaviorTreeAgentComponent(agent, "WargTree", Array.Empty<object>()));
                        wargCount++;
                    }
                }
                _logger.LogInfo($"[Warg] Added behavior trees to {wargCount} wargs");
            }

            WargRiderHandManager.Tick();
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
