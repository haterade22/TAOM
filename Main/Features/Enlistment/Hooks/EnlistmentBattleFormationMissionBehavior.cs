using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Logging;
using TAOM.Features.Enlistment.Content;

namespace TAOM.Features.Enlistment.Hooks;

/// <summary>
/// Puts the enlisted soldier IN a formation (#441) — the second half of the engine branch the
/// #424 role strip made reachable. BehaviorComponent (v1.4.7, :105) runs its
/// "player is a soldier inside a formation receiving orders" path only when the team has
/// neither player role AND <c>Formation.IsPlayerTroopInFormation</c> is true; that flag is set
/// by <c>Formation.AddUnit</c> when the added agent <c>IsPlayerTroop</c> (verified in the
/// decompile), so plain <c>agent.Formation =</c> assignment completes it — the
/// <c>ElephantMissionBehavior</c> idiom.
///
/// <c>: MissionLogic</c> — NEVER MissionBehavior (BehaviorTreeMissionLogic regression rule).
/// Registered UNCONDITIONALLY from SubModule; all filtering happens inside.
/// <c>OnAgentBuild</c> rather than AfterStart because the player agent does not exist yet at
/// AfterStart on the enlisted join path; <c>agent.IsPlayerTroop</c> is roster-derived and
/// already set at build time, and it is the exact flag AddUnit consults.
///
/// Repositioning is conservative: teleport to <c>Formation.OrderGroundPosition</c> only when
/// the order position is valid AND the formation already has units — an empty or orderless
/// formation keeps vanilla spawn placement and the player walks. A Cavalry-assigned soldier
/// without a mount still joins the cavalry formation; placement follows the assignment the
/// player chose, not their current horse.
/// </summary>
public class EnlistmentBattleFormationMissionBehavior : MissionLogic
{
    private readonly IEnlistmentStateQuery _query;
    private readonly IEnlistmentContentStore _contentStore;
    private readonly IModLogger _logger;

    private bool _applied;

    public EnlistmentBattleFormationMissionBehavior(
        IEnlistmentStateQuery query,
        IEnlistmentContentStore contentStore,
        IModLogger logger)
    {
        _query = query;
        _contentStore = contentStore;
        _logger = logger;
    }

    public override void OnAgentBuild(Agent agent, Banner banner)
    {
        if (_applied || agent == null || !agent.IsPlayerTroop || Campaign.Current == null)
            return;

        var mapEvent = MobileParty.MainParty?.MapEvent;
        if (mapEvent == null)
            return;

        var playerLeads = mapEvent.GetLeaderParty(mapEvent.PlayerSide) == PartyBase.MainParty;
        if (!BattleCommandPolicy.ShouldStripPlayerCommand(_query.State, playerLeads))
            return;

        var targetClass = BattleFormationPolicy.TargetFormationFor(_contentStore.Record.Assignment);
        if (targetClass == null)
            return;

        var team = agent.Team ?? Mission?.PlayerTeam;
        if (team == null)
            return;

        var formation = team.GetFormation(targetClass.Value);
        if (formation == null)
            return;

        var hadUnits = formation.CountOfUnits > 0;
        agent.Formation = formation;
        _applied = true;

        if (hadUnits && formation.OrderPositionIsValid)
            agent.TeleportToPosition(formation.OrderGroundPosition);

        _logger?.LogInfo(
            $"[Enlistment] soldier placed in the {targetClass.Value} formation" +
            $"{(hadUnits && formation.OrderPositionIsValid ? " at its position" : " (no line to join yet — walking)")} (#441)");
    }
}
