using System;
using System.Collections.Generic;
using BehaviorTrees;
using BehaviorTreeWrapper;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.SignatureStrikes.Hooks;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.RaceAbilities.Hooks;

/// <summary>
/// Mission boundary for the race abilities: attaches a <see cref="RaceAbilityBehaviorTree"/> to every
/// soldier with an ability profile and no tree yet (RaceAbilityRuntime.CarriesTree), ages the abilities each
/// tick through the runtime's ticker, hands it every death, and writes the battle's report at the end. The
/// troll's wiring: MUST be <c>: MissionLogic</c> (rca-looter-battle-nre-2026-05-24.md), and the trees tick
/// from BehaviorTreeMissionLogic.OnMissionTick, never from here. The gate (the MCM switch, the JSON switch,
/// combat missions only, the signature strikes' rule) is decided on the first tick: OnBehaviorInitialize
/// never runs for a behavior a feature module adds (#606). Custom Battle spawns its armies after that first
/// tick, so most trees arrive as late spawns.
/// </summary>
public class RaceAbilitiesMissionLogic : MissionLogic
{
    private const string TreeName = "RaceAbilityTree";
    // The shadow list only bounds its own growth; each tree retires itself on removal, so a rare prune is enough.
    private const float PruneSeconds = 5f;

    private readonly RaceAbilityRuntime _runtime;
    private readonly CreatureTreeTracker _tracker;
    private readonly DeferredCallbackQueue _deferred;
    private readonly HashSet<string> _loggedErrors = new HashSet<string>();
    private bool? _eligible;
    // Set on the main thread once the trees are attached; the engine callbacks read it as their gate.
    private volatile bool _treesAdded;
    private bool _registered;
    private float _nextPrune;

    public RaceAbilitiesMissionLogic(RaceAbilityRuntime runtime)
    {
        _runtime = runtime;
        _tracker = new CreatureTreeTracker(TreeName, "[RaceAbilities]", runtime.CarriesTree, runtime.Logger);
        // The reporter runs on the calling thread: the file log only.
        _deferred = new DeferredCallbackQueue(runtime.Warn);
    }

    public override void OnMissionTick(float dt)
    {
        MissionThreadGuard.MarkMainThread();
        if (!(_eligible ??= DecideEligible()))
            return;
        try
        {
            if (!_registered)
            {
                _registered = true;
                BTRegister.RegisterClass(TreeName, objects => RaceAbilityBehaviorTree.BuildTree(objects));
            }

            _deferred.Drain();

            if (!_treesAdded)
            {
                _treesAdded = true;
                var count = 0;
                foreach (var agent in Mission.AllAgents)
                    if (_tracker.TryAttach(agent))
                    {
                        count++;
                        _runtime.CountTree(agent);
                    }
                _runtime.Logger.LogInfo($"[RaceAbilities] Attached ability trees to {count} soldier(s) at the first tick; " +
                    "later spawns attach as they arrive (Custom Battle spawns its armies after this tick)");
            }

            var now = Mission.CurrentTime;
            if (now >= _nextPrune)
            {
                _nextPrune = now + PruneSeconds;
                _tracker.PruneDead();
            }
            _runtime.Ticker.Tick(now);
        }
        catch (Exception ex)
        {
            string key = $"{ex.GetType().Name}:{ex.TargetSite?.Name}";
            if (_loggedErrors.Add(key))
                _runtime.Logger.LogError($"[RaceAbilities] OnMissionTick error: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private bool DecideEligible()
    {
        var mcm = _runtime.Settings.Enabled;
        var json = _runtime.Resolver.ConfigEnabled;
        var eligible = mcm && json && SignatureMissionGate.IsEligible(Mission);
        _runtime.MissionGate = $"{(eligible ? "open" : "closed")} (MCM={mcm}, json={json}, combatType={Mission?.CombatType})";
        _runtime.Logger.LogInfo($"[RaceAbilities] mission gate: eligible={eligible} MCM={mcm} json={json} combatType={Mission?.CombatType}");
        return eligible;
    }

    public override void OnAgentBuild(Agent agent, Banner banner)
    {
        base.OnAgentBuild(agent, banner);
        // Late spawns (deployment, reinforcements, every Custom Battle soldier): only after the first tick
        // registered the tree, which it does only in an eligible mission.
        if (_treesAdded && _tracker.TryLateAttach(agent))
            _runtime.CountTree(agent);
    }

    // Native may raise this off the main thread (#634). What the runtime needs is read now, while the agent
    // still owns its slot, and the work is deferred to the next tick. A horse has no Character: no soldier.
    public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow)
    {
        base.OnAgentRemoved(affectedAgent, affectorAgent, agentState, blow);
        if (!_treesAdded || affectedAgent == null)
            return;
        var soldier = affectedAgent.Character != null;
        var died = agentState == AgentState.Killed || agentState == AgentState.Unconscious;
        var killer = affectorAgent != null && affectorAgent.IsMount ? affectorAgent.RiderAgent : affectorAgent;
        var position = affectedAgent.Position.AsVec2;
        var team = affectedAgent.Team;
        var race = affectedAgent.IsHuman ? affectedAgent.Character?.Race : null;
        var culture = affectedAgent.IsHuman ? affectedAgent.Character?.Culture?.StringId : null;
        var now = Mission.CurrentTime;
        _deferred.RunOrDefer("RaceAbilitiesMissionLogic.OnAgentRemoved",
            () => _runtime.Deaths.OnAgentRemoved(affectedAgent, soldier, killer, died, position, team, race, culture, now));
    }

    public override void OnRemoveBehavior()
    {
        if (_treesAdded)
        {
            _runtime.Logger.LogInfo($"[RaceAbilities] Mission end: {_tracker.LateAttachCount} tree(s) late-attached, " +
                $"{_tracker.AliveCount} soldier(s) tracked at end");
            _runtime.LogReport("Mission end");
        }
        _runtime.Clear();
        _tracker.Clear();
        _deferred.Clear();
        base.OnRemoveBehavior();
    }
}
