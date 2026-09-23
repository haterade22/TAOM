using System;
using System.Collections.Generic;
using BehaviorTrees;
using BehaviorTreeWrapper;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.Elephant;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Mumakil;

/// <summary>
/// Mission boundary for the ridden Mûmakil. Attaches a per-agent <see cref="MumakilBehaviorTree"/> (via a
/// <c>BehaviorTreeAgentComponent</c>) to every Mûmakil-MOUNT agent in the battle — the elephant/spider wiring.
/// The attach key is <c>Monster.StringId == "taom_mumakil"</c>, NEVER the character id: the horse-slot mount
/// agent has no Character (the engine builds it with null), and the only character on hand is the Harad RIDER's. The
/// rider (the player, or the cavalry AI) drives movement; the BT layers the trample/tusk attacks.
///
/// Phase 2 (#627, 2026-09-20) adds the war tower's crew: when a rider builds on a mumakil, the behaviour
/// instantiates <see cref="TaomMumakilPlatform"/> on it and queues a crew, which spawns from the next
/// <see cref="OnMissionTick"/>. The platform is invisible; the tower the player sees is an AdditionalMesh on the
/// Horse item, so every mumakil carries one and there is no harness to gate on.
/// </summary>
public class MumakilMissionBehavior : MissionLogic
{
    private readonly IMumakilAttackService _service;
    private readonly IModLogger _logger;
    private readonly HashSet<string> _loggedErrors = new();
    // Attach/prune bookkeeping (shadow list, dedup, late-attach counting) — shared tracker,
    // see CreatureTreeTracker for the discipline notes.
    private readonly CreatureTreeTracker _tracker;
    private bool _initialized;
    private bool _treesAdded;
    // The tower crews (#627 phase 2): queued from OnAgentBuild, spawned from OnMissionTick. See MumakilCrewSpawner.
    private readonly MumakilCrewSpawner _crew;

    public MumakilMissionBehavior()
    {
        _service = IoC.Resolve<IMumakilAttackService>();
        _logger = IoC.Resolve<IModLogger>();
        _tracker = new CreatureTreeTracker("MumakilTree", "[Mumakil]",
            a => _service.IsCreatureMonster(a.Monster?.StringId), _logger);
        _crew = new MumakilCrewSpawner(_logger);
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
        if (MumakilCombat.Profile.AnyUnresolved())
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
                int count = _tracker.AttachAll(Mission.Current.AllAgents);
                _logger.LogInfo($"[Mumakil] Attached behavior trees to {count} mumakil(s)");
            }

            // Prune dead Mûmakil from the shadow list (Agent.Tick auto-ticks the components).
            _tracker.PruneDead();

            // Outside the engine's own SpawnAgent loop (#595): a crew queued while a rider was building spawns here.
            _crew.Drain();
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
        if (_treesAdded)
            _tracker.TryLateAttach(agent);

        // Phase 2: the crew spawner owns the platform's whole lifecycle; this stays a delegation (ADR-002).
        if (agent?.MountAgent != null && _service.IsCreatureMonster(agent.MountAgent.Monster?.StringId))
            _crew.TryBuildPlatform(agent);
    }

    public override void OnRemoveBehavior()
    {
        if (_treesAdded)
            _logger.LogInfo($"[Mumakil] Mission end: {_tracker.LateAttachCount} tree(s) late-attached, {_tracker.AliveCount} mumakil(s) alive at end");
        _tracker.Clear();
        _crew.Clear();
        // Clear error dedup so a fresh mission can re-log genuinely new occurrences.
        _loggedErrors.Clear();
        base.OnRemoveBehavior();
    }
}
