using System;
using System.Collections.Generic;
using System.Threading;
using BehaviorTrees;
using BehaviorTreeWrapper;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.CreatureBandits.Diagnostics;
using TAOM.Features.CreatureBandits.Hooks;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.CreatureBandits;

/// <summary>
/// Mission side of Creature Bandits (#692), in three parts.
///
/// <b>The creature's tree.</b> Attaches <see cref="CreatureBanditBehaviorTree"/> to every creature bandit
/// (<see cref="CreatureBanditAgents.Is"/>), the troll's wiring: registered on the first mission tick, a first-tick scan,
/// then late attach from <c>OnAgentBuild</c>, which the spawner raises after it has set the creature's Character, so the
/// fingerprint already matches. SpiderMissionBehavior skips creature bandits, so no spider carries both trees (the tree
/// framework would overwrite its entry and leave both ticking). MUST stay <c>: MissionLogic</c>
/// (BehaviorTreeMissionLogicInheritanceTests); it never ticks a tree itself.
///
/// <b>The routed-count backstop.</b> The one removal SandBox does not count: a mount removed
/// as routed with no attacker returns early in <c>BattleAgentLogic.OnAgentRemoved</c> (v1.5.3 line 147), before
/// <c>Origin.SetRouted</c>, and a creature counted as spawned but never as removed keeps its side from depleting, so
/// the battle cannot end. Only a creature route A skipped is still a mount; an unmounted one SandBox counts itself.
/// Patch93 stops the panic and the rout that would lead there; this counts one that slips through.
/// <c>PartyGroupAgentOrigin</c> ignores a second removal, so a repeat is harmless.
///
/// <b>The scoreboard.</b> <see cref="CreatureScoreboardBridge"/> reports each creature to the battle observer, which
/// counts humans only, so a brood's troops, casualties and kills show like any troop's.
///
/// The engine may raise <c>OnAgentRemoved</c> off the main thread (#634); this touches no TAOM collection but the
/// scoreboard bridge's concurrent set of added creatures, makes the same origin and observer calls vanilla makes from
/// the same callback, and reports through the diagnostics' concurrent queue.
/// </summary>
public class CreatureBanditMissionBehavior : MissionLogic
{
    private readonly IModLogger _logger;
    private readonly CreatureTreeTracker _tracker;
    private readonly CreatureScoreboardBridge _scoreboard = new();
    private int _scoreboardErrorLogged;
    private readonly HashSet<string> _loggedErrors = new();
    private bool _initialized;
    private bool _treesAdded;

    public CreatureBanditMissionBehavior(IModLogger logger)
    {
        _logger = logger;
        _tracker = new CreatureTreeTracker(CreatureBanditsConfig.TreeName, "[CreatureBandits]", CreatureBanditAgents.Is, logger);
    }

    public override void OnMissionTick(float dt)
    {
        try
        {
            if (!_initialized)
            {
                _initialized = true;
                BTRegister.RegisterClass(CreatureBanditsConfig.TreeName, objects => CreatureBanditBehaviorTree.BuildTree(objects));
                if (BTRegister.Logger == null)
                    BTRegister.AddLogger(new TaomBTLogger());
            }
            if (!_treesAdded)
            {
                _treesAdded = true;
                int count = _tracker.AttachAll(Mission.Current.AllAgents);
                _logger.LogInfo($"[CreatureBandits] Attached behavior trees to {count} creature(s)");
            }
            _tracker.PruneDead();
            _scoreboard.Flush(Mission);
        }
        catch (Exception ex)
        {
            if (_loggedErrors.Add($"{ex.GetType().Name}:{ex.TargetSite?.Name}"))
                _logger.LogError($"[CreatureBandits] OnMissionTick error: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
        }
    }

    public override void OnAgentBuild(Agent agent, Banner banner)
    {
        base.OnAgentBuild(agent, banner);
        // Late attach, once the first tick has registered the tree; earlier creatures are caught by the first-tick scan.
        if (_treesAdded)
            _tracker.TryLateAttach(agent);
        if (CreatureBanditAgents.Is(agent))
            Guarded(() => _scoreboard.OnBuilt(Mission, agent));
    }

    public override void OnRemoveBehavior()
    {
        // Logged so a creature standing idle is attributable: with no tree it is still targetable but never moves.
        if (_treesAdded)
            _logger.LogInfo($"[CreatureBandits] Mission end: {_tracker.LateAttachCount} tree(s) late-attached, " +
                $"{_tracker.AliveCount} creature(s) alive at end");
        _tracker.Clear();
        _scoreboard.Clear();
        _loggedErrors.Clear();
        base.OnRemoveBehavior();
    }

    public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow)
    {
        base.OnAgentRemoved(affectedAgent, affectorAgent, agentState, blow);
        Guarded(() => _scoreboard.OnRemoved(affectedAgent, affectorAgent, agentState));
        if (!CreatureBanditRules.ShouldBackstopRoutedRemoval(CreatureBanditAgents.Is(affectedAgent), affectedAgent.IsMount,
                agentState, hasAffector: affectorAgent != null))
            return;

        affectedAgent.Origin?.SetRouted(isOrderRetreat: false);
        Interlocked.Increment(ref CreatureBanditDiag.BackstopCount);
        CreatureBanditDiag.Enqueue(new CreatureDiagEvent(CreatureDiagEventKind.Backstopped, CreatureBanditDiag.SerialOf(affectedAgent),
            Mission?.CurrentTime ?? float.NaN, Thread.CurrentThread.ManagedThreadId, MissionThreadGuard.IsOnMainThread));
    }

    // The engine's behavior loops are unguarded (a throw skips every later behavior's callback): the scoreboard never
    // breaks one. Logged once per mission; any thread.
    private void Guarded(Action report)
    {
        try { report(); }
        catch (Exception ex)
        {
            if (Interlocked.Exchange(ref _scoreboardErrorLogged, 1) == 0)
                _logger.LogError($"[CreatureBandits] Scoreboard report failed: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
        }
    }
}
