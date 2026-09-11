using System;
using System.Reflection;
using HarmonyLib;
using TAOM.Core.Logging;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.ShaderPrecompilation;

// Deployment fallback guard for the shader-precompile battles. Added ONLY while a walk is in flight
// (SubModule.OnMissionBehaviorInitialize, gated on ShaderPrecompileRunner.TryClaimMission), so it can
// NEVER affect a real battle.
//
// The engine dereferences Mission.InitialPlayerAgent unconditionally in DeploymentMissionController
// .SetupTeams and .FinishDeployment (1.4.7 added the AgentControllerType hand-control lines; 1.4.8
// still has them). That field is written in exactly one place, Mission.BuildAgent, when an agent builds
// with Controller == Player, which Mission.SpawnTroop requests for the troop equal to
// Game.Current.PlayerTroop spawning on the player side. Since #560 every shader battle takes that
// vanilla shape (a one-entry player party holding the designated PlayerCharacter, which always gets an
// initial spawn slot), so on the normal path the engine sets the field itself and this guard does
// nothing. Two fallbacks remain, for a walk that did not take that shape (a future roster edit, an
// engine change to the spawn split):
//
// 1. SEED InitialPlayerAgent (OnAgentBuild): the first PLAYER-TEAM agent built while the field is still
//    null is written into it by reflection (private field, no setter; drift-guarded by
//    ReflectionSiteBindingTests). Player team only: SetupTeams spawns the ENEMY side first, so "the first
//    agent built" is an enemy agent, and seeding that one would make it the player team's general
//    (GeneralsAndCaptainsAssignmentLogic reads InitialPlayerAgent for the player team) and later the
//    player-controlled MainAgent. OnSetupTeamsOfSide(PlayerSide) runs before the deref, so a player-team
//    seed still lands in time.
// 2. FORCE-FINISH deployment (OnMissionTick): if the player party ever holds 20 or more entries again,
//    CanPlayerSideDeployWithOrderOfBattle() opens the Order of Battle view and waits for a click nobody
//    makes. Once SetupTeams has run (TeamSetupOver) the controller's public FinishDeployment() is called.
//    (In Custom Battle the base OrderOfBattleVM's SaveConfiguration() is empty, so nothing is written to
//    the player's profile either way; the one-entry party is the shape because it keeps the view from
//    opening at all.) A battle whose deployment auto-finished has no controller left, so this is a no-op.
//
// Fail-safe throughout: any failure logs once and latches so it never retry-spams. The residual is
// engine drift (field renamed, SetValue throws): the binding test catches it at test time; the runtime
// catch only keeps the walk from spamming logs.
public sealed class ShaderPrecompilePlayerAgentGuard : MissionLogic
{
    private static readonly FieldInfo InitialPlayerAgentField =
        AccessTools.Field(typeof(Mission), "_initialPlayerAgent");

    private readonly IModLogger _logger;
    private bool _seeded;
    private bool _deploymentHandled;

    public ShaderPrecompilePlayerAgentGuard(IModLogger logger) => _logger = logger;

    public override void OnAgentBuild(Agent agent, Banner banner)
    {
        if (_seeded || agent == null) return;
        try
        {
            var mission = Mission.Current;
            if (mission == null) return;
            if (mission.InitialPlayerAgent != null) { _seeded = true; return; }   // the engine did it: the normal path
            // The enemy side spawns first; only a player-team agent may stand in for the player.
            if (agent.Team == null || mission.PlayerTeam == null || agent.Team != mission.PlayerTeam) return;
            if (InitialPlayerAgentField == null)
            {
                _logger?.LogWarning("[ShaderPrecompilation] Mission._initialPlayerAgent field not found; the deployment fallback seed is inactive");
                _seeded = true;
                return;
            }
            InitialPlayerAgentField.SetValue(mission, agent);
            _seeded = true;
            _logger?.LogWarning($"[ShaderPrecompilation] FALLBACK: seeded InitialPlayerAgent with player-team agent {agent.Index}; " +
                                "the engine did not flag the designated player character, check the item's player party");
        }
        catch (Exception ex)
        {
            _logger?.LogWarning($"[ShaderPrecompilation] InitialPlayerAgent seed failed: {ex.Message}");
            _seeded = true;  // never retry-spam; the walk's timeouts/cancel still bound a stuck item
        }
    }

    public override void OnMissionTick(float dt)
    {
        if (_deploymentHandled) return;
        try
        {
            var mission = Mission.Current;
            if (mission == null) return;
            var deployment = mission.GetMissionBehavior<DeploymentMissionController>();
            // No controller (deployment already auto-finished, the normal path, or a non-deployment
            // mission): nothing to force. Otherwise wait until SetupTeams has run before finishing, so
            // we never cut in ahead of the engine's own setup.
            if (deployment == null) { _deploymentHandled = true; return; }
            if (!deployment.TeamSetupOver) return;
            _deploymentHandled = true;
            deployment.FinishDeployment();
            _logger?.LogWarning("[ShaderPrecompilation] FALLBACK: force-finished deployment (the player party reached the Order of Battle threshold; nobody can click Deploy in this battle)");
        }
        catch (Exception ex)
        {
            _logger?.LogWarning($"[ShaderPrecompilation] force-finish deployment failed: {ex.Message}");
            _deploymentHandled = true;  // best-effort; the per-item timeout still bounds a stuck item
        }
    }
}
