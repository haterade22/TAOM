using TaleWorlds.MountAndBlade;
using TAOM.Core.Logging;

namespace TAOM.Features.SiegeDismount.Hooks;

/// <summary>
/// Thin <see cref="MissionBehavior"/> that bridges Mission lifecycle into <see cref="ISiegeDismountService"/>.
/// Resolves the service via IoC at construction and forwards primitive mission state.
/// </summary>
public class SiegeDismountMissionBehavior : MissionBehavior
{
    private readonly ISiegeDismountService _service;
    private readonly IModLogger _logger;

    public override MissionBehaviorType BehaviorType => MissionBehaviorType.Logic;

    public SiegeDismountMissionBehavior()
    {
        _service = IoC.Resolve<ISiegeDismountService>();
        _logger = IoC.Resolve<IModLogger>();
    }

    public override void OnBehaviorInitialize()
    {
        base.OnBehaviorInitialize();

        var mission = Mission.Current;
        var isSiegeBattle = mission?.IsSiegeBattle ?? false;
        var sceneName = mission?.SceneName;

        _service.OnMissionStart(isSiegeBattle, sceneName);
    }

    protected override void OnEndMission()
    {
        base.OnEndMission();
        _service.OnMissionEnd();
    }
}
