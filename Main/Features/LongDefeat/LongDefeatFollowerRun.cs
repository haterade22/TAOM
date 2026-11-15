using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Features.LongDefeat;

/// <summary>
/// A co-op follower's part of a run (#1280). The host's director spawns every soldier and runs the waves, and Coop shows them here as
/// puppets, so a follower only boots the deployment plan and spawns its own lord. It never adopts the host's defenders: the engine
/// still makes its lord the order owner of its own team, which holds only him (AssignPlayerRoleInTeamMissionController).
/// Slice 1 spawns the lord on foot: a mounted spawn needs the home point that only the host's sally sets up.
/// </summary>
internal static class LongDefeatFollowerRun
{
    /// <summary>False when the field cannot boot or the lord cannot be built.</summary>
    public static bool Start(RunPlan plan, ILongDefeatBattlefieldAdapter field, IModLogger logger)
    {
        if (!field.Boot(LongDefeatPlanSizes.Of(plan), out var problem))
        {
            logger.LogError($"[LongDefeat][Coop] the follower's field cannot boot: {problem}");
            return false;
        }

        var rides = plan.Loadout?.Mounted == true;
        if (!field.SpawnPlayer(true, rides ? null : plan.Loadout))
        {
            logger.LogError("[LongDefeat][Coop] the follower's lord could not be spawned");
            return false;
        }

        field.FinishInitialSpawn();
        var onFoot = rides ? " on foot, without the mounted class" : "";
        logger.LogInfo($"[LongDefeat][Coop] follower lord spawned for fortress {plan.FortressId}{onFoot}");
        return true;
    }
}
