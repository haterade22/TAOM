using System;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.FordsOfWar;
using TAOM.Features.Stamina.Domain;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Stamina.Hooks;

/// <summary>
/// Mission boundary for stamina (#1497, docs/features/stamina.md). MUST be <c>: MissionLogic</c>
/// (rca-looter-battle-nre-2026-05-24.md). The gate (the MCM switch and the mission kind) is decided on the first tick:
/// OnBehaviorInitialize never runs for a behavior a feature module adds (#606). The rules run only while the mission
/// is in Battle mode, so nothing drains during deployment. The engine callbacks only queue or mark a soldier, or add
/// a stopped blow's cost to his cell, which is safe from any thread; the tick does the work. Co-op role AgentOwner:
/// the ticker asks CoopMissionOwnership.IsOwnedHere for each soldier before it acts on him.
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
        try
        {
            if (!(_armed ??= DecideArmed()) || Mission.Mode != MissionMode.Battle)
                return;
            _runtime.Ticker.Tick(Mission.CurrentTime, dt);
        }
        catch (Exception ex)
        {
            // A gate that throws gives this mission no stamina: left at null it would run again on every tick.
            _armed ??= false;
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
        _runtime.BeginMission();
        var enabled = _runtime.Settings.Enabled;
        var fords = FordsOfWarMission.IsActive(Mission);
        var armed = StaminaMissionGate.IsArmable(enabled, fords, Mission.CombatType == Mission.MissionCombatType.Combat,
            Mission.IsFieldBattle, Mission.IsSiegeBattle, Mission.IsSallyOutBattle);
        _runtime.MissionGate = $"{(armed ? "open" : "closed")} (MCM={enabled}, fords={fords}, combatType={Mission.CombatType}, teamAI={Mission.MissionTeamAIType})";
        _runtime.Logger.LogInfo($"[Stamina] mission gate: {_runtime.MissionGate}");
        StaminaChecks.Armed(_runtime.Outcomes, _runtime.MissionKey, enabled, armed);
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

    // The defender's cost of a stopped melee blow. The lookup by reference comes before any read of the agent, and a
    // mount has no cell. The engine reports a stopped blow here only, never through RegisterBlow, so it is charged once.
    public override void OnMeleeHit(Agent attacker, Agent victim, bool isCanceled, AttackCollisionData collisionData)
    {
        if (!_runtime.Armed || victim == null || !_runtime.Store.TryGet(victim, out var cell))
            return;
        var result = collisionData.CollisionResult;
        if (result != CombatCollisionResult.Blocked && result != CombatCollisionResult.Parried)
            return;
        var block = collisionData.AttackBlockedWithShield ? StaminaBlock.ShieldMelee : StaminaBlock.WeaponParry;
        Charge(cell, block, collisionData.InflictedDamage);
    }

    public override void OnMissileHit(Agent attacker, Agent victim, bool isCanceled, AttackCollisionData collisionData)
    {
        if (!_runtime.Armed || victim == null || !collisionData.AttackBlockedWithShield || !_runtime.Store.TryGet(victim, out var cell))
            return;
        Charge(cell, StaminaBlock.ShieldMissile, 0f);
    }

    // Any thread: an atomic add on the cell. The tick applies it, with the soldier's cost factor, at his next look.
    // A soldier the tick does not tire (an exempt people, an MCM opt-out, another peer's) pays and counts nothing.
    private void Charge(StaminaCell cell, StaminaBlock block, float shieldDamage)
    {
        if (!cell.Tracked)
            return;
        cell.AddPending(StaminaService.BlockCost(_runtime.Tuning.Economy, block, shieldDamage));
        _runtime.Telemetry.CountBlock();
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
                var telemetry = _runtime.Telemetry;
                _runtime.Logger.LogInfo($"[Stamina][Summary] {_runtime.Ticker.Describe()}");
                StaminaChecks.Lock(_runtime.Outcomes, _runtime.MissionKey, telemetry.Exhaustions, telemetry.Recoveries, telemetry.Stuck);
                StaminaChecks.Refresh(_runtime.Outcomes, _runtime.MissionKey, telemetry.Refreshes, telemetry.SlowestRefreshBatchMs);
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
