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
/// (<c>GetCharacterSergeantScore</c> feeds <c>GetLeaderOfMapEvent</c>). Neither-role is not
/// a state vanilla produces in campaign; the OOB gate was checked for this
/// (<c>BattleInitializationModel.CanPlayerSideDeployWithOrderOfBattle</c> reads no player-role
/// flag), and the in-game F1–F8 observation is tracked on #424.
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

        var playerLeads = mapEvent.GetLeaderParty(mapEvent.PlayerSide) == PartyBase.MainParty;
        if (!BattleCommandPolicy.ShouldStripPlayerCommand(_query.State, playerLeads))
            return;

        var team = Mission?.PlayerTeam;
        if (team == null)
            return;

        team.SetPlayerRole(false, false);
        _applied = true;
        _logger?.LogInfo(
            $"[Enlistment] battle command stripped at {site} — enlisted soldier, side led by " +
            $"'{mapEvent.GetLeaderParty(mapEvent.PlayerSide)?.Id ?? "unknown"}' (#424)");
    }
}
