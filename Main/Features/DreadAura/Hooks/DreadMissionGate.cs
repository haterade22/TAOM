using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TAOM.Features.FordsOfWar;

namespace TAOM.Features.DreadAura.Hooks;

/// <summary>
/// Decides whether the dread aura may run in a given mission. Its own type so the MissionLogic
/// stays a thin entry point (ADR-002) and so the allowlist is testable in isolation.
///
/// <c>SubModule.OnMissionBehaviorInitialize</c> fires for EVERY mission: conversations, town
/// walkarounds, arenas, tournaments, hideouts, custom battles, multiplayer, and the headless
/// shader-precompilation walk. A wraith entrant draining a tournament could rout the other entrants
/// (at moraleFloor 0; at the shipped floor it would only weaken them) inside an arena that has no
/// retreat position, which is the failure family TAOM
/// already carries an RCA and a dedicated patch category for
/// (<c>docs/reviews/rca-tournament-exit-hang-2026-07-06.md</c>).
/// </summary>
public static class DreadMissionGate
{
    /// <summary>
    /// True only for a campaign field battle, siege, or sally-out, or a Fords of War match (which runs with no campaign).
    ///
    /// This is an ALLOWLIST, deliberately. A blocklist would silently admit every mission type
    /// added by a future engine version or another mod.
    /// </summary>
    public static bool IsEligible(Mission mission) => IsEligible(mission, FordsOfWarMission.IsActive);

    /// <summary>
    /// The gate with the Fords of War test passed in, so a unit test can stand in for the engine's mission state. A Fords of War
    /// match (a campaign-free mission of TAOM's own, build-spec section 4) is eligible with no campaign: it is checked before the
    /// campaign requirement, and the network-session rule still applies to it.
    /// </summary>
    internal static bool IsEligible(Mission mission, Func<Mission, bool> isFordsMission)
    {
        if (mission == null)
            return false;

        if (isFordsMission(mission))
            return !GameNetwork.IsSessionActive;

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
