using System;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.FordsOfWar;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Stamina.Hooks;

/// <summary>
/// Mission boundary for stamina (#1497, docs/features/stamina.md). MUST be <c>: MissionLogic</c>
/// (rca-looter-battle-nre-2026-05-24.md). The gate (the MCM switch and the mission kind) is decided on the first tick:
/// OnBehaviorInitialize never runs for a behavior a feature module adds (#606). The engine callbacks only queue or mark
/// a soldier, which is safe from any thread; the tick does the work. Co-op role AgentOwner: the ticker asks
/// CoopMissionOwnership.IsOwnedHere for each soldier before it acts on him.
/// </summary>
public class StaminaMissionLogic : MissionLogic
{
    private readonly StaminaRuntime _runtime;
    private bool? _armed;
    private bool _errorLogged;

    public StaminaMissionLogic(StaminaRuntime runtime) => _runtime = runtime;

    public override void OnMissionTick(float dt)
    {
        MissionThreadGuard.MarkMainThread();
        if (!(_armed ??= DecideArmed()))
            return;
        try
        {
            _runtime.Ticker.Tick(Mission.CurrentTime, dt);
        }
        catch (Exception ex)
        {
            if (_errorLogged)
                return;
            _errorLogged = true;
            _runtime.Logger.LogError($"[Stamina] OnMissionTick error: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private bool DecideArmed()
    {
        // The runtime is a singleton: nothing of an earlier mission may reach this one.
        _runtime.Clear();
        var enabled = _runtime.Settings.Enabled;
        var fords = FordsOfWarMission.IsActive(Mission);
        var armed = StaminaMissionGate.IsArmable(enabled, fords, Mission.CombatType == Mission.MissionCombatType.Combat,
            Mission.IsFieldBattle, Mission.IsSiegeBattle, Mission.IsSallyOutBattle);
        _runtime.MissionGate = $"{(armed ? "open" : "closed")} (MCM={enabled}, fords={fords}, combatType={Mission.CombatType}, teamAI={Mission.MissionTeamAIType})";
        _runtime.Logger.LogInfo($"[Stamina] mission gate: {_runtime.MissionGate}");
        if (!armed)
            return false;
        foreach (var agent in Mission.AllAgents)
            Consider(agent);
        _runtime.Arm();
        return true;
    }

    // A soldier is a human agent that is not a mount; horses and creature mounts get no stamina.
    private void Consider(Agent agent)
    {
        if (agent != null && agent.IsHuman && !agent.IsMount)
            _runtime.Store.Enqueue(agent);
    }

    public override void OnAgentBuild(Agent agent, Banner banner)
    {
        base.OnAgentBuild(agent, banner);
        if (_runtime.Armed)
            Consider(agent);
    }

    // Native may raise the two removal callbacks off the main thread (#634): one concurrent remove and one flag.
    public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow)
    {
        base.OnAgentRemoved(affectedAgent, affectorAgent, agentState, blow);
        if (_runtime.Armed && affectedAgent != null)
            _runtime.Store.MarkRemoved(affectedAgent);
    }

    public override void OnAgentDeleted(Agent affectedAgent)
    {
        base.OnAgentDeleted(affectedAgent);
        if (_runtime.Armed && affectedAgent != null)
            _runtime.Store.MarkRemoved(affectedAgent);
    }

    public override void OnRemoveBehavior()
    {
        try
        {
            if (_armed == true)
            {
                // The claims go first, so nothing after this line can keep them from going.
                _runtime.Ticker.Teardown();
                _runtime.Logger.LogInfo($"[Stamina] Mission end: {_runtime.Ticker.Describe()}");
            }
        }
        catch (Exception ex)
        {
            _runtime.Logger.LogError($"[Stamina] the mission end failed: {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            _runtime.Clear();
            base.OnRemoveBehavior();
        }
    }
}
