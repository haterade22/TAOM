using System;
using System.Collections.Generic;
using BehaviorTrees;
using BehaviorTreeWrapper;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Elk;

/// <summary>
/// Mission boundary for the ridden great elk (#636). Attaches a per-agent <see cref="ElkBehaviorTree"/> (via a
/// <c>BehaviorTreeAgentComponent</c>) to every elk-MOUNT agent in the battle, the war ram's wiring. MUST be
/// <c>: MissionLogic</c>, NEVER <c>: MissionBehavior</c> (regression rule,
/// docs/reviews/rca-looter-battle-nre-2026-05-24.md, pinned by BehaviorTreeMissionLogicInheritanceTests). The
/// attach key is <c>Monster.StringId == "taom_elk"</c>, NEVER the character id: the horse-slot mount agent has no
/// Character (the engine builds it with null, v1.5.3 Mission.cs:4611), and the only character on hand is the elf RIDER's.
///
/// Like the ram, the elk carries NO mount-lock (Mirkwood's markets sell it, so a player can ride one) and gets no
/// Patch47 dismount-before-death entry: base_monster="horse" inherits vanilla's rider-death surface whole. A
/// ridden-death in-game test is what would reopen that, not a structural gap here.
///
/// The trees tick from BehaviorTreeMissionLogic.OnMissionTick on the main thread; the component's own OnTick is a
/// deliberate no-op since #592 (Agent.Tick runs on the async AI thread). This behavior never ticks a tree itself.
/// </summary>
public class ElkMissionBehavior : MissionLogic
{
    private readonly IElkAttackService _service;
    private readonly IModLogger _logger;
    private readonly HashSet<string> _loggedErrors = new();
    // Attach/prune bookkeeping (shadow list, dedup, late-attach counting): the shared tracker,
    // see CreatureTreeTracker for the discipline notes.
    private readonly CreatureTreeTracker _tracker;
    private bool _initialized;
    private bool _treesAdded;

    public ElkMissionBehavior()
    {
        _service = IoC.Resolve<IElkAttackService>();
        _logger = IoC.Resolve<IModLogger>();
        _tracker = new CreatureTreeTracker("ElkTree", "[Elk]",
            a => _service.IsCreatureMonster(a.Monster?.StringId), _logger);
    }

    private void Initialize()
    {
        _initialized = true;
        BTRegister.RegisterClass("ElkTree", (object[] objects) => ElkBehaviorTree.BuildTree(objects));
        if (BTRegister.Logger == null)
            BTRegister.AddLogger(new TaomBTLogger());

        // Drift guard: the antler charge is the ram's act_war_ram_butt, bound in LOTRLOME_Armory's as_war_ram and
        // typed in its action_types.xml, and the Armory is UNVERSIONED: a reinstall drops both. A missing action
        // resolves to act_none silently; a typed action whose binding or clip package is gone resolves to a valid
        // index and plays nothing. So check the type, then look the set up by id and ask for the clip.
        if (ElkCombat.Profile.AnyUnresolved())
            _logger.LogError(
                "[Elk] One or more attack actions resolved to act_none - LOTRLOME_Armory as_war_ram / action_types drift? " +
                $"Expected {ElkConfig.AttackActionName}. The antler charge will not animate.");
        else
        {
            MBActionSet elkSet = MBActionSet.GetActionSet(ElkConfig.ActionSetId);
            if (!elkSet.IsValid)
                _logger.LogError(
                    $"[Elk] Action set {ElkConfig.ActionSetId} not found - LOTRLOME_Armory action_sets drift? " +
                    "The elk Monster names it, so elks will not animate correctly.");
            else if (!MBActionSet.CheckActionAnimationClipExists(elkSet, ElkCombat.Profile.Trample))
                _logger.LogError(
                    $"[Elk] {ElkConfig.AttackActionName} has no clip in {ElkConfig.ActionSetId} - the binding or the " +
                    "war_ram_butt clip package is missing. The antler charge will play nothing.");
        }

        _logger.LogInfo("[Elk] Initialized");
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
                _logger.LogInfo($"[Elk] Attached behavior trees to {count} elk(s)");
            }

            // Prune dead elks from the shadow list.
            _tracker.PruneDead();
        }
        catch (Exception ex)
        {
            string key = $"{ex.GetType().Name}:{ex.TargetSite?.Name}";
            if (_loggedErrors.Add(key))
                _logger.LogError($"[Elk] OnMissionTick error: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
        }
    }

    public override void OnAgentBuild(Agent agent, Banner banner)
    {
        base.OnAgentBuild(agent, banner);
        // Late-spawn attach: only after Initialize registered "ElkTree" (first OnMissionTick).
        // Elks built before that are caught by the first-tick scan.
        if (_treesAdded)
            _tracker.TryLateAttach(agent);
    }

    public override void OnRemoveBehavior()
    {
        if (_treesAdded)
            _logger.LogInfo($"[Elk] Mission end: {_tracker.LateAttachCount} tree(s) late-attached, {_tracker.AliveCount} elk(s) alive at end");
        _tracker.Clear();
        // Clear error dedup so a fresh mission can re-log genuinely new occurrences.
        _loggedErrors.Clear();
        base.OnRemoveBehavior();
    }
}
