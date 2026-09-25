using TAOM.Features.AdvancedCombat;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Warg;

internal static class WargRiderHandManager
{
    /// <summary>
    /// Called every mission tick by WargMissionBehavior. Warg-ness comes from the mount's own Monster,
    /// as WargMissionBehavior.TryAttachWargTree decides it: no container lookup and no adapter cache
    /// lookup per frame.
    /// </summary>
    public static void Tick()
    {
        if (Agent.Main == null) return;

        if (Agent.Main.HasMount && WargConfig.IsWargMonster(Agent.Main.MountAgent.Monster?.StringId))
        {
            UpdateWargRiderHandle();
        }
    }

    public static void OnMainAgentDismount()
    {
        ClearCustomLookDirection();
    }

    private static void ClearCustomLookDirection()
    {
        AutonomousMovementPlayerController missionMainAgentController = Mission.Current.GetMissionBehavior<AutonomousMovementPlayerController>();
        missionMainAgentController.CustomLookDir = Vec3.Zero;
    }

    private static void UpdateWargRiderHandle()
    {
        AutonomousMovementPlayerController missionMainAgentController = Mission.Current.GetMissionBehavior<AutonomousMovementPlayerController>();

        if (Agent.Main.GetCurrentAction(0) == ActionIndexCache.act_none && Agent.Main.GetCurrentAction(1) == ActionIndexCache.act_none && !Agent.Main.HeadCameraMode)
        {
            Vec3 newLookDir = Agent.Main.GetMovementDirection().ToVec3();
            missionMainAgentController.CustomLookDir = newLookDir;
            return;
        }

        ClearCustomLookDirection();
    }
}
