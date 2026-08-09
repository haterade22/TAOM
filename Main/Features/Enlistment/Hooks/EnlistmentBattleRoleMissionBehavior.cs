using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Logging;

namespace TAOM.Features.Enlistment.Hooks;

/// <summary>
/// Strips the enlisted player's battlefield command (#424). Vanilla's
/// <c>AssignPlayerRoleInTeamMissionController.AfterStart</c> makes him the GENERAL of his
/// whole side because <c>IsPlayerSergeant()</c> needs <c>Army != null</c> and enlistment
/// keeps Army permanently null. <c>: MissionLogic</c> — NEVER MissionBehavior
/// (BehaviorTreeMissionLogic regression rule). Registered UNCONDITIONALLY from SubModule;
/// all filtering happens inside. SubModule-added behaviors run after the mission's own
/// controllers, so this AfterStart executes after vanilla's role assignment.
///
/// Deliberately <c>SetPlayerRole(false, false)</c> — option (a) from #424 — rather than the
/// SAS sergeant-score rig, which mutates campaign-level battle leadership
/// (<c>GetCharacterSergeantScore</c> feeds <c>GetLeaderOfMapEvent</c>).
///
/// NEITHER-ROLE IS A SUPPORTED VANILLA STATE, not untested ground. <c>BehaviorComponent</c>
/// (v1.4.7, <c>:105</c>) branches on exactly it:
/// <c>if (!Team.IsPlayerGeneral &amp;&amp; !Team.IsPlayerSergeant &amp;&amp; Formation.IsPlayerTroopInFormation
/// &amp;&amp; Mission.Current.MainAgent != null)</c> — the "player is a soldier inside a formation
/// receiving orders" path. That branch is what this correction makes reachable, and it is the
/// enlistment fantasy stated in engine code.
///
/// ORDER OF BATTLE is unaffected, but NOT for the reason first written here (the original
/// comment claimed the model "reads no player-role flag" — it reads one). The real mechanism,
/// from <c>SandboxBattleInitializationModel.CanPlayerSideDeployWithOrderOfBattleAux()</c>:
/// deployment is offered only if the player leads the side, owns the besieged settlement, or
/// <c>playerMapEvent.IsPlayerSergeant()</c>. For an enlisted player all three are false — the
/// commander leads, and IsPlayerSergeant is false because Army is null — so the model returns
/// false and <c>DeploymentMissionController</c> calls <c>FinishDeployment()</c> immediately.
/// The OOB screen was ALREADY unreachable while enlisted, before this class existed, which is
/// why the ordering question (does this AfterStart beat deployment setup?) does not arise.
///
/// The in-game F1–F8 observation is still owed and tracked on #424.
/// </summary>
public class EnlistmentBattleRoleMissionBehavior : MissionLogic
{
    private readonly IEnlistmentStateQuery _query;
    private readonly IModLogger _logger;

    private bool _applied;

    public EnlistmentBattleRoleMissionBehavior(IEnlistmentStateQuery query, IModLogger logger)
    {
        _query = query;
        _logger = logger;
    }

    public override void AfterStart() => TryStripCommand("AfterStart");

    // Belt for missions with a deployment phase: role flags are re-derived around
    // deployment, and a second idempotent pass costs nothing when _applied is set.
    public override void OnDeploymentFinished() => TryStripCommand("DeploymentFinished");

    private void TryStripCommand(string site)
    {
        if (_applied || Campaign.Current == null)
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
        _applied = true;
        _logger?.LogInfo(
            $"[Enlistment] battle command stripped at {site} — enlisted soldier, side led by " +
            $"'{sideLeader?.Id ?? "unknown"}' (#424)");
    }
}
