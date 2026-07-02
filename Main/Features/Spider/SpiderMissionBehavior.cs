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
    // Attach/prune bookkeeping (shadow list, dedup, late-attach counting) — shared tracker,
    // see CreatureTreeTracker for the discipline notes.
    private readonly CreatureTreeTracker _tracker;
    private bool _initialized;
    private bool _treesAdded;

    public SpiderMissionBehavior()
    {
        _service = IoC.Resolve<ISpiderAttackService>();
        _logger = IoC.Resolve<IModLogger>();
        _tracker = new CreatureTreeTracker("SpiderTree", "[Spider]",
            a => _service.IsSpiderMonster(a.Monster?.StringId), _logger);
    }

    private void Initialize()
    {
        _initialized = true;
        BTRegister.RegisterClass("SpiderTree", (object[] objects) => SpiderBehaviorTree.BuildTree(objects));
        if (BTRegister.Logger == null)
            BTRegister.AddLogger(new TaomBTLogger());

        // Armory-drift guard: the attack clip names live in the EXTERNAL LOTRLOME action_types.xml and
        // ActionIndexCache resolves eagerly — a rename there silently yields act_none, and playing act_none
        // on channel 0 kills the locomotion cycle (the elephant "slide" class). Detect at mission start.
        if (SpiderAttackActions.AnyUnresolved())
            _logger.LogError(
                "[Spider] One or more attack actions resolved to act_none — LOTRLOME action_types drift? " +
                $"Expected {SpiderConfig.PounceFrontActionName}/{SpiderConfig.PounceChargeActionName}/" +
                $"{SpiderConfig.SwingLeftActionName}/{SpiderConfig.SwingRightActionName}. " +
                "Attacks will not animate correctly.");

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
                int count = _tracker.AttachAll(Mission.Current.AllAgents);
                _logger.LogInfo($"[Spider] Attached behavior trees to {count} spider(s)");
            }

            // Prune dead spiders from the shadow list (Agent.Tick auto-ticks the components).
            _tracker.PruneDead();
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
        if (_treesAdded)
            _tracker.TryLateAttach(agent);
    }

    public override void OnRemoveBehavior()
    {
        if (_treesAdded)
            _logger.LogInfo($"[Spider] Mission end: {_tracker.LateAttachCount} tree(s) late-attached, {_tracker.AliveCount} spider(s) alive at end");
        _tracker.Clear();
        // Clear error dedup so a fresh mission can re-log genuinely new occurrences.
        _loggedErrors.Clear();
        base.OnRemoveBehavior();
    }
}
