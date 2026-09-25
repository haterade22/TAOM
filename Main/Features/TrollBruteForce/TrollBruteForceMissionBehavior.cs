using System;
using System.Collections.Generic;
using BehaviorTrees;
using BehaviorTreeWrapper;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.TrollBruteForce;

/// <summary>
/// Mission boundary for the trolls' Brute Force (#649): attaches a <see cref="TrollBruteForceBehaviorTree"/> to
/// every agent whose Monster is <c>cave_troll</c> or <c>hill_troll</c> (the settlement and child Monsters never
/// match; <see cref="TrollBruteForceConfig.ActionSetsByMonster"/>). Elk's wiring:
/// MUST be <c>: MissionLogic</c>, NEVER <c>: MissionBehavior</c> (rca-looter-battle-nre-2026-05-24.md, pinned by
/// BehaviorTreeMissionLogicInheritanceTests). The trees tick from BehaviorTreeMissionLogic.OnMissionTick on the
/// main thread; this behavior never ticks a tree itself.
/// </summary>
public class TrollBruteForceMissionBehavior : MissionLogic
{
    private const string TreeName = "TrollBruteForceTree";

    private readonly ITrollBruteForceService _service;
    private readonly IModLogger _logger;
    private readonly HashSet<string> _loggedErrors = new();
    private readonly CreatureTreeTracker _tracker;
    private bool _initialized;
    private bool _treesAdded;

    public TrollBruteForceMissionBehavior()
    {
        _service = IoC.Resolve<ITrollBruteForceService>();
        _logger = IoC.Resolve<IModLogger>();
        _tracker = new CreatureTreeTracker(TreeName, "[TrollBruteForce]",
            a => _service.IsBruteForceTroll(a.Monster?.StringId), _logger);
    }

    private void Initialize()
    {
        _initialized = true;
        BTRegister.RegisterClass(TreeName, (object[] objects) => TrollBruteForceBehaviorTree.BuildTree(objects));
        if (BTRegister.Logger == null)
            BTRegister.AddLogger(new TaomBTLogger());

        // Drift guard: the action lives in the UNVERSIONED LOTRLOME_Armory (action_types.xml, and a binding in each
        // troll's standalone set), so a reinstall drops it. A missing action resolves to act_none silently; a declared
        // action whose binding or clip is gone resolves to a valid index and plays nothing.
        if (TrollBruteForceCombat.Action == ActionIndexCache.act_none)
            _logger.LogError($"[TrollBruteForce] {TrollBruteForceConfig.ActionName} resolved to act_none - " +
                "LOTRLOME_Armory action_types drift? Trolls will not smash.");
        else
        {
            foreach (var pair in TrollBruteForceConfig.ActionSetsByMonster)
            {
                MBActionSet set = MBActionSet.GetActionSet(pair.Value);
                if (!set.IsValid)
                    _logger.LogError($"[TrollBruteForce] Action set {pair.Value} ({pair.Key}) not found - " +
                        "LOTRLOME_Armory action_sets drift?");
                else if (!MBActionSet.CheckActionAnimationClipExists(set, TrollBruteForceCombat.Action))
                    _logger.LogError($"[TrollBruteForce] {TrollBruteForceConfig.ActionName} has no clip in " +
                        $"{pair.Value} - the binding or its clip package is missing. The {pair.Key} smash will play nothing.");
            }
        }

        _logger.LogInfo("[TrollBruteForce] Initialized");
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
                _logger.LogInfo($"[TrollBruteForce] Attached behavior trees to {count} troll(s)");
            }

            _tracker.PruneDead();
        }
        catch (Exception ex)
        {
            string key = $"{ex.GetType().Name}:{ex.TargetSite?.Name}";
            if (_loggedErrors.Add(key))
                _logger.LogError($"[TrollBruteForce] OnMissionTick error: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
        }
    }

    public override void OnAgentBuild(Agent agent, Banner banner)
    {
        base.OnAgentBuild(agent, banner);
        // Late-spawn attach: only after Initialize registered the tree (first OnMissionTick).
        if (_treesAdded)
            _tracker.TryLateAttach(agent);
    }

    public override void OnRemoveBehavior()
    {
        if (_treesAdded)
            _logger.LogInfo($"[TrollBruteForce] Mission end: {_tracker.LateAttachCount} tree(s) late-attached, " +
                $"{_tracker.AliveCount} troll(s) alive at end");
        _tracker.Clear();
        _loggedErrors.Clear();
        base.OnRemoveBehavior();
    }
}
