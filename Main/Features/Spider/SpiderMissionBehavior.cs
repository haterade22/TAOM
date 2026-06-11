using System;
using System.Collections.Generic;
using BehaviorTrees;
using BehaviorTreeWrapper;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.Spider.BehaviorTreeElements;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Spider;

/// <summary>
/// Mission boundary for the RIDDEN giant spider. Attaches a per-agent <see cref="SpiderBehaviorTree"/>
/// (via a <c>BehaviorTreeAgentComponent</c>) to every spider-MOUNT agent in the battle — the elephant's
/// exact wiring (<c>ElephantMissionBehavior</c>). The attach key is <c>Monster.StringId == "spider"</c>,
/// NEVER the character id: the horse-slot mount agent's Character is the goblin RIDER
/// (`taom_spider_creature`), not the spider. The rider's cavalry AI drives movement; the BT layers the
/// bite. SpatialGrid/bone-collision ticking is owned by <c>AdvancedCombatBehavior</c> (always
/// co-registered in SubModule and ticking every frame) — the old conditional fallback here was dead code.
/// </summary>
public class SpiderMissionBehavior : MissionLogic
{
    private readonly ISpiderAttackService _service;
    private readonly IModLogger _logger;
    private readonly HashSet<string> _loggedErrors = new();
    // Shadow list of attached BT components, for dead-agent pruning. The engine's Agent.Tick auto-ticks
    // each component (v1.4.5) — never tick them manually; only drop dead spiders from this list.
    private readonly List<(Agent agent, BehaviorTreeAgentComponent comp)> _spiderComponents = new();
    private bool _initialized;
    private bool _treesAdded;
    // Custom-battle deployment spawns AFTER the first mission tick, so the first-tick scan
    // typically reports 0 and every spider arrives via OnAgentBuild — count those for visibility
    // (the "0 spider(s)" scan line otherwise reads like an attach failure).
    private int _lateAttachCount;

    public SpiderMissionBehavior()
    {
        _service = IoC.Resolve<ISpiderAttackService>();
        _logger = IoC.Resolve<IModLogger>();
    }

    private void Initialize()
    {
        _initialized = true;
        BTRegister.RegisterClass("SpiderTree", (object[] objects) => SpiderBehaviorTree.BuildTree(objects));
        if (BTRegister.Logger == null)
            BTRegister.AddLogger(new TaomBTLogger());

        // Armory-drift guard: the bite clip names live in the EXTERNAL LOTRLOME action_types.xml and
        // ActionIndexCache resolves eagerly — a rename there silently yields act_none, and playing act_none
        // on channel 0 kills the locomotion cycle (the elephant "slide" class). Detect at mission start.
        if (SpiderAttackActions.AnyUnresolved())
            _logger.LogError(
                "[Spider] One or more bite actions resolved to act_none — LOTRLOME action_types drift? " +
                $"Expected {SpiderConfig.BiteStandActionName}/{SpiderConfig.BiteChargeActionName}. " +
                "Bites will not animate correctly.");

        _logger.LogInfo("[Spider] Initialized");
    }

    public override void OnMissionTick(float dt)
    {
        try
        {
            if (!_initialized) Initialize();

            if (!_treesAdded)
            {
                _treesAdded = true;
                int count = 0;
                foreach (Agent a in Mission.Current.AllAgents)
                    if (TryAttachSpiderTree(a)) count++;
                _logger.LogInfo($"[Spider] Attached behavior trees to {count} spider(s)");
            }

            // Prune dead spiders from the shadow list (Agent.Tick auto-ticks the components).
            for (int i = _spiderComponents.Count - 1; i >= 0; i--)
                if (!_spiderComponents[i].agent.IsActive())
                    _spiderComponents.RemoveAt(i);
        }
        catch (Exception ex)
        {
            string key = $"{ex.GetType().Name}:{ex.TargetSite?.Name}";
            if (_loggedErrors.Add(key))
                _logger.LogError($"[Spider] OnMissionTick error: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
        }
    }

    public override void OnAgentBuild(Agent agent, Banner banner)
    {
        base.OnAgentBuild(agent, banner);
        // Late-spawn attach: only after Initialize registered "SpiderTree" (first OnMissionTick).
        // Spiders that built before that are caught by the first-tick scan.
        if (_treesAdded && TryAttachSpiderTree(agent))
        {
            _lateAttachCount++;
            if (_lateAttachCount == 1)
                _logger.LogInfo("[Spider] First late-spawn tree attached (deployment spawns after the first tick)");
        }
    }

    // Attaches a SpiderBehaviorTree component to the agent if it is a spider mount not already tracked.
    // Returns true when a tree was newly attached. Safe pre/post the first-tick scan (de-duplicates).
    private bool TryAttachSpiderTree(Agent agent)
    {
        if (agent == null || !_service.IsSpiderMonster(agent.Monster?.StringId)) return false;
        for (int i = 0; i < _spiderComponents.Count; i++)
            if (_spiderComponents[i].agent == agent) return false;   // already attached

        var comp = new BehaviorTreeAgentComponent(agent, "SpiderTree", Array.Empty<object>());
        agent.AddComponent(comp);
        if (comp.Tree != null)
        {
            _spiderComponents.Add((agent, comp));
            return true;
        }
        _logger.LogError($"[Spider] BT build failed for {agent.Name} (Rider={agent.RiderAgent?.Name ?? "null"})");
        return false;
    }

    public override void OnRemoveBehavior()
    {
        if (_treesAdded)
            _logger.LogInfo($"[Spider] Mission end: {_lateAttachCount} tree(s) late-attached, {_spiderComponents.Count} spider(s) alive at end");
        _spiderComponents.Clear();
        // Clear error dedup so a fresh mission can re-log genuinely new occurrences.
        _loggedErrors.Clear();
        base.OnRemoveBehavior();
    }
}
