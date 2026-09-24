using System;
using System.Collections.Generic;
using BehaviorTrees;
using BehaviorTreeWrapper;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.ElephantLike.BehaviorTreeElements;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Animalia;

/// <summary>
/// Mission boundary for the ridden Animalia elk and moose (#646). Attaches a per-agent <see cref="AnimaliaBehaviorTree"/>
/// to every elk- or moose-MOUNT agent, one tracker and one registered tree name per animal, so each tree carries its
/// own profile. The great elk's wiring (<see cref="TAOM.Features.Elk.ElkMissionBehavior"/>) for two animals. MUST be
/// <c>: MissionLogic</c>, NEVER <c>: MissionBehavior</c> (the looter-battle NRE rule, pinned by
/// BehaviorTreeMissionLogicInheritanceTests). The attach key is the Monster's StringId, never a character id: the
/// horse-slot mount agent has no Character. No mount-lock and no Patch47 entry: base_monster="horse" inherits vanilla's
/// rider-death surface whole, as on the ram and the great elk.
/// </summary>
public class AnimaliaMissionBehavior : MissionLogic
{
    private const string ElkTree = "AnimaliaElkTree";
    private const string MooseTree = "AnimaliaMooseTree";

    private readonly IModLogger _logger;
    private readonly HashSet<string> _loggedErrors = new();
    private readonly CreatureTreeTracker _elks;
    private readonly CreatureTreeTracker _moose;
    private bool _initialized;
    private bool _treesAdded;

    public AnimaliaMissionBehavior()
    {
        _logger = IoC.Resolve<IModLogger>();
        IAnimaliaElkAttackService elk = IoC.Resolve<IAnimaliaElkAttackService>();
        IAnimaliaMooseAttackService moose = IoC.Resolve<IAnimaliaMooseAttackService>();
        _elks = new CreatureTreeTracker(ElkTree, "[Animalia]", a => elk.IsCreatureMonster(a.Monster?.StringId), _logger);
        _moose = new CreatureTreeTracker(MooseTree, "[Animalia]", a => moose.IsCreatureMonster(a.Monster?.StringId), _logger);
    }

    private void Initialize()
    {
        _initialized = true;
        BTRegister.RegisterClass(ElkTree, (object[] objects) => AnimaliaBehaviorTree.Build(objects, AnimaliaCombat.ElkProfile));
        BTRegister.RegisterClass(MooseTree, (object[] objects) => AnimaliaBehaviorTree.Build(objects, AnimaliaCombat.MooseProfile));
        if (BTRegister.Logger == null)
            BTRegister.AddLogger(new TaomBTLogger());
        CheckBinding(AnimaliaCombat.ElkProfile, AnimaliaConfig.ElkAttackActionName, AnimaliaConfig.ElkActionSetId);
        CheckBinding(AnimaliaCombat.MooseProfile, AnimaliaConfig.MooseAttackActionName, AnimaliaConfig.MooseActionSetId);
        _logger.LogInfo("[Animalia] Initialized");
    }

    /// <summary>Drift guard: the attack actions, their bindings and clips live in the UNVERSIONED Armory. A missing
    /// action resolves to act_none silently; a typed action whose binding or clip package is gone resolves to a valid
    /// index and plays nothing. So check the type, then look the set up by id and ask for the clip.</summary>
    private void CheckBinding(ElephantLikeCombatProfile profile, string action, string actionSetId)
    {
        if (profile.AnyUnresolved())
        {
            _logger.LogError($"[Animalia] {action} resolved to act_none - LOTRLOME_Armory action_types drift? The attack will not animate.");
            return;
        }
        MBActionSet set = MBActionSet.GetActionSet(actionSetId);
        if (!set.IsValid)
            _logger.LogError($"[Animalia] Action set {actionSetId} not found - LOTRLOME_Armory action_sets drift? The animal will not animate correctly.");
        else if (!MBActionSet.CheckActionAnimationClipExists(set, profile.Trample))
            _logger.LogError($"[Animalia] {action} has no clip in {actionSetId} - the binding or its clip package is missing. The attack will play nothing.");
    }

    public override void OnMissionTick(float dt)
    {
        try
        {
            if (!_initialized) Initialize();
            if (!_treesAdded)
            {
                _treesAdded = true;
                int elks = _elks.AttachAll(Mission.Current.AllAgents);
                int moose = _moose.AttachAll(Mission.Current.AllAgents);
                _logger.LogInfo($"[Animalia] Attached behavior trees to {elks} elk(s) and {moose} moose");
            }
            _elks.PruneDead();
            _moose.PruneDead();
        }
        catch (Exception ex)
        {
            string key = $"{ex.GetType().Name}:{ex.TargetSite?.Name}";
            if (_loggedErrors.Add(key))
                _logger.LogError($"[Animalia] OnMissionTick error: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
        }
    }

    public override void OnAgentBuild(Agent agent, Banner banner)
    {
        base.OnAgentBuild(agent, banner);
        // Late-spawn attach (taom.spawn_troops, reinforcements): only after Initialize registered the tree names.
        if (_treesAdded)
        {
            _elks.TryLateAttach(agent);
            _moose.TryLateAttach(agent);
        }
    }

    public override void OnRemoveBehavior()
    {
        if (_treesAdded)
            _logger.LogInfo($"[Animalia] Mission end: {_elks.LateAttachCount + _moose.LateAttachCount} tree(s) late-attached, " +
                            $"{_elks.AliveCount} elk(s) and {_moose.AliveCount} moose alive at end");
        _elks.Clear();
        _moose.Clear();
        _loggedErrors.Clear();
        base.OnRemoveBehavior();
    }
}
