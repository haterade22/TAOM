using System;
using BehaviorTreeWrapper;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.HeroAbilities.Domain;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.HeroAbilities.Hooks;

/// <summary>
/// Mission entry point for the hero balance tally (docs/features/hero-abilities.md, "Balance tally"): for each body of a
/// hero listed in hero_abilities.json, the enemies he downs, the health he takes and loses, how long he lives and what
/// fells him. It writes one [HeroAbilities][Combat] line when a body leaves the battle, one for each body still standing
/// at mission end, and one summary for each hero. It changes nothing in the fight. The removal callback reaches behaviors
/// off the main thread (#634), so hits and removals wait for the tick there. Same gate as <see cref="HeroAbilitiesMissionLogic"/>.
/// MUST be <c>: MissionLogic</c> (rca-looter-battle-nre-2026-05-24.md). Boundary code, game-tested (ADR-008).
/// </summary>
public sealed class HeroCombatLogic : MissionLogic
{
    private const string Tag = "[HeroAbilities][Combat]";

    private readonly HeroAbilityRuntime _runtime;
    private readonly HeroCombatLedger<Agent> _ledger = new HeroCombatLedger<Agent>();
    private readonly DeferredCallbackQueue _deferred;
    private bool? _eligible;
    // Set on the main thread; the callbacks read them from any thread to skip work when no hero is listed.
    private volatile bool _disabled;
    private volatile bool _any;
    private float _nextSample;
    // The battle's result time: the mission runs on for a few seconds after it, which would thin the rates.
    private float? _resultAt;

    public HeroCombatLogic(HeroAbilityRuntime runtime)
    {
        _runtime = runtime;
        _deferred = new DeferredCallbackQueue(runtime.Logger.LogWarning);
    }

    public override void OnAgentBuild(Agent agent, Banner banner)
    {
        base.OnAgentBuild(agent, banner);
        if (_disabled || _eligible == false || agent == null || !agent.IsHuman || agent.Character == null)
            return;
        // Inside Mission.SpawnAgent a throw breaks the spawn (lesson #595): stand down instead.
        try
        {
            var kit = _runtime.Service.KitFor(agent.Character.StringId);
            if (kit == null)
                return;
            _ledger.Add(agent, kit.CharacterId, Mission.CurrentTime).NoteHealth(agent.HealthLimit);
            _any = true;
        }
        catch (Exception ex)
        {
            LogAndDisable(ex, "OnAgentBuild");
        }
    }

    // OnScoreHit carries the health the blow removed, capped at the victim's health (Agent.cs:5503-5507, Mission.cs:5672);
    // OnAgentHit carries the raw blow, overkill included. A blocked hit removes none.
    public override void OnScoreHit(Agent affectedAgent, Agent affectorAgent, WeaponComponentData attackerWeapon, bool isBlocked, bool isSiegeEngineHit, in Blow blow,
        in AttackCollisionData collisionData, float damagedHp, float hitDistance, float shotDifficulty)
    {
        base.OnScoreHit(affectedAgent, affectorAgent, attackerWeapon, isBlocked, isSiegeEngineHit, in blow, in collisionData, damagedHp, hitDistance, shotDifficulty);
        if (!_any || _disabled || affectedAgent == null || isBlocked || !(damagedHp >= 0.5f))
            return;
        var damage = (int)Math.Round(damagedHp);
        var onEnemy = affectorAgent != null && affectedAgent.IsHuman && IsEnemy(affectorAgent, affectedAgent);
        _deferred.RunOrDefer("HeroCombatLogic.OnScoreHit", () =>
        {
            if (onEnemy)
                _ledger.Dealt(affectorAgent!, damage);
            _ledger.Taken(affectedAgent, damage);
        });
    }

    public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow)
    {
        base.OnAgentRemoved(affectedAgent, affectorAgent, agentState, blow);
        if (!_any || _disabled || affectedAgent == null)
            return;
        var downed = agentState == AgentState.Killed || agentState == AgentState.Unconscious;
        var kill = downed && affectorAgent != null && affectedAgent.IsHuman && IsEnemy(affectorAgent, affectedAgent);
        var killer = affectorAgent?.Character?.StringId ?? "none";
        var kind = !blow.IsValid ? "none" : blow.IsMissile ? "missile" : "melee";
        var health = downed ? 0f : affectedAgent.Health;
        var now = Mission.CurrentTime;
        _deferred.RunOrDefer("HeroCombatLogic.OnAgentRemoved", () =>
        {
            if (kill)
                _ledger.Kill(affectorAgent!);
            var fell = _ledger.Fell(affectedAgent, now, killer, kind, downed);
            if (fell != null)
                _runtime.Logger.LogInfo($"{Tag} {fell.Line(now, health)} state_engine={agentState}");
        });
    }

    public override void OnMissionResultReady(MissionResult missionResult)
    {
        base.OnMissionResultReady(missionResult);
        _resultAt ??= Mission.CurrentTime;
    }

    public override void OnMissionTick(float dt)
    {
        base.OnMissionTick(dt);
        MissionThreadGuard.MarkMainThread();
        if (_disabled)
            return;
        try
        {
            if (_eligible == null)
                Decide();
            if (_eligible != true)
                return;
            _deferred.Drain();
            var now = Mission.CurrentTime;
            if (!(now >= _nextSample))
                return;
            _nextSample = now + HeroAbilityService.PassSeconds;
            // No fall recorded means he stands: no native read through a handle that may be stale.
            _ledger.Sample(a => a.HealthLimit, Mission.MainAgent);
        }
        catch (Exception ex)
        {
            LogAndDisable(ex, "OnMissionTick");
        }
    }

    public override void OnRemoveBehavior()
    {
        if (_eligible == true && !_disabled && _ledger.Count > 0)
        {
            _deferred.Drain();
            foreach (var line in _ledger.EndLines(_resultAt ?? Mission.CurrentTime, a => a.Health))
                _runtime.Logger.LogInfo($"{Tag} {line}");
        }
        _ledger.Clear();
        _deferred.Clear();
        base.OnRemoveBehavior();
    }

    private void Decide()
    {
        var (combat, longDefeat) = HeroMissionGate.Read(Mission);
        _eligible = HeroMissionGate.IsHeroMission(combat, longDefeat) && _runtime.Service.ConfigEnabled;
        if (_eligible == true)
            return;
        _ledger.Clear();
        _any = false;
    }

    private static bool IsEnemy(Agent a, Agent b) => a.Team != null && b.Team != null && a.Team.IsEnemyOf(b.Team);

    private void LogAndDisable(Exception ex, string site)
    {
        _disabled = true;
        _deferred.Clear();
        _runtime.Logger.LogError($"{Tag} disabled for this mission after {ex.GetType().Name} in {site}: {ex.Message}");
    }
}
