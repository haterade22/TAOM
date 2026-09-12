using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Logging;
using TAOM.Features.Enlistment.Content;

namespace TAOM.Features.Enlistment.Hooks;

/// <summary>
/// Puts the enlisted soldier IN the formation his service assignment names (#441), and keeps him
/// there after vanilla's captain and general assignment has run (#576). The engine work lives in
/// <see cref="EnlistedSoldierPlacement"/>; this class is the two hooks and their guards.
///
/// <c>: MissionLogic</c>, NEVER MissionBehavior (BehaviorTreeMissionLogic regression rule).
/// Registered UNCONDITIONALLY from SubModule; all filtering happens inside, on the same
/// <see cref="BattleCommandPolicy"/> gate as the role strip and the deployment-screen model, so
/// the three cannot gate apart. <c>OnAgentBuild</c> rather than AfterStart because the player
/// agent does not exist yet at AfterStart on the enlisted join path; <c>agent.IsPlayerTroop</c> is
/// roster-derived and already set at build time. <c>OnAfterDeploymentFinished</c> because the
/// engine dispatches it after every <c>OnDeploymentFinished</c> handler, which is where vanilla
/// moves him.
/// </summary>
public class EnlistmentBattleFormationMissionBehavior : MissionLogic
{
    private readonly EnlistedSoldierPlacement _placement;
    private readonly IModLogger _logger;

    private bool _applied;

    public EnlistmentBattleFormationMissionBehavior(
        IEnlistmentStateQuery query,
        IEnlistmentContentStore contentStore,
        IModLogger logger)
    {
        _placement = new EnlistedSoldierPlacement(query, contentStore, logger);
        _logger = logger;
    }

    public override void OnAgentBuild(Agent agent, Banner banner)
    {
        if (_applied || agent == null || !agent.IsPlayerTroop || Campaign.Current == null)
            return;

        // GUARDED, and the reason is the dispatch shape rather than distrust of the code below.
        // Mission.SpawnAgent runs `foreach (MissionBehavior mb in MissionBehaviors) mb.OnAgentBuild(...)`
        // with no per-behavior try (Mission.cs:4357-4360), so a throw here aborts the whole spawn
        // wave and every TAOM behavior registered after this one silently never sees the agent.
        // Placement is cosmetic; taking the battle down for it is not a trade worth making. Same
        // shape as the sibling ElephantMissionBehavior:61-66.
        try
        {
            if (_placement.Place(agent))
                _applied = true;
        }
        catch (Exception ex)
        {
            // Latch on failure too: a placement that threw once will throw again on the next agent,
            // and retrying it per-agent for the whole spawn wave turns one logged error into
            // hundreds.
            _applied = true;
            _logger?.LogError($"[Enlistment] formation placement failed, leaving vanilla placement: {ex.Message}");
        }
    }

    /// <summary>
    /// Vanilla's <c>GeneralsAndCaptainsAssignmentLogic</c> may have made the soldier a captain and
    /// moved him during deployment (#576). Undo both, under the same gate. Guarded for the same
    /// reason OnAgentBuild is: this dispatch has no per-behavior try either.
    /// <c>Mission.MainAgent</c> is non-null here on every deployment path:
    /// <c>DeploymentMissionController.FinishDeployment</c> sets
    /// <c>InitialPlayerAgent.Controller = Player</c> (:72-74), whose setter assigns
    /// <c>Mission.MainAgent</c> (Agent.cs:1221), before it dispatches this hook (:78).
    /// </summary>
    public override void OnAfterDeploymentFinished()
    {
        if (Campaign.Current == null)
            return;

        try
        {
            _placement.ReclaimAfterDeployment(Mission?.MainAgent);
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[Enlistment] after-deployment correction failed, leaving vanilla placement: {ex.Message}");
        }
    }
}
