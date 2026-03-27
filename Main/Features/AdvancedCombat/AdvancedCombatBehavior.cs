using TAOM.Features.AdvancedCombat.Services;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.AdvancedCombat;

public class AdvancedCombatBehavior : MissionLogic
{
    private readonly IBoneCollisionService _boneCollisionService;
    private readonly ISpatialGridDebugService _debugService;
    private readonly int _noTicksBeforeGridUpdate = 5;
    private int _currentTick = 0;

    public AdvancedCombatBehavior()
    {
        SpatialGrid.Instance = new();
        _boneCollisionService = IoC.Resolve<IBoneCollisionService>();
        _debugService = IoC.Resolve<ISpatialGridDebugService>();
    }

    public override void OnMissionTick(float dt)
    {
        _currentTick++;
        if (_currentTick < _noTicksBeforeGridUpdate) return;

        if (Mission.Current != null)
        {
            SpatialGrid.Instance.UpdateGrid(Mission.Current.AllAgents);
            _debugService.RenderDebugVisualization();
        }

        _boneCollisionService.TickBoneChecks(dt);
    }

    public void AddBoneCheckComponent(BoneCheck component)
    {
        _boneCollisionService.AddBoneCheckComponent(component);
    }

    public override void OnRemoveBehavior()
    {
        _boneCollisionService.Clear();
        base.OnRemoveBehavior();
    }
}
