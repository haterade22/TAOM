using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Logging;

namespace TAOM.Features.Enlistment.Hooks;

/// <summary>
/// Strips the enlisted player's battlefield command (#424, #576). <c>: MissionLogic</c>, NEVER
/// MissionBehavior (BehaviorTreeMissionLogic regression rule). Registered UNCONDITIONALLY from
/// SubModule; all filtering happens inside. SubModule-added behaviors run after the mission's own
/// controllers, so this AfterStart executes after vanilla's role assignment.
///
/// WHAT VANILLA HANDS US. Since the #443 army join, <c>MainParty.Army</c> is non-null for the
/// battle, so <c>MapEvent.IsPlayerSergeant()</c> is true and <c>SandBoxMissions</c> wires
/// <c>AssignPlayerRoleInTeamMissionController(isPlayerGeneral: false, isPlayerSergeant: true)</c>.
/// When the merge fails (#495: a commander with no kingdom gets no army) Army stays null and
/// vanilla makes him GENERAL of his whole side instead. Either way vanilla's
/// <c>AssignPlayerRoleInTeamMissionController.AfterStart</c> has run by the time this one does,
/// and this one overwrites it with <c>SetPlayerRole(false, false)</c>: neither general nor
/// sergeant, at every rank.
///
/// NEITHER-ROLE IS A SUPPORTED VANILLA STATE, not untested ground. <c>BehaviorComponent</c>
/// (installed 1.4.8, <c>:103-110</c>) branches on exactly it:
/// <c>if (!Team.IsPlayerGeneral &amp;&amp; !Team.IsPlayerSergeant &amp;&amp; Formation.IsPlayerTroopInFormation
/// &amp;&amp; Mission.Current.MainAgent != null)</c>, the "player is a soldier inside a formation
/// receiving orders" path. <c>Team.SetPlayerRole</c> also calls <c>SetControlledByAI(true)</c>
/// on every formation for this pair, and <c>MissionOrderVM.CheckCanBeOpened</c> refuses the
/// order UI on it, which is the enlistment fantasy stated in engine code.
///
/// ONE PASS AT AFTERSTART IS FINAL. <c>SetPlayerRole</c> has exactly two engine call sites
/// (<c>Mission.cs:741-746</c> at team creation and
/// <c>AssignPlayerRoleInTeamMissionController.cs:43</c> at AfterStart), both before this one.
/// Nothing re-derives the role flags around deployment; an earlier <c>OnDeploymentFinished</c>
/// belt here claimed otherwise and was dead code (it also dispatches in REVERSE behavior order,
/// so it would have run before the deployment finalize it meant to follow).
///
/// THE ORDER OF BATTLE SCREEN IS NOT THIS CLASS'S JOB, and it cannot be. The engine decides
/// deployment from campaign state (<c>IsPlayerSergeant()</c>), never from <c>Team.IsPlayerGeneral</c>,
/// so the strip cannot close it; worse, <c>IsPlayerGeneral == false</c> is what routes
/// <c>OnPlayerTeamDeployed</c> into the sergeant-choice UI that let the soldier captain a formation
/// nobody could then command (#576). <c>TaomBattleInitializationModel</c> keeps that screen shut
/// through the same <see cref="BattleCommandPolicy"/> gate, so the two cannot gate apart.
///
/// Deliberately NOT the ServeAsSoldier sergeant-score rig, which mutates campaign-level battle
/// leadership (<c>GetCharacterSergeantScore</c> feeds <c>GetLeaderOfMapEvent</c>).
/// </summary>
public class EnlistmentBattleRoleMissionBehavior : MissionLogic
{
    private readonly IEnlistmentStateQuery _query;
    private readonly IModLogger _logger;

    public EnlistmentBattleRoleMissionBehavior(IEnlistmentStateQuery query, IModLogger logger)
    {
        _query = query;
        _logger = logger;
    }

    public override void AfterStart() => TryStripCommand();

    private void TryStripCommand()
    {
        if (Campaign.Current == null)
            return;

        var mapEvent = MobileParty.MainParty?.MapEvent;
        if (mapEvent == null)
            return;

        var sideLeader = mapEvent.GetLeaderParty(mapEvent.PlayerSide);
        if (!BattleCommandPolicy.ShouldStripPlayerCommand(_query.State, sideLeader == PartyBase.MainParty))
            return;

        var team = Mission?.PlayerTeam;
        if (team == null)
            return;

        team.SetPlayerRole(false, false);
        // NAME WHAT THE ID ACTUALLY IS. MapEvent.GetLeaderParty returns MapEventSide.LeaderParty,
        // which the engine sets to whichever party OPENED that side and only reassigns if that
        // party leaves. So on a side several parties joined it is routinely an allied lord, not the
        // player's commander and not the mission team's general. Reading it as "the player is on
        // the wrong team" is a false #443 sighting, which is exactly how the 2026-08-12 field log
        // was misread. The real #443 signal is ServiceBattleService's "army merge unavailable".
        _logger?.LogInfo(
            "[Enlistment] battle command stripped at AfterStart: enlisted soldier, player's side " +
            $"opened by '{sideLeader?.Id ?? "unknown"}' (map-event side initiator, not the mission " +
            "team leader) (#424)");
    }
}
