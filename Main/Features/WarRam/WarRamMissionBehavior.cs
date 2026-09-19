using System;
using System.Collections.Generic;
using BehaviorTrees;
using BehaviorTreeWrapper;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.WarRam;

/// <summary>
/// Mission boundary for the ridden war ram. Attaches a per-agent <see cref="WarRamBehaviorTree"/> (via a
/// <c>BehaviorTreeAgentComponent</c>) to every war-ram-MOUNT agent in the battle, the elephant/spider/
/// Mumakil wiring. MUST be <c>: MissionLogic</c>, NEVER <c>: MissionBehavior</c> (regression rule, see
/// docs/reviews/rca-looter-battle-nre-2026-05-24.md, pinned by
/// TAOM.Tests/BehaviorTreeWrapper/BehaviorTreeMissionLogicInheritanceTests.cs). The attach key is
/// <c>Monster.StringId == "taom_war_ram"</c>, NEVER the character id: the horse-slot mount agent's
/// Character is the DWARF RIDER, not the ram. The rider's cavalry AI drives movement; the BT layers the
/// single kick attack on top of it.
///
/// Unlike the war elephant/spider/Mumakil, the war ram carries NO mount-lock (it is a player-rideable
/// culture mount, see the shipping ram_rider career) and gets no Patch47 dismount-before-death entry
/// (Patch47 is SPIDER-only, not elephant):
/// it inherits vanilla's rider-death surface whole via base_monster="horse", so the elephant/spider's
/// problem (a Monster with no vanilla rider-death surface) does not apply here. A ridden-death in-game
/// test is what would justify revisiting that decision, not a structural gap in this file.
///
/// The trees tick from BehaviorTreeMissionLogic.OnMissionTick on the main thread; the component's own
/// OnTick is a deliberate no-op since #592 (Agent.Tick runs on the async AI thread). This behavior does
/// not tick the BT component itself (the warg/spider double-tick regression class).
/// </summary>
public class WarRamMissionBehavior : MissionLogic
{
    private readonly IWarRamAttackService _service;
    private readonly IModLogger _logger;
    private readonly HashSet<string> _loggedErrors = new();
    // Attach/prune bookkeeping (shadow list, dedup, late-attach counting) - shared tracker,
    // see CreatureTreeTracker for the discipline notes.
    private readonly CreatureTreeTracker _tracker;
    private bool _initialized;
    private bool _treesAdded;

    public WarRamMissionBehavior()
    {
        _service = IoC.Resolve<IWarRamAttackService>();
        _logger = IoC.Resolve<IModLogger>();
        _tracker = new CreatureTreeTracker("WarRamTree", "[WarRam]",
            a => _service.IsCreatureMonster(a.Monster?.StringId), _logger);
    }

    private void Initialize()
    {
        _initialized = true;
        BTRegister.RegisterClass("WarRamTree", (object[] objects) => WarRamBehaviorTree.BuildTree(objects));
        if (BTRegister.Logger == null)
            BTRegister.AddLogger(new TaomBTLogger());

        // Drift guard: act_war_ram_butt is bound in LOTRLOME_Armory's action_sets.xml (as_war_ram) and
        // typed in its action_types.xml, and the Armory is UNVERSIONED: a module reinstall drops both.
        // ActionIndexCache resolves eagerly, so a missing action silently yields act_none and kills the
        // ram's locomotion cycle on channel 0 (the elephant "slide" class). Detect at mission start.
        // The type check alone misses a typed action whose binding or clip package is gone: that resolves
        // to a valid index and plays nothing. So also look the set up by id (an invalid set for a missing id,
        // as vanilla's MBGlobals.GetActionSet relies on) and ask the engine whether the action has a clip in it.
        if (WarRamCombat.Profile.AnyUnresolved())
            _logger.LogError(
                "[WarRam] One or more attack actions resolved to act_none - LOTRLOME_Armory as_war_ram / action_types drift? " +
                $"Expected {WarRamConfig.AttackActionName}. Attacks will not animate correctly.");
        else
        {
            MBActionSet ramSet = MBActionSet.GetActionSet(WarRamConfig.ActionSetId);
            if (!ramSet.IsValid)
                _logger.LogError(
                    $"[WarRam] Action set {WarRamConfig.ActionSetId} not found - LOTRLOME_Armory action_sets drift? " +
                    "The ram Monster names it, so rams will not animate correctly.");
            else if (!MBActionSet.CheckActionAnimationClipExists(ramSet, WarRamCombat.Profile.Trample))
                _logger.LogError(
                    $"[WarRam] {WarRamConfig.AttackActionName} has no clip in {WarRamConfig.ActionSetId} - the binding or the " +
                    "war_ram_butt clip package is missing. The head-butt will play nothing.");
        }

        _logger.LogInfo("[WarRam] Initialized");
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
                _logger.LogInfo($"[WarRam] Attached behavior trees to {count} war ram(s)");
            }

            // Prune dead war rams from the shadow list (Agent.Tick auto-ticks the components).
            _tracker.PruneDead();
        }
        catch (Exception ex)
        {
            string key = $"{ex.GetType().Name}:{ex.TargetSite?.Name}";
            if (_loggedErrors.Add(key))
                _logger.LogError($"[WarRam] OnMissionTick error: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
        }
    }

    public override void OnAgentBuild(Agent agent, Banner banner)
    {
        base.OnAgentBuild(agent, banner);
        // Late-spawn attach: only after Initialize registered "WarRamTree" (first OnMissionTick).
        // War rams that built before that are caught by the first-tick scan.
        if (_treesAdded)
            _tracker.TryLateAttach(agent);
    }

    public override void OnRemoveBehavior()
    {
        if (_treesAdded)
            _logger.LogInfo($"[WarRam] Mission end: {_tracker.LateAttachCount} tree(s) late-attached, {_tracker.AliveCount} war ram(s) alive at end");
        _tracker.Clear();
        // Clear error dedup so a fresh mission can re-log genuinely new occurrences.
        _loggedErrors.Clear();
        base.OnRemoveBehavior();
    }
}
