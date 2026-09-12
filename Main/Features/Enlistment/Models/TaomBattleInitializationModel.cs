using SandBox.GameComponents;

namespace TAOM.Features.Enlistment.Models;

/// <summary>
/// Keeps the Order of Battle deployment screen shut while the player is an enlisted soldier in
/// his commander's battle (#576).
///
/// Second consumer of the #443 army join. With <c>MainParty.Army</c> non-null for the battle,
/// <c>MapEvent.IsPlayerSergeant()</c> is true, which is the third arm of
/// <c>SandboxBattleInitializationModel.CanPlayerSideDeployWithOrderOfBattleAux</c> (installed
/// 1.4.8, <c>:78</c>). The screen then let the player drop himself on a formation;
/// <c>AssignPlayerRoleInTeamMissionController.AssignSergeant</c> set <c>Formation.PlayerOwner</c>,
/// whose setter turns <c>IsAIControlled</c> off (<c>Formation.cs:271-282</c>), while TAOM's
/// <c>(false, false)</c> roles block the order UI (<c>MissionOrderVM.cs:797</c>). Nobody could
/// command that formation.
///
/// Why this slot: <c>BattleInitializationModel.CanPlayerSideDeployWithOrderOfBattle()</c> is
/// NON-virtual and caches the Aux answer once per mission (cleared by <c>InitializeModel</c> at
/// <c>DefaultBattleMissionAgentSpawnLogic.OnBehaviorInitialize</c>, first read at
/// <c>DeploymentMissionController.SetupTeams:181</c>). The deployment controller,
/// <c>AssignPlayerRoleInTeamMissionController.OnPlayerTeamDeployed:66</c> and
/// <c>GeneralsAndCaptainsAssignmentLogic.OnTeamDeployed:49</c> all read that one cached bool, so
/// the Aux override dominates every path. Subclassing the SandBox model rather than the abstract
/// base keeps <c>GetAllAvailableTroopTypes</c> and the sally-out rule vanilla
/// (<c>TaomBattleBannerBearersModel</c> precedent).
///
/// The disabled path is literally <c>base</c>: the service answers null for every battle that is
/// not enlisted service, including the player's own.
/// </summary>
public class TaomBattleInitializationModel : SandboxBattleInitializationModel
{
    private readonly IEnlistmentDeploymentService _service;

    public TaomBattleInitializationModel(IEnlistmentDeploymentService service)
    {
        _service = service;
    }

    protected override bool CanPlayerSideDeployWithOrderOfBattleAux() =>
        _service.CanPlayerSideDeployWithOrderOfBattle() ?? base.CanPlayerSideDeployWithOrderOfBattleAux();
}
