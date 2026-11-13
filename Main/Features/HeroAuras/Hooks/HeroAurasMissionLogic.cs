using System;
using TAOM.Features.SignatureStrikes.Hooks;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.HeroAuras.Hooks;

/// <summary>
/// Mission entry point for the hero auras (#1127, docs/features/hero-auras.md): the gate, the pulse clock, the engine
/// events and the end-of-battle log line. The work is <see cref="HeroAuraPulse"/>'s, the rules <see cref="HeroAuraService"/>'s.
/// Gate, decided on the first tick (OnBehaviorInitialize never runs for a behavior a feature module adds, #606): the JSON
/// switch, SignatureMissionGate (combat missions, Custom Battle included, no multiplayer) and the issue's battle types, a
/// field battle, siege or sally-out (hideouts and raids are NoTeamAI, so they fall out). Read on every tick: the battle
/// phase (Mode is Deployment while the player places formations, as DreadMissionGate notes). The MCM switch is read on
/// every pulse; while it is off no soldier is stamped and the auras already given linger out. MUST be
/// <c>: MissionLogic</c> (rca-looter-battle-nre-2026-05-24.md). Boundary code, game-tested (ADR-008).
/// </summary>
public sealed class HeroAurasMissionLogic : MissionLogic
{
    internal const string ReachedCheckId = "taom.hero-auras.reached";
    private const float WarmupSeconds = 1f;

    private readonly HeroAuraRuntime _runtime;
    private readonly HeroAuraPulse _pulse;
    private bool? _eligible;
    private bool _scanned;
    private bool _disabled;
    private float _nextPulse;

    public HeroAurasMissionLogic(HeroAuraRuntime runtime)
    {
        _runtime = runtime;
        _pulse = new HeroAuraPulse(runtime);
    }

    public override void OnMissionTick(float dt)
    {
        base.OnMissionTick(dt);
        if (_disabled || !(_eligible ??= DecideEligible()) || Mission.Mode != MissionMode.Battle)
            return;
        // A throw here would become a crash report every frame: fail once, then stand down.
        try
        {
            Tick(Mission.CurrentTime);
        }
        catch (Exception ex)
        {
            _disabled = true;
            _runtime.Logger.LogError($"[HeroAuras] disabled for this mission after {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            StandDown();
        }
    }

    // No tick runs after a stand-down, so nothing would expire the auras already given: end them all now.
    private void StandDown()
    {
        try
        {
            _pulse.Expire(float.MaxValue);
        }
        catch (Exception ex)
        {
            _runtime.Ledger.Clear();
            _runtime.Logger.LogError($"[HeroAuras] stand-down refresh failed, auras cleared: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private bool DecideEligible()
    {
        var json = _runtime.Service.ConfigEnabled;
        var mission = Mission;
        var battle = mission != null && (mission.IsFieldBattle || mission.IsSiegeBattle || mission.IsSallyOutBattle);
        var eligible = json && battle && SignatureMissionGate.IsEligible(mission);
        _runtime.Logger.LogInfo($"[HeroAuras] mission gate: eligible={eligible} json={json} battle={battle} MCM={_runtime.Settings.Enabled} combatType={mission?.CombatType}");
        return eligible;
    }

    private void Tick(float now)
    {
        // Positive requirements: an inverted early exit would admit NaN.
        if (!(now >= WarmupSeconds) || !(now >= _nextPulse))
            return;
        _nextPulse = now + _runtime.Service.PulseSeconds;
        if (!_scanned)
        {
            _scanned = true;
            foreach (var agent in Mission.AllAgents)
                _pulse.TryRegister(agent);
            if (_pulse.SourceCount > 0)
                _runtime.Logger.LogInfo($"[HeroAuras] heroes with an aura on the field: {_pulse.DescribeSources()}");
        }

        _pulse.Prune();
        if (_runtime.Settings.Enabled)
            _pulse.Run(Mission, now);
        _pulse.Expire(now);
    }

    public override void OnAgentBuild(Agent agent, Banner banner)
    {
        base.OnAgentBuild(agent, banner);
        // Before the first scan, that scan finds this agent; Custom Battle spawns its armies after it.
        if (!_scanned || _disabled)
            return;
        // Inside Mission.SpawnAgent a throw breaks the spawn (lesson #595): stand down instead, as the Dread Aura does.
        try
        {
            _pulse.TryRegister(agent);
        }
        catch (Exception ex)
        {
            _disabled = true;
            _runtime.Logger.LogError($"[HeroAuras] disabled for this mission after {ex.GetType().Name} in OnAgentBuild: {ex.Message}");
            StandDown();
        }
    }

    // Native may raise this off the main thread (#634): the ledger is concurrent, and nothing else is touched here.
    public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow)
    {
        base.OnAgentRemoved(affectedAgent, affectorAgent, agentState, blow);
        if (affectedAgent != null)
            _runtime.Ledger.Remove(affectedAgent);
    }

    public override void OnRemoveBehavior()
    {
        var reach = _runtime.Ledger.ReachLines();
        if (_scanned && (_pulse.SourceCount > 0 || reach.Count > 0))
            _runtime.Logger.LogInfo($"[HeroAuras] Mission end: {(reach.Count == 0 ? "no soldier reached" : string.Join("; ", reach))}");
        if (reach.Count > 0)
            _runtime.Outcomes.Pass(ReachedCheckId, "", $"auras={reach.Count} soldiers={_runtime.Ledger.ReachTotal}");
        _runtime.Ledger.Clear();
        _pulse.Clear();
        base.OnRemoveBehavior();
    }
}
