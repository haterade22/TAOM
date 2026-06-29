using System;
using System.Collections.Generic;
using BehaviorTrees;
using BehaviorTreeWrapper;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.Mumakil.BehaviorTreeElements;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Mumakil;

/// <summary>
/// Mission boundary for the ridden Mûmakil. Attaches a per-agent <see cref="MumakilBehaviorTree"/> (via a
/// <c>BehaviorTreeAgentComponent</c>) to every Mûmakil-MOUNT agent in the battle — the elephant/spider wiring.
/// The attach key is <c>Monster.StringId == "taom_mumakil"</c>, NEVER the character id: the horse-slot mount
/// agent's Character is the Harad RIDER, not the Mûmakil. The rider's cavalry AI drives movement; the BT layers
/// the trample/tusk attacks. No howdah (Phase 1 = single rider, no platform crew).
/// </summary>
public class MumakilMissionBehavior : MissionLogic
{
    private readonly IMumakilAttackService _service;
    private readonly IModLogger _logger;
    private readonly HashSet<string> _loggedErrors = new();
    // Shadow list of attached BT components, for dead-agent pruning. The engine's Agent.Tick auto-ticks
    // each component (v1.4.5) — never tick them manually; only drop dead Mûmakil from this list.
    private readonly List<(Agent agent, BehaviorTreeAgentComponent comp)> _mumakilComponents = new();
    private bool _initialized;
    private bool _treesAdded;
    // Custom-battle deployment spawns AFTER the first mission tick, so the first-tick scan typically reports 0
    // and every Mûmakil arrives via OnAgentBuild — count those for visibility.
    private int _lateAttachCount;

    public MumakilMissionBehavior()
    {
        _service = IoC.Resolve<IMumakilAttackService>();
        _logger = IoC.Resolve<IModLogger>();
    }

    private void Initialize()
    {
        _initialized = true;
        BTRegister.RegisterClass("MumakilTree", (object[] objects) => MumakilBehaviorTree.BuildTree(objects));
        if (BTRegister.Logger == null)
            BTRegister.AddLogger(new TaomBTLogger());

        // Armory-drift guard: the attack clip names live in the EXTERNAL LOTRLOME action_types.xml and
        // ActionIndexCache resolves eagerly — a rename there silently yields act_none, and playing act_none
        // on channel 0 kills the locomotion cycle (the elephant "slide" class). Detect at mission start.
        if (MumakilAttackActions.AnyUnresolved())
            _logger.LogError(
                "[Mumakil] One or more attack actions resolved to act_none — LOTRLOME action_types drift? " +
                $"Expected {MumakilConfig.TrampleActionName}/{MumakilConfig.SideAttackLeftActionName}/" +
                $"{MumakilConfig.SideAttackRightActionName} (shared elephant clips). Attacks will not animate correctly.");

        _logger.LogInfo("[Mumakil] Initialized");
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
                    if (TryAttachMumakilTree(a)) count++;
                _logger.LogInfo($"[Mumakil] Attached behavior trees to {count} mumakil(s)");
            }

            // Prune dead Mûmakil from the shadow list (Agent.Tick auto-ticks the components).
            for (int i = _mumakilComponents.Count - 1; i >= 0; i--)
                if (!_mumakilComponents[i].agent.IsActive())
                    _mumakilComponents.RemoveAt(i);
        }
        catch (Exception ex)
        {
            string key = $"{ex.GetType().Name}:{ex.TargetSite?.Name}";
            if (_loggedErrors.Add(key))
                _logger.LogError($"[Mumakil] OnMissionTick error: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
        }
    }

    public override void OnAgentBuild(Agent agent, Banner banner)
    {
        base.OnAgentBuild(agent, banner);
        // Late-spawn attach: only after Initialize registered "MumakilTree" (first OnMissionTick).
        // Mûmakil that built before that are caught by the first-tick scan.
        if (_treesAdded && TryAttachMumakilTree(agent))
        {
            _lateAttachCount++;
            if (_lateAttachCount == 1)
                _logger.LogInfo("[Mumakil] First late-spawn tree attached (deployment spawns after the first tick)");
        }
    }

    // Attaches a MumakilBehaviorTree component to the agent if it is a Mûmakil mount not already tracked.
    // Returns true when a tree was newly attached. Safe pre/post the first-tick scan (de-duplicates).
    private bool TryAttachMumakilTree(Agent agent)
    {
        if (agent == null || !_service.IsMumakilMonster(agent.Monster?.StringId)) return false;
        for (int i = 0; i < _mumakilComponents.Count; i++)
            if (_mumakilComponents[i].agent == agent) return false;   // already attached

        var comp = new BehaviorTreeAgentComponent(agent, "MumakilTree", Array.Empty<object>());
        agent.AddComponent(comp);
        if (comp.Tree != null)
        {
            _mumakilComponents.Add((agent, comp));
            return true;
        }
        _logger.LogError($"[Mumakil] BT build failed for {agent.Name} (Rider={agent.RiderAgent?.Name ?? "null"})");
        return false;
    }

    public override void OnRemoveBehavior()
    {
        if (_treesAdded)
            _logger.LogInfo($"[Mumakil] Mission end: {_lateAttachCount} tree(s) late-attached, {_mumakilComponents.Count} mumakil(s) alive at end");
        _mumakilComponents.Clear();
        // Clear error dedup so a fresh mission can re-log genuinely new occurrences.
        _loggedErrors.Clear();
        base.OnRemoveBehavior();
    }
}
