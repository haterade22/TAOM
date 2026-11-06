using System;
using System.Collections.Generic;
using BehaviorTrees;
using BehaviorTreeWrapper;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.DwarfAttacks.Hooks;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.DwarfAttacks;

/// <summary>
/// Mission boundary for the dwarf attacks (#1087): builds a <see cref="DwarfAttacksBehaviorTree"/> for every agent whose
/// Monster is <c>dwarf</c> (male and female; <c>dwarf_child</c> never matches) and runs them every mission tick, on the
/// main thread, through <see cref="DwarfTreeHost"/>, which says why they are not agent components. MUST be
/// <c>: MissionLogic</c>, NEVER <c>: MissionBehavior</c> (rca-looter-battle-nre-2026-05-24.md, pinned by
/// BehaviorTreeMissionLogicInheritanceTests). Trees build whatever the MCM switch says, so switching it on mid-battle
/// works; the ready decorator reads the switch live. Only field battles, sieges and hideouts
/// (<see cref="Mission.MissionCombatType.Combat"/>): an arena or tournament gets no tree, as the race abilities and
/// signature strikes do (SignatureMissionGate).
/// </summary>
public class DwarfAttacksMissionBehavior : MissionLogic
{
    private readonly IModLogger _logger;
    private readonly HashSet<string> _loggedErrors = new();
    private readonly DwarfTreeHost _host;
    private bool _initialized;
    private bool _eligible;
    private bool _treesAdded;
    private int _lateAttached;

    public DwarfAttacksMissionBehavior()
    {
        _logger = IoC.Resolve<IModLogger>();
        _host = new DwarfTreeHost(IoC.Resolve<DwarfAttackService>(), IoC.Resolve<DwarfAttacksSettingsProvider>(),
            IoC.Resolve<DwarfAttackChecks>());
    }

    private void Initialize()
    {
        _initialized = true;
        Mission mission = Mission.Current;
        _eligible = mission != null && mission.CombatType == Mission.MissionCombatType.Combat;
        _logger.LogInfo($"[DwarfAttacks] mission gate: eligible={_eligible} combatType={mission?.CombatType}");
        if (!_eligible) return;
        if (BTRegister.Logger == null)
            BTRegister.AddLogger(new TaomBTLogger());

        // Drift guard: both actions live in the UNVERSIONED LOTRLOME_Armory (action_types.xml, and the bindings in
        // as_dwarf_warrior), so a reinstall drops them. A missing action resolves to act_none, and the ready decorator
        // then never starts that attack; a declared action whose binding or clip is gone plays nothing.
        CheckAction(DwarfAttacksConfig.HeadButtActionName, DwarfAttacksCombat.HeadButt);
        CheckAction(DwarfAttacksConfig.DropKickActionName, DwarfAttacksCombat.DropKick);
        _logger.LogInfo("[DwarfAttacks] Initialized");
    }

    private void CheckAction(string name, ActionIndexCache action)
    {
        if (action == ActionIndexCache.act_none)
        {
            _logger.LogError($"[DwarfAttacks] {name} resolved to act_none - LOTRLOME_Armory action_types drift? " +
                "Run python tools/bind_dwarf_attack_actions.py --check. Dwarves never start this attack.");
            return;
        }

        MBActionSet set = MBActionSet.GetActionSet(DwarfAttacksConfig.ActionSetId);
        if (!ActionSetLookup.Found(DwarfAttacksConfig.ActionSetId, set.IsValid, set.GetName()))
            _logger.LogError($"[DwarfAttacks] Action set {DwarfAttacksConfig.ActionSetId} not found - LOTRLOME_Armory action_sets drift?");
        else if (!MBActionSet.CheckActionAnimationClipExists(set, action))
            _logger.LogError($"[DwarfAttacks] {name} has no clip in {DwarfAttacksConfig.ActionSetId} - the binding or its " +
                "clip package is missing (tools/gen_dwarf_attack_clips.ps1 -Verify). The attack will play nothing.");
    }

    public override void OnMissionTick(float dt)
    {
        try
        {
            if (!_initialized) Initialize();
            if (!_eligible) return;

            if (!_treesAdded)
            {
                _treesAdded = true;
                int count = 0;
                foreach (Agent agent in Mission.Current.AllAgents)
                    if (_host.TryAttach(agent)) count++;
                _logger.LogInfo($"[DwarfAttacks] Built behavior trees for {count} dwarf(s)");
            }

            _host.RunAll();
        }
        catch (Exception ex)
        {
            LogOnce($"{ex.GetType().Name}:{ex.TargetSite?.Name}",
                $"[DwarfAttacks] OnMissionTick error: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
        }
    }

    // Reinforcements and every Custom Battle soldier arrive here. Inside Mission.SpawnAgent's unguarded loop over
    // behaviors, so it must not throw (#595).
    public override void OnAgentBuild(Agent agent, Banner banner)
    {
        base.OnAgentBuild(agent, banner);
        if (!_treesAdded) return;
        try
        {
            if (_host.TryAttach(agent)) _lateAttached++;
        }
        catch (Exception ex)
        {
            LogOnce("late:" + ex.GetType().Name, $"[DwarfAttacks] OnAgentBuild error: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void LogOnce(string key, string message)
    {
        if (_loggedErrors.Add(key)) _logger.LogError(message);
    }

    public override void OnRemoveBehavior()
    {
        if (_treesAdded)
            _logger.LogInfo($"[DwarfAttacks] Mission end: {_lateAttached} tree(s) built late, {_host.Count} dwarf(s) alive at end");
        _host.Clear();
        _loggedErrors.Clear();
        base.OnRemoveBehavior();
    }
}
