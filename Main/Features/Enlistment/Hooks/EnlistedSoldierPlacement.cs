using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Logging;
using TAOM.Features.Enlistment.Content;

namespace TAOM.Features.Enlistment.Hooks;

/// <summary>
/// The engine-touching half of <see cref="EnlistmentBattleFormationMissionBehavior"/>: stands the
/// enlisted soldier in the formation his service assignment names, and puts him back there after
/// vanilla's own captain and general assignment has moved him (#441, #576). Isolated here for the
/// same reason <c>MeritGeometryScanner</c> is: the behavior is an ADR-002 entry point and this is
/// Agent/Formation/Team boundary work that no service may hold.
///
/// WHAT PLACEMENT ACTUALLY BUYS (2026-08-09 correction). <c>Agent.Build</c> already assigns
/// <c>Formation = agentBuildData?.AgentFormation</c> (Agent.cs:5173) and <c>Mission.SpawnAgent</c>
/// calls <c>BuildAgent</c> BEFORE the <c>OnAgentBuild</c> dispatch loop, so the player is already in
/// a formation and <c>IsPlayerTroopInFormation</c> is already true when this runs. What it buys is
/// the formation matching the assignment the player CHOSE rather than his equipment, plus the
/// reposition. Since the #443 army join his commander's troops share his team, so the formation
/// he joins is the lord's line.
///
/// Repositioning is conservative: teleport to <c>Formation.OrderGroundPosition</c> only when the
/// order position is valid AND the formation already has non-player units; an empty or orderless
/// formation keeps vanilla placement and the player walks. A Cavalry-assigned soldier without a
/// mount still joins the cavalry formation; placement follows the assignment, not the horse.
///
/// AND THEN VANILLA MOVES HIM AGAIN (#576). With the Order of Battle screen shut for an enlisted
/// soldier (<c>TaomBattleInitializationModel</c>), <c>GeneralsAndCaptainsAssignmentLogic.OnTeamDeployed</c>
/// runs <c>AssignBestCaptainsForTeam</c> over every hero on the player team, the player included
/// (installed 1.4.8, :131), and <c>OnCaptainAssignedToFormation</c> (:253-266) makes him captain of
/// the largest formation matching his mount state and MOVES him into it. Then
/// <c>OnDeploymentFinished</c> (:69-85) moves him into the general's formation whenever the team
/// has 50 or more members. Neither sets <c>PlayerOwner</c>, so orders keep flowing, but he is
/// captain of, and standing in, a formation that is not his assignment.
/// <see cref="ReclaimAfterDeployment"/> runs from <c>OnAfterDeploymentFinished</c>, after every
/// <c>OnDeploymentFinished</c> handler (<c>DeploymentMissionController.FinishDeployment:48</c> then
/// :78), clears any captaincy vanilla handed him, and re-runs the placement. The explicit clear is
/// load-bearing, not a belt: <c>Formation.RemoveUnit</c> nulls <c>Captain</c> only when the leaving
/// unit cannot lead formations remotely (Formation.cs:2193), and vanilla's <c>OnDeploymentFinished</c>
/// calls <c>SetCanLeadFormationsRemotely(true)</c> on the player before moving him, so moving him
/// out of a formation he captains leaves him its captain. Known cosmetic leftover: the formation
/// banner <c>BannerBearerLogic.SetFormationBanner</c> set for the brief captaincy stays.
/// </summary>
public sealed class EnlistedSoldierPlacement
{
    private readonly IEnlistmentStateQuery _query;
    private readonly IEnlistmentContentStore _contentStore;
    private readonly IModLogger _logger;

    public EnlistedSoldierPlacement(IEnlistmentStateQuery query, IEnlistmentContentStore contentStore, IModLogger logger)
    {
        _query = query;
        _contentStore = contentStore;
        _logger = logger;
    }

    /// <summary>
    /// The shared gate (<see cref="BattleCommandPolicy"/>): the battle was entered as enlisted
    /// service and the player does not lead his side. False when the main party is in no map event.
    /// </summary>
    public bool IsEnlistedSoldierBattle()
    {
        var mapEvent = MobileParty.MainParty?.MapEvent;
        if (mapEvent == null)
            return false;

        var playerLeads = mapEvent.GetLeaderParty(mapEvent.PlayerSide) == PartyBase.MainParty;
        return BattleCommandPolicy.ShouldStripPlayerCommand(_query.State, playerLeads);
    }

