using BehaviorTrees;
using BehaviorTreeWrapper;
using TAOM.Adapters;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.AdvancedCombat.Services;
using System;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.MissionViews;

namespace TAOM.Features.Warg;

public class WargMissionBehavior : MissionLogic
{
    private readonly IMissionAdapterFactory _adapterFactory;
    private readonly IBoneCollisionService _boneCollisionService;
    private float _timeSinceStart = 0f;
    private bool _treesAdded = false;
    private bool _managesCombatInfrastructure = false;
    private int _gridUpdateTick = 0;
    private const int GridUpdateInterval = 5;

    public WargMissionBehavior()
    {
        _adapterFactory = IoC.Resolve<IMissionAdapterFactory>();
        _boneCollisionService = IoC.Resolve<IBoneCollisionService>();
    }

    public override void OnBehaviorInitialize()
    {
        base.OnBehaviorInitialize();
        BTRegister.RegisterClass("WargTree", (object[] objects) => WargBehaviorTree.BuildTree(objects));
        BTRegister.AddLogger(new TaomBTLogger());
        AutonomousMovementPlayerController autonomousMovementController = Mission.Current.GetMissionBehavior<AutonomousMovementPlayerController>();
        autonomousMovementController.Disable();

        if (Mission.Current.GetMissionBehavior<AdvancedCombatBehavior>() == null)
        {
            _managesCombatInfrastructure = true;
            SpatialGrid.Instance ??= new SpatialGrid();
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
            foreach (Agent agent in Mission.Current.AllAgents)
            {
                if (_adapterFactory.GetAgentAdapter(agent).IsWarg())
                {
                    agent.AddComponent(new BehaviorTreeAgentComponent(agent, "WargTree", Array.Empty<object>()));
                }
            }
        }

        WargRiderHandManager.Tick();
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
