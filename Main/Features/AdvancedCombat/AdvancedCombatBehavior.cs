using TAOM.Features.AdvancedCombat.Services;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.AdvancedCombat;

public class AdvancedCombatBehavior : MissionLogic
{
    private readonly IBoneCollisionService _boneCollisionService;
    private readonly ISpatialGridDebugService _debugService;
    private const float GridUpdateInterval = 2f;
    private float _timeSinceLastUpdate = 0f;

    public AdvancedCombatBehavior()
    {
        SpatialGrid.Instance = new();
        _boneCollisionService = IoC.Resolve<IBoneCollisionService>();
        _debugService = IoC.Resolve<ISpatialGridDebugService>();
    }

    public override void OnMissionTick(float dt)
    {
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
