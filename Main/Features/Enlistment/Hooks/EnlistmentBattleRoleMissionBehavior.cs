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
/// ORDER OF BATTLE — this paragraph previously concluded "unaffected", and that conclusion no
/// longer holds. The mechanism, from
/// <c>SandboxBattleInitializationModel.CanPlayerSideDeployWithOrderOfBattleAux()</c>: deployment is
/// offered if the player leads the side, owns the besieged settlement, or
/// <c>playerMapEvent.IsPlayerSergeant()</c>. The old reasoning was that all three are false while
/// enlisted, because Army is null. Army is no longer null — it is joined for the duration of a
/// battle so the player deploys with his commander's troops rather than in a lone block 20 units
/// behind them (#443) — so the third clause is now TRUE and the OOB screen IS reachable.
///
/// That is the intended outcome at the top of the ladder and only there:
/// <see cref="BattleCommandPolicy.ShouldKeepSergeantCommand"/> lets a rank-3 Sergeant keep the one
/// formation vanilla offers him, and strips every rank below back to a private in the line. It also
/// re-checks <c>Team.IsPlayerSergeant</c> rather than trusting rank, because the merge is
/// best-effort and a failed merge puts vanilla back on the general-of-the-side path.
///
/// The in-game F1–F8 observation is still owed and tracked on #424, and now also covers whether the
/// deployment screen appears for a Sergeant.
/// </summary>
public class EnlistmentBattleRoleMissionBehavior : MissionLogic
{
    private readonly IEnlistmentStateQuery _query;
    private readonly Content.IEnlistmentContentStore _content;
    private readonly IModLogger _logger;

    private bool _applied;

    public EnlistmentBattleRoleMissionBehavior(
        IEnlistmentStateQuery query, Content.IEnlistmentContentStore content, IModLogger logger)
    {
        _query = query;
        _content = content;
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

        // Read the role vanilla actually granted, BEFORE overwriting it. Team.IsPlayerSergeant is
        // the engine's own verdict, so this distinguishes "the army merge worked and he was offered
        // a formation" from "the merge failed and he was made general of the side" — which look
        // identical from rank alone.
        if (BattleCommandPolicy.ShouldKeepSergeantCommand(_content.Record.Rank, team.IsPlayerSergeant))
        {
            _applied = true;
            _logger?.LogInfo(
                $"[Enlistment] battle command KEPT at {site} — rank {_content.Record.Rank} " +
                "commands one formation under the side's leader");
            return;
        }

        team.SetPlayerRole(false, false);
        _applied = true;
        // NAME WHAT THE ID ACTUALLY IS. MapEvent.GetLeaderParty returns MapEventSide.LeaderParty,
        // which the engine sets to whichever party OPENED that side and only reassigns if that
        // party leaves. So on a side several parties joined it is routinely an allied lord, not the
        // player's commander and not the mission team's general. Reading it as "the player is on
        // the wrong team" is a false #443 sighting, which is exactly how the 2026-08-12 field log
        // was misread. The real #443 signal is ServiceBattleService's "army merge unavailable".
        _logger?.LogInfo(
            $"[Enlistment] battle command stripped at {site} — enlisted soldier, player's side " +
            $"opened by '{sideLeader?.Id ?? "unknown"}' (map-event side initiator, not the mission " +
            "team leader) (#424)");
    }
}
