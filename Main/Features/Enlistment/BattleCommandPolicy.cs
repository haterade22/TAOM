using TAOM.Features.Enlistment.Domain;

namespace TAOM.Features.Enlistment;

/// <summary>
/// Decides whether the enlisted player's battlefield command must be stripped (#424).
///
/// Why this exists: enlistment keeps <c>MobileParty.MainParty.Army</c> permanently null
/// (<c>ClearArmyAttachment</c> runs in both ParkNear and RestorePresence), and
/// <c>MapEvent.IsPlayerSergeant()</c> requires <c>Army != null</c> — so it is structurally
/// false for an enlisted player, and <c>AssignPlayerRoleInTeamMissionController</c> passes
/// him to <c>Team.SetPlayerRole</c> as the GENERAL of his whole side. A rank-1 private
/// commands every formation; the lord he serves commands nothing.
///
/// The policy is a pure table so the decision is testable without a mission: strip command
/// exactly when the battle was entered as enlisted service (<see cref="EnlistmentState.EnlistedBattle"/>)
/// and the player does not lead the battle side. Detached-duty battles are the player's own
/// business (their duty spawned the fight) and keep vanilla roles.
/// </summary>
public static class BattleCommandPolicy
{
    public static bool ShouldStripPlayerCommand(EnlistmentState state, bool playerLeadsBattleSide)
        => state == EnlistmentState.EnlistedBattle && !playerLeadsBattleSide;
}