    /// <summary>Move the agent into his assignment's formation. Returns true when he was moved.</summary>
    public bool Place(Agent agent)
    {
        if (!IsEnlistedSoldierBattle())
            return false;

        var targetClass = BattleFormationPolicy.TargetFormationFor(_contentStore.Record.Assignment);
        if (targetClass == null)
            return false;

        // agent.Team, with no PlayerTeam fallback. Team is always assigned before OnAgentBuild is
        // dispatched, so a fallback would be dead, and on the merge-failed path (#495) it would be
        // WRONG: there the player's party routes to PlayerTeam while the commander's routes to
        // PlayerAllyTeam (Mission.GetAgentTeam), so substituting PlayerTeam for a missing team would
        // place the agent on a team he is not on.
        var team = agent.Team;
        if (team == null)
            return false;

        var formation = team.GetFormation(targetClass.Value);
        if (formation == null)
            return false;

        // Already there: the setter would early-return on identity anyway (Agent.cs:1106), but
        // the teleport below would still fire and the log would still claim a placement, so bail
        // explicitly rather than narrating a move that did not happen.
        if (agent.Formation == formation)
            return false;

        // Non-player units only. The sibling CountOfUnits (`Arrangement.UnitCount +
        // _detachedUnits.Count`, Formation.cs:182) counts the player himself once he is in it, so
        // "does this formation have a line to join" has to exclude him or a formation of one reads
        // as a line. CountOfDetachableNonPlayerUnits (Formation.cs:194) subtracts the player troop.
        var hasLine = formation.CountOfDetachableNonPlayerUnits > 0;

        agent.Formation = formation;

        var repositioned = false;
        if (hasLine && formation.OrderPositionIsValid)
        {
            var target = formation.OrderGroundPosition;

            // NaN gate, positive requirement (.claude/rules/csharp-architecture.md).
            // OrderPositionIsValid checks the 2D position and the scene pointer only; the Z comes
            // from GetGroundZ(), which returns NaN when it cannot validate, and TeleportToPosition
            // hands the vector straight to native SetPosition. Every comparison against NaN is
            // false, so this is written as "must be finite to proceed" rather than "skip if NaN".
            if (IsFinite(target.x) && IsFinite(target.y) && IsFinite(target.z))
            {
                agent.TeleportToPosition(target);
                repositioned = true;
            }
            else
            {
                _logger?.LogWarning(
                    $"[Enlistment] {targetClass.Value} formation order position is not finite ({target}), " +
                    "joined the formation but kept vanilla spawn placement");
            }
        }

        _logger?.LogInfo(
            $"[Enlistment] soldier placed in the {targetClass.Value} formation" +
            $"{(repositioned ? " at its position" : " (no line to join yet, walking)")} (#441)");
        return true;
    }

    /// <summary>
    /// Undo what vanilla's captain and general assignment did to the soldier during deployment
    /// (#576): clear any captaincy it handed him and stand him back in his assignment's formation.
    /// </summary>
    public void ReclaimAfterDeployment(Agent agent)
    {
        if (agent == null || !IsEnlistedSoldierBattle())
            return;

        var team = agent.Team;
        if (team == null)
            return;

        var cleared = 0;
        foreach (var formation in team.FormationsIncludingSpecialAndEmpty)
        {
            if (formation.Captain != agent)
                continue;

            // Null is a value the engine itself writes (captain death, unit removal), so this is
            // the supported way to say "no captain" rather than a guess.
            formation.Captain = null;
            cleared++;
        }

        var before = agent.Formation?.FormationIndex;
        Place(agent);
        var after = agent.Formation?.FormationIndex;

        if (cleared > 0 || before != after)
            _logger?.LogInfo(
                $"[Enlistment] after deployment: cleared {cleared} captaincy vanilla handed the soldier, " +
                $"formation {before?.ToString() ?? "none"} -> {after?.ToString() ?? "none"} (#576)");
    }

    private static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
}
