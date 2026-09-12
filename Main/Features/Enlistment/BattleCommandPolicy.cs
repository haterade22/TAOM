using TAOM.Features.Enlistment.Domain;

namespace TAOM.Features.Enlistment;

/// <summary>
/// Decides whether the enlisted player holds ANY battlefield command (#424, #576).
///
/// One predicate, three consumers, and they must never gate apart:
/// <list type="bullet">
///   <item><c>EnlistmentDeploymentService</c>, through <c>TaomBattleInitializationModel</c>: keeps
///   the Order of Battle deployment screen shut, so there is no captain slot to take.</item>
///   <item><c>EnlistmentBattleRoleMissionBehavior</c>: <c>Team.SetPlayerRole(false, false)</c>, so
///   the order UI stays closed and every formation is AI-controlled.</item>
///   <item><c>EnlistmentBattleFormationMissionBehavior</c>: stands the soldier in the formation his
///   service assignment names, and puts him back there after vanilla's own captain and general
///   assignment has moved him.</item>
/// </list>
///
/// Strip command exactly when the battle was entered as enlisted service
/// (<see cref="EnlistmentState.EnlistedBattle"/>) and the player does not lead the battle side.
/// Detached-duty battles are the player's own business and keep vanilla roles. (The "their duty
/// spawned the fight" case this once also covered is gone: since #428 a duty never detaches the
/// player and so never produces a battle of their own.)
///
/// There is deliberately no rank input. The 2026-08-12 army join (#443) made
/// <c>MapEvent.IsPlayerSergeant()</c> true, and a rank-3 Sergeant was allowed to keep the one
/// formation vanilla then offered. That carve-out is what opened the deployment screen to every
/// rank and let a soldier captain a formation nobody could then command (#576). No rank holds a
/// command while enlisted.
/// </summary>
public static class BattleCommandPolicy
{
    public static bool ShouldStripPlayerCommand(EnlistmentState state, bool playerLeadsBattleSide)
        => state == EnlistmentState.EnlistedBattle && !playerLeadsBattleSide;
}
