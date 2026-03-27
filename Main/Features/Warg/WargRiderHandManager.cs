using TAOM.Adapters;
using TAOM.Features.AdvancedCombat;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Warg;

internal static class WargRiderHandManager
{
    public static void Tick()
    {
        if (Agent.Main == null) return;

        if (Agent.Main.HasMount && IoC.Resolve<IMissionAdapterFactory>().GetAgentAdapter(Agent.Main.MountAgent).IsWarg())
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
