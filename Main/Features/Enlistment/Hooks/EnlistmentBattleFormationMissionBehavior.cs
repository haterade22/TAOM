using System;
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
/// by <c>Formation.AddUnit</c> when the added agent <c>IsPlayerTroop</c> (Formation.cs:2117-2119),
/// and the <c>Agent.Formation</c> setter routes through <c>AddUnit</c> (Agent.cs:1143) — the
/// <c>ElephantMissionBehavior</c> idiom.
///
/// CORRECTION (2026-08-09, post-merge review): this RELOCATES that flag, it does not deliver it.
/// <c>Agent.Build</c> already assigns <c>Formation = agentBuildData?.AgentFormation</c>
/// (Agent.cs:5173) and <c>Mission.SpawnAgent</c> calls <c>BuildAgent</c> BEFORE the
/// <c>OnAgentBuild</c> dispatch loop (Mission.cs:4348 then :4360), so the player is already in a
/// formation and the flag is already true when this runs. #441's "member of no formation" premise
/// is wrong. What this actually buys is the formation matching the player's CHOSEN assignment
/// rather than their equipment, plus the reposition.
///
/// AND IT IS NOT YET THE LORD'S LINE — see #443. An enlisted player is alone on
/// <c>Mission.PlayerTeam</c> while the commander's troops land on <c>Mission.PlayerAllyTeam</c>,
/// because <c>MainParty.Army</c> is permanently null so <c>IsInSameArmyAsPlayer</c> is false
/// (Mission.GetAgentTeam:5183-5189). The formation joined here has no one else in it.
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

        // GUARDED, and the reason is the dispatch shape rather than distrust of the code below.
        // Mission.SpawnAgent runs `foreach (MissionBehavior mb in MissionBehaviors) mb.OnAgentBuild(...)`
        // with no per-behavior try (Mission.cs:4357-4360), so a throw here aborts the whole spawn
        // wave and every TAOM behavior registered after this one silently never sees the agent.
        // Placement is cosmetic; taking the battle down for it is not a trade worth making. Same
        // shape as the sibling ElephantMissionBehavior:61-66.
        try
        {
            Place(agent);
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

    private void Place(Agent agent)
    {
        var mapEvent = MobileParty.MainParty?.MapEvent;
        if (mapEvent == null)
            return;

        var playerLeads = mapEvent.GetLeaderParty(mapEvent.PlayerSide) == PartyBase.MainParty;
        if (!BattleCommandPolicy.ShouldStripPlayerCommand(_query.State, playerLeads))
            return;

        var targetClass = BattleFormationPolicy.TargetFormationFor(_contentStore.Record.Assignment);
        if (targetClass == null)
            return;

        // agent.Team, with no PlayerTeam fallback. Team is always assigned before OnAgentBuild is
        // dispatched, so the fallback was dead — and on the one path that could have reached it, it
        // was WRONG: an enlisted player's own party routes to PlayerTeam while the commander's
        // party routes to PlayerAllyTeam (Mission.GetAgentTeam:5183-5189), so silently substituting
        // PlayerTeam for a missing team would place the agent on a team he is not on.
        var team = agent.Team;
        if (team == null)
            return;

        var formation = team.GetFormation(targetClass.Value);
        if (formation == null)
            return;

        // Already there — vanilla assigned this same class at Agent.Build (Agent.cs:5173), which
        // runs BEFORE this dispatch. The setter would early-return on identity anyway
        // (Agent.cs:1105-1108), but the teleport below would still fire and the log would still
        // claim a placement, so bail explicitly rather than narrating a move that did not happen.
        if (agent.Formation == formation)
            return;

        // Non-player units only. CountOfUnits is `Arrangement.UnitCount + _detachedUnits.Count`
        // (Formation.cs:182) and counts the player himself once he is in it, so "does this
        // formation have a line to join" has to exclude him or a formation of one reads as a line.
        var hasLine = formation.CountOfDetachableNonPlayerUnits > 0;

        agent.Formation = formation;
        _applied = true;

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
                    $"[Enlistment] {targetClass.Value} formation order position is not finite ({target}) — " +
                    "joined the formation but kept vanilla spawn placement");
            }
        }

        _logger?.LogInfo(
            $"[Enlistment] soldier placed in the {targetClass.Value} formation" +
            $"{(repositioned ? " at its position" : " (no line to join yet — walking)")} (#441)");
    }

    private static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
}
