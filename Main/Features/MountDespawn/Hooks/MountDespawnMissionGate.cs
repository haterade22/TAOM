using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.MountDespawn.Hooks;

/// <summary>
/// Decides whether dead mounts may be retired in a given mission. Its own type so the
/// MissionBehavior stays a thin entry point (ADR-002) and so the allowlist is testable in isolation.
///
/// Structure copied from <c>TAOM.Features.DreadAura.Hooks.DreadMissionGate</c>, with one deliberate
/// difference: no <c>Campaign.Current</c> requirement, because a custom battle gets the same
/// performance win and this feature needs no campaign context.
///
/// <c>SubModule.OnMissionBehaviorInitialize</c> fires for EVERY mission, including town walkarounds
/// and conversations. Those scenes set <c>MissionInitializerRecord.DisableCorpseFadeOut</c> and
/// deliberately keep bodies forever, and <c>CorpseDraggingMissionLogic</c> depends on them
/// persisting. The allowlist below excludes them by construction.
/// </summary>
public static class MountDespawnMissionGate
{
    /// <summary>
    /// True only for a field battle, siege, or sally-out.
    ///
    /// This is an ALLOWLIST, deliberately. A blocklist would silently admit every mission type added
    /// by a future engine version or another mod.
    /// </summary>
    public static bool IsEligible(Mission mission)
    {
        if (mission == null)
            return false;

        if (GameNetwork.IsSessionActive)
            return false;

        // Cheapest first, because this runs per dead mount as well as per sweep. Mode and the
        // team-AI checks are field reads and enum compares; CombatType is a native call through
        // MBAPI.IMBMission.GetCombatType, so it goes last. All three are independent rejections,
        // so the order changes the cost and not the answer.
        //
        // Nothing dies during deployment, but the mode also fences off the order phase and any
        // non-battle mode a mission passes through.
        if (mission.Mode != MissionMode.Battle)
            return false;

        // MissionTeamAITypeEnum is { NoTeamAI, FieldBattle, Siege, SallyOut, NavalBattle, NavalRaid }
        // so hideouts and arenas, which are NoTeamAI, are excluded here too. Naval is excluded
        // because TAOM's naval travel is parked (#120/#296).
        if (!(mission.IsFieldBattle || mission.IsSiegeBattle || mission.IsSallyOutBattle))
            return false;

        // ArenaCombat covers arenas and tournaments; NoCombat covers conversations and walkarounds.
        return mission.CombatType == Mission.MissionCombatType.Combat;
    }
}
