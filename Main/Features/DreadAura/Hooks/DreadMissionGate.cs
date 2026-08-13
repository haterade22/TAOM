using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.DreadAura.Hooks;

/// <summary>
/// Decides whether the dread aura may run in a given mission. Its own type so the MissionLogic
/// stays a thin entry point (ADR-002) and so the allowlist is testable in isolation.
///
/// <c>SubModule.OnMissionBehaviorInitialize</c> fires for EVERY mission: conversations, town
/// walkarounds, arenas, tournaments, hideouts, custom battles, multiplayer, and the headless
/// shader-precompilation walk. A wraith entrant draining a tournament would rout the other
/// entrants inside an arena that has no retreat position, which is the failure family TAOM
/// already carries an RCA and a dedicated patch category for
/// (<c>docs/reviews/rca-tournament-exit-hang-2026-07-06.md</c>).
/// </summary>
public static class DreadMissionGate
{
    /// <summary>
    /// True only for a campaign field battle, siege, or sally-out.
    ///
    /// This is an ALLOWLIST, deliberately. A blocklist would silently admit every mission type
    /// added by a future engine version or another mod.
    /// </summary>
    public static bool IsEligible(Mission mission)
    {
        if (mission == null)
            return false;

        // No campaign means custom battle, the main menu, or a multiplayer map. The mission-scope
        // pieces of this feature are registered against CampaignGameStarter, so running here would
        // apply the aura with none of its campaign context.
        if (Campaign.Current == null)
            return false;

        if (GameNetwork.IsSessionActive)
            return false;

        // ArenaCombat covers arenas and tournaments; NoCombat covers conversations and walkarounds.
        if (mission.CombatType != Mission.MissionCombatType.Combat)
            return false;

        // MissionTeamAIType is assigned BEFORE the deployment phase, so the allowlist below is
        // already true while the player is still positioning formations (DeploymentHandler.cs:41
        // sets Mode = Deployment; MissionCombatantsLogic.cs:225 flips it to Battle afterwards).
        // Without this clause a wraith drains morale during the order phase, and a player who
        // lingers there starts the battle with pre-drained troops. Same gate BannerBearers uses.
        if (mission.Mode != MissionMode.Battle)
            return false;

        // MissionTeamAITypeEnum is { NoTeamAI, FieldBattle, Siege, SallyOut, NavalBattle, NavalRaid }
        // — hideouts and arenas are NoTeamAI, so this allowlist excludes them by construction.
        // Naval is excluded too: TAOM's naval travel is parked (#120/#296).
        return mission.IsFieldBattle || mission.IsSiegeBattle || mission.IsSallyOutBattle;
    }
}
