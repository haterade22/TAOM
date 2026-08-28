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
/// Agent.Tick auto-ticks attached agent components since v1.4.5; this behavior does NOT manually tick
/// the BT component (the warg/spider double-tick regression class), matching the Mumakil wiring below.
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

        // Drift guard: act_horse_kick is a VANILLA
        // clips resolved out of the game's own Native/ModuleData, not LOTRLOME_Armory, but
        // ActionIndexCache still resolves eagerly, so a future engine bump renaming/removing one of
        // them would silently yield act_none and kill the ram's locomotion cycle on channel 0 (the
        // elephant "slide" class). Detect at mission start.
        if (WarRamCombat.Profile.AnyUnresolved())
            _logger.LogError(
                "[WarRam] One or more attack actions resolved to act_none - vanilla as_horse action drift? " +
                $"Expected {WarRamConfig.AttackActionName}. Attacks will not animate correctly.");

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
