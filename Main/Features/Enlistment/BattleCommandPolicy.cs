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
/// business and keep vanilla roles. (The "their duty spawned the fight" case this
/// once also covered is gone: since #428 a duty never detaches the player and so never
/// produces a battle of their own.)
/// </summary>
public static class BattleCommandPolicy
{
    public static bool ShouldStripPlayerCommand(EnlistmentState state, bool playerLeadsBattleSide)
        => state == EnlistmentState.EnlistedBattle && !playerLeadsBattleSide;

    /// <summary>
    /// Inside a battle this policy has already claimed, does the player nevertheless keep the ONE
    /// formation vanilla handed him? True only at the top of the ladder, and only when the engine
    /// itself made him a sergeant.
    ///
    /// The premise of the summary above has changed, and this method is the consequence. Army is no
    /// longer permanently null: it is now joined for the duration of a battle so the player deploys
    /// with his commander's troops instead of in a lone block behind them (#443). That makes
    /// <c>MapEvent.IsPlayerSergeant()</c> true, so vanilla stops promoting him to GENERAL and starts
    /// offering him a single formation — a role worth keeping once he has earned it.
    ///
    /// BOTH conditions are load-bearing. The merge is best-effort: a commander with no kingdom
    /// cannot have an army raised for him, Army stays null, and
    /// <c>AssignPlayerRoleInTeamMissionController</c> falls back to the old general-of-the-side
    /// assignment. Gating on rank alone would therefore hand a sergeant command of the entire army
    /// exactly when the merge failed — #424 again, wearing a promotion. So we also require that what
    /// the engine actually granted was the sergeant role, read off
    /// <c>Team.IsPlayerSergeant</c> after vanilla's assignment has run.
    /// </summary>
    public static bool ShouldKeepSergeantCommand(
        Content.Domain.ServiceRank rank, bool engineAssignedSergeantRole)
        => rank >= Content.Domain.ServiceRank.Sergeant && engineAssignedSergeantRole;
}
