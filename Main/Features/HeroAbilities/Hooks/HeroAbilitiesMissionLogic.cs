using System;
using System.Threading;
using BehaviorTreeWrapper;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.LongDefeat.Diagnostics;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.HeroAbilities.Hooks;

/// <summary>
/// Mission entry point for the hero abilities (#1359, docs/features/hero-abilities.md): the gate, the one-second pass, the
/// engine events and the end-of-mission check lines. The work is <see cref="HeroAbilityHost"/>'s and
/// <see cref="HeroAbilityCaster"/>'s, the rules <see cref="HeroAbilityService"/>'s. The gate is decided on the first tick
/// (OnBehaviorInitialize never runs for a behavior a feature module adds, #606): a Long Defeat mission (the story
/// mission holds that logic too), a combat mission, the JSON switch and at least one kit. The battle phase is read on every tick. MUST be
/// <c>: MissionLogic</c> (rca-looter-battle-nre-2026-05-24.md). Boundary code, game-tested (ADR-008).
/// </summary>
public sealed class HeroAbilitiesMissionLogic : MissionLogic
{
    private static int _missions;

    private readonly HeroAbilityRuntime _runtime;
    private readonly HeroAbilityHost _host;
    private readonly DeferredCallbackQueue _deferred;
    private readonly string _key;
    private bool? _eligible;
    // Set on the main thread; the removal callback reads it from any thread.
    private volatile bool _scanned;
    private bool _disabled;
    private float _nextPass;

    public HeroAbilitiesMissionLogic(HeroAbilityRuntime runtime)
    {
        _runtime = runtime;
        _key = "ha" + Interlocked.Increment(ref _missions);
        _host = new HeroAbilityHost(runtime, new HeroAbilityCaster(runtime, _key));
        // The reporter runs on the calling thread: the file log only.
        _deferred = new DeferredCallbackQueue(runtime.Logger.LogWarning);
    }

    public override void OnMissionTick(float dt)
    {
        base.OnMissionTick(dt);
        MissionThreadGuard.MarkMainThread();
        if (_disabled || !(_eligible ??= DecideEligible()))
            return;
        // A throw here would become a crash report every frame: fail once, then stand down.
        try
        {
            _deferred.Drain();
            if (Mission.Mode == MissionMode.Battle)
                Tick(Mission.CurrentTime);
        }
        catch (Exception ex)
        {
            _disabled = true;
            _runtime.Logger.LogError($"[HeroAbilities] disabled for this mission after {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            StandDown();
        }
    }

    // No pass runs after a stand-down, so the host ends the buffs it gave now.
    private void StandDown()
    {
        _host.StandDown();
    }

    private bool DecideEligible()
    {
        var (combat, longDefeat) = HeroMissionGate.Read(Mission);
        var config = _runtime.Service.ConfigEnabled;
        var kits = _runtime.Service.KitCount;
        if (longDefeat)
            LongDefeatChecks.Record(_runtime.Outcomes, HeroAbilityChecks.GateId, _key, HeroAbilityChecks.Gate(longDefeat, combat, config, kits));
        var eligible = HeroMissionGate.IsHeroMission(combat, longDefeat) && config && kits > 0;
        _runtime.Logger.LogInfo($"[HeroAbilities] mission gate: eligible={eligible} long_defeat={longDefeat} combat={combat} json={config} kits={kits}");
        return eligible;
    }

    private void Tick(float now)
    {
        // A positive requirement: an inverted early exit would admit NaN.
        if (!(now >= _nextPass))
            return;
        _nextPass = now + HeroAbilityService.PassSeconds;
        if (!_scanned)
        {
            _scanned = true;
            foreach (var agent in Mission.AllAgents)
                _host.TryRegister(agent);
        }
        _host.RunAll(Mission, now);
        _host.Expire(now);
    }

    public override void OnAgentBuild(Agent agent, Banner banner)
    {
        base.OnAgentBuild(agent, banner);
        // Before the first scan, that scan finds this agent.
        if (!_scanned || _disabled)
            return;
        // Inside Mission.SpawnAgent a throw breaks the spawn (lesson #595): stand down instead.
        try
        {
            _host.TryRegister(agent);
        }
        catch (Exception ex)
        {
            _disabled = true;
            _runtime.Logger.LogError($"[HeroAbilities] disabled for this mission after {ex.GetType().Name} in OnAgentBuild: {ex.Message}");
            StandDown();
        }
    }

    // Native may raise this off the main thread (#634): the ledger's writer state is main-thread only, so the work waits
    // for the next tick there.
    public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow)
    {
        base.OnAgentRemoved(affectedAgent, affectorAgent, agentState, blow);
        if (!_scanned || affectedAgent == null)
            return;
        _deferred.RunOrDefer("HeroAbilitiesMissionLogic.OnAgentRemoved", () => _host.Forget(affectedAgent));
    }

    public override void OnRemoveBehavior()
    {
        if (_scanned)
        {
            _runtime.Logger.LogInfo($"[HeroAbilities] Mission end: {_host.Count} hero body(ies) registered");
            _host.WriteChecks(_key);
        }
        _host.Clear();
        _deferred.Clear();
        base.OnRemoveBehavior();
    }
}
