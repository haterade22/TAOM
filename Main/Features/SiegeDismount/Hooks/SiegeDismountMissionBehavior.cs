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

    // BehaviorType=Other: this class inherits MissionBehavior (not MissionLogic) and does not
    // override MissionEnded/OnMissionResultReady, so it has no business in Mission.MissionLogics.
    // Returning Logic here caused vanilla AddMissionBehavior to do `MissionLogics.Add(this as MissionLogic)`
    // which evaluates to null and NREs the next CheckMissionEnded tick.
    public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

    public SiegeDismountMissionBehavior()
    {
        _service = IoC.Resolve<ISiegeDismountService>();
        _logger = IoC.Resolve<IModLogger>();
    }

    // AfterStart, for two reasons (#606). OnBehaviorInitialize never fires for a behavior TAOM adds
    // (the engine dispatches it before SubModule.OnMissionBehaviorInitialize, Mission.AfterStart
    // :3827 vs :3831). And IsSiegeBattle reads MissionTeamAIType, which MissionCombatantsLogic.EarlyStart
    // sets, so even a firing OnBehaviorInitialize would have read false. AfterStart runs after every
    // EarlyStart and before the spawn logic's first tick (DefaultBattleMissionAgentSpawnLogic
    // spawns from OnMissionTick), so the mount is stripped before the player agent is built.
    public override void AfterStart()
    {
        base.AfterStart();

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
