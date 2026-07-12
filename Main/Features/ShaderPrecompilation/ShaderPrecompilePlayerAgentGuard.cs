using System;
using System.Reflection;
using HarmonyLib;
using TAOM.Core.Logging;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.ShaderPrecompilation;

// 1.4.7 headless-deployment guard, scoped to the shader-precompile battle. Added ONLY while a walk is
// in flight (SubModule.OnMissionBehaviorInitialize, gated on ShaderPrecompileRunner.IsWalkInProgress),
// so it can NEVER affect a real battle. Two jobs, both because the precompile battle has NO human
// player and so takes the engine's deployment path with a null InitialPlayerAgent:
//
// 1. SEED InitialPlayerAgent (OnAgentBuild). 1.4.7 added an unconditional deref of
//    Mission.InitialPlayerAgent to DeploymentMissionController.SetupTeams() and .FinishDeployment()
//    (the new AgentControllerType hand-control lines). That field (Mission._initialPlayerAgent) is set
//    ONLY when an agent builds with Controller==Player (Mission.cs:4024); a headless battle spawns no
//    player-controlled agent, so it stays null and SetupTeams NREs every tick (the "stuck on 1.4.7"
//    crash; 1.4.6 didn't touch the field). The first agent build happens synchronously inside
//    SetupTeams (OnSetupTeamsOfSide -> SetSpawnTroops(enforceSpawning:true) -> CheckDeployment) BEFORE
//    the deref, so seeding the field there makes the deref find a non-null agent and harmlessly
//    reconfigure it in the throwaway battle. Reflection because the field is private with no setter;
//    drift-guarded by ReflectionSiteBindingTests.
//
// 2. FORCE-FINISH deployment (OnMissionTick). The all-characters battle has >=20 player troops, so
//    CanPlayerSideDeployWithOrderOfBattle() is true and the engine opens the Order-of-Battle deployment
//    VIEW and waits for the player to click "Deploy". Headless, nobody clicks -> the game hangs at the
//    deployment screen forever (confirmed in-game 2026-07-11: after the NRE was fixed, the walk froze at
//    the OoB view for hours, tick dead). Once SetupTeams has run (TeamSetupOver), we call the controller's
//    public FinishDeployment() ourselves so the battle proceeds straight to rendering + shader compile —
//    exactly what a <20-troop scene pass gets automatically via SetupTeams' auto-finish. FinishDeployment
//    also derefs InitialPlayerAgent, but job (1) has already seeded it. A scene pass whose deployment
//    already auto-finished has no controller left, so this is a no-op there.
//
// Fail-safe throughout: any failure logs once and latches so it never retry-spams. The one residual is
// engine drift — if the private field is renamed (AccessTools.Field returns null) or SetValue throws, the
// seed can't happen and the engine's own deref still NREs. That drift is caught at TEST time by
// ReflectionSiteBindingTests (the real guard against it) — this runtime catch only keeps the walk from
// spamming logs, it does not make a drifted field safe.
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
            if (mission == null || mission.InitialPlayerAgent != null) { _seeded = true; return; }
            if (InitialPlayerAgentField == null)
            {
                _logger?.LogWarning("[ShaderPrecompilation] Mission._initialPlayerAgent field not found — 1.4.7 deployment NRE guard inactive");
                _seeded = true;
                return;
            }
            InitialPlayerAgentField.SetValue(mission, agent);
            _seeded = true;
            _logger?.LogInfo($"[ShaderPrecompilation] seeded InitialPlayerAgent (agent index {agent.Index}) so 1.4.7 deployment SetupTeams won't NRE on the headless battle");
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
            // No controller (a scene pass whose SetupTeams already auto-finished, or a non-deployment
            // mission) — nothing to force. Wait until SetupTeams has run (troops spawned + seed done)
            // before finishing, so we don't cut in ahead of the engine's own setup.
            if (deployment == null) { _deploymentHandled = true; return; }
            if (!deployment.TeamSetupOver) return;
            _deploymentHandled = true;
            deployment.FinishDeployment();
            _logger?.LogInfo("[ShaderPrecompilation] force-finished deployment (headless battle — no player to click Deploy) so the item can render + compile");
        }
        catch (Exception ex)
        {
            _logger?.LogWarning($"[ShaderPrecompilation] force-finish deployment failed: {ex.Message}");
            _deploymentHandled = true;  // best-effort; the per-item timeout still bounds a stuck item
        }
    }
}
