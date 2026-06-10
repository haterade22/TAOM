using System;
using System.Collections.Generic;
using BehaviorTrees;
using BehaviorTreeWrapper;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace TAOM.Features.Elephant;

/// <summary>
/// Mission boundary for the AI war-elephant. Attaches a per-agent <see cref="ElephantBehaviorTree"/> (via a
/// <c>BehaviorTreeAgentComponent</c>) to every elephant in the battle — the warg's pattern
/// (<see cref="TAOM.Features.Warg.WargMissionBehavior"/>) applied to the elephant. The tree drives the auto-trample
/// (a behavioral 1-for-1 of ADOD's <c>OnTickAsAI</c>): an AI-ridden elephant occasionally plays an attack animation
/// and deals a radial knockdown to enemies within <see cref="ElephantConfig.TrampleRadius"/>. The pure gate + damage
/// formula live in <see cref="IElephantAttackService"/> (unit-tested); the engine work lives in the BT leaf nodes
/// (<c>EnemyInTrampleRangeDecorator</c> + <c>ElephantTrampleTask</c>). This behavior also instantiates the howdah
/// when the mahout builds (see <see cref="TryInstantiateHowdah"/>). The rider/ai-controlled branch structure is the
/// foundation for richer creature AI — player-triggered trample, enrage/charge — in later phases.
/// </summary>
public class ElephantMissionBehavior : MissionLogic
{
    private readonly IElephantAttackService _service;
    private readonly IModLogger _logger;
    private readonly HashSet<string> _loggedErrors = new();
    // Shadow list of attached BT components, for dead-agent pruning. The engine's Agent.Tick auto-ticks each
    // component (v1.4.5) — we never tick them manually; we only drop dead elephants from this list.
    private readonly List<(Agent agent, BehaviorTreeAgentComponent comp)> _elephantComponents = new();
    private bool _initialized;
    private bool _treesAdded;

    public ElephantMissionBehavior()
    {
        _service = IoC.Resolve<IElephantAttackService>();
        _logger = IoC.Resolve<IModLogger>();
    }

    public override void OnAgentBuild(Agent agent, Banner banner)
    {
        base.OnAgentBuild(agent, banner);

        // Late-spawn BT attach: only after Initialize has registered "ElephantTree" (first OnMissionTick).
        // Elephants that built before that are caught by the first-tick scan in OnMissionTick.
        if (_treesAdded) TryAttachElephantTree(agent);

        // Howdah: when the mahout rider builds (human, mounted on an elephant, wearing sk_elephant_armor_a),
        // instantiate the howdah seat entity above the elephant's neck. Clean-room port of ADOD_Beasts
        // ADODBeastsMissionLogic.OnAgentBuild howdah branch. MountAgent is already built at this point
        // because the engine always builds the mount before the rider (horse-slot spawn order).
        TryInstantiateHowdah(agent);
    }

    private void TryInstantiateHowdah(Agent agent)
    {
        if (agent == null || !agent.IsHuman || agent.MountAgent == null) return;
        if (!_service.IsElephantMonster(agent.MountAgent.Monster?.StringId)) return;

        try
        {
            var character = agent.Character;
            if (character?.Equipment == null) return;
            var harness = character.Equipment[EquipmentIndex.HorseHarness];
            if (harness.Item?.StringId != ElephantConfig.HarnessStringId) return;

            GameEntity howdah = GameEntity.Instantiate(Mission.Current.Scene, "taom_howdah_agent", true);
            if (howdah == null)
            {
                _logger.LogError("[Elephant] Howdah: prefab 'taom_howdah_agent' not found in Prefabs folder.");
                return;
            }
            howdah.SetVisibilityExcludeParents(true);
            var machine = howdah.GetFirstScriptOfType<TaomHowdahMachine>();
            if (machine == null)
            {
                _logger.LogError("[Elephant] Howdah: TaomHowdahMachine script not found on instantiated prefab.");
                return;
            }
            machine.elephantAgent = agent.MountAgent;
            machine.elephantRider = agent;
            // Position the howdah at the elephant's back BEFORE spawning crew so that
            // seat GlobalPositions are valid world coordinates, not world origin (0,0,0).
            machine.RepositionToElephant();

            // Capture the mahout's current formation for crew BEFORE moving mahout to Cavalry.
            // Crew stay in the original formation (HorseArcher) to receive ranged fire orders;
            // mahout moves to Cavalry so the elephant charges instead of circling at skirmish range.
            Formation crewFormation = agent.Formation;
            if (agent.Team != null)
            {
                var cavalryFormation = agent.Team.GetFormation(FormationClass.Cavalry);
                if (cavalryFormation != null && agent.Formation != cavalryFormation)
                {
                    agent.Formation = cavalryFormation;
                    _logger.LogInfo($"[Elephant] Mahout reassigned to Cavalry formation (was {crewFormation?.FormationIndex})");
                }
            }

            _logger.LogInfo($"[Elephant] Howdah instantiated for rider={agent.Name}");
            // DEFERRED (2026-06-10): crew spawn is a CONFIRMED slide source and is disabled for now.
            // The 4 force-spawned archers, teleported onto the elephant each tick, overlap its collision capsule
            // and the physics solver shoves the elephant ("slide"). Confirmed by the isolation ladder: Build B
            // (crew on, everything else off) slid; rung 4 (all off) did not. Re-enable ONLY together with the
            // crew↔elephant collision fix (e.g. give the crew the elephant's FaceGroupId so they don't collide
            // with it, the engine's own rider-vs-mount mechanism). TrySpawnHowdahCrew is retained for that fix.
            // See docs/features/elephant.md → "Slide root-cause isolation".
            // TrySpawnHowdahCrew(machine, agent, crewFormation);
        }
        catch (Exception ex)
        {
            string key = $"Howdah:{ex.GetType().Name}";
            if (_loggedErrors.Add(key))
                _logger.LogError($"[Elephant] Howdah instantiation failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    // Vanilla UsableMachine detachment cannot path to a moving target — archers walk toward
    // the last-known seat position but the elephant moves away. Force-spawn harad archers
    // directly into all howdah seats the moment the mahout builds, bypassing detachment.
    private void TrySpawnHowdahCrew(TaomHowdahMachine machine, Agent mahout, Formation crewFormation)
    {
        var crewChar = MBObjectManager.Instance.GetObject<CharacterObject>(ElephantConfig.HowdahCrewCharacterId);
        if (crewChar == null)
        {
            _logger.LogError($"[Elephant] Howdah crew character '{ElephantConfig.HowdahCrewCharacterId}' not found.");
            return;
        }

        int seatIndex = 0;
        _logger.LogInfo($"[Elephant] Howdah crew spawn: {machine.StandingPoints.Count} total StandingPoint(s) in prefab");
        foreach (StandingPoint sp in machine.StandingPoints)
        {
            _logger.LogInfo($"[Elephant]   StandingPoint[{seatIndex}]: type={sp.GetType().Name} disabled={sp.IsDisabled} occupied={sp.MovingAgent != null}");
            seatIndex++;
        }

        int spawned = 0;
        foreach (StandingPoint sp in machine.StandingPoints)
        {
            if (!(sp is TaomHowdahStandingPoint seat) || seat.IsDisabled || seat.MovingAgent != null)
                continue;

            // Spawn 0.5m ABOVE the howdah entity origin so agents land ON the bo_empire_keep_a_door_top
            // physics floor surface (at Z=0.273 local) rather than inside its bottom face.
            // Spawning at entity origin (Z=0 local) puts agents inside the floor shape, causing physics
            // to pop them downward on the first frame. TeleportToPosition in OnTick corrects to the
            // exact seat position on the first tick regardless.
            var spawnPos = mahout.Position + new Vec3(0f, 0f, ElephantConfig.HowdahHeightAboveGround + 0.5f);
            _logger.LogInfo($"[Elephant]   Spawning crew #{spawned} at pos=({spawnPos.x:F1},{spawnPos.y:F1},{spawnPos.z:F1})");
            var buildData = new AgentBuildData(crewChar)
                .Team(mahout.Team)
                .InitialPosition(spawnPos)
                .InitialDirection(mahout.LookDirection.AsVec2);

            if (mahout.Origin != null)
                buildData = buildData.TroopOrigin(mahout.Origin);

            // Assign crew to the pre-reassignment (HorseArcher) formation so ranged fire orders
            // reach them. The mahout has already been moved to Cavalry for charge AI; crew stay
            // in HorseArcher. Physical position is controlled by TeleportToPosition each tick.
            if (crewFormation != null)
                buildData = buildData.Formation(crewFormation);

            Agent crewAgent = Mission.Current.SpawnAgent(buildData);
            if (crewAgent == null)
            {
                _logger.LogError($"[Elephant] Howdah crew SpawnAgent returned null for seat {spawned}.");
                continue;
            }

            _logger.LogInfo($"[Elephant]   Crew #{spawned} agent built: name={crewAgent.Name} isActive={crewAgent.IsActive()} hasRanged={crewAgent.HasRangedWeapon(false)}");
            // Managed-only seating: OnUse registers the agent in our seat (AddMovingAgent + lock flags)
            // WITHOUT triggering native AIUseGameObjectEnable. UseGameObject would cause the native
            // pathfinder to continuously route agents toward the elevated seat (unreachable via navmesh),
            // producing a climbing loop. TeleportToPosition in OnTick handles visual elevation instead.
            seat.OnUse(crewAgent, 0);
            spawned++;
        }

        if (spawned == 0)
            _logger.LogWarning("[Elephant] Howdah: no available seats for crew spawn.");
        else
            _logger.LogInfo($"[Elephant] Howdah crew force-spawned: {spawned} archer(s)");
    }

    private void Initialize()
    {
        _initialized = true;
        BTRegister.RegisterClass("ElephantTree", (object[] objects) => ElephantBehaviorTree.BuildTree(objects));
        if (BTRegister.Logger == null)
            BTRegister.AddLogger(new TaomBTLogger());

        // Armory-drift guard: the attack clip names live in the EXTERNAL LOTRLOME action_types.xml and
        // ActionIndexCache resolves eagerly — a rename there silently yields act_none, and playing act_none on
        // channel 0 kills the locomotion cycle (the "slide" bug that shipped 2026-06-09). Detect at mission start.
        if (BehaviorTreeElements.ElephantAttackActions.AnyUnresolved())
            _logger.LogError(
                "[Elephant] One or more attack actions resolved to act_none — LOTRLOME action_types drift? " +
                $"Expected {ElephantConfig.TrampleActionName}/{ElephantConfig.SideAttackLeftActionName}/" +
                $"{ElephantConfig.SideAttackRightActionName}. Attacks will not animate correctly.");

        _logger.LogInfo("[Elephant] Initialized");
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
                    if (TryAttachElephantTree(a)) count++;
                _logger.LogInfo($"[Elephant] Attached behavior trees to {count} elephant(s)");
            }

            // Prune dead elephants from the shadow list. Agent.Tick auto-ticks each BT component (v1.4.5) —
            // there is no manual tick here; we only drop dead elephants so the list doesn't grow unbounded.
            for (int i = _elephantComponents.Count - 1; i >= 0; i--)
                if (!_elephantComponents[i].agent.IsActive())
                    _elephantComponents.RemoveAt(i);
        }
        catch (Exception ex)
        {
            string key = $"{ex.GetType().Name}:{ex.TargetSite?.Name}";
            if (_loggedErrors.Add(key))
                _logger.LogError($"[Elephant] OnMissionTick error: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
        }
    }

    // Attaches an ElephantBehaviorTree component to the agent if it is an elephant not already tracked.
    // Returns true when a tree was newly attached. Safe to call before/after the first-tick scan (de-duplicates).
    private bool TryAttachElephantTree(Agent agent)
    {
        if (agent == null || !_service.IsElephantMonster(agent.Monster?.StringId)) return false;
        for (int i = 0; i < _elephantComponents.Count; i++)
            if (_elephantComponents[i].agent == agent) return false;   // already attached

        var comp = new BehaviorTreeAgentComponent(agent, "ElephantTree", Array.Empty<object>());
        agent.AddComponent(comp);
        if (comp.Tree != null)
        {
            _elephantComponents.Add((agent, comp));
            return true;
        }
        _logger.LogError($"[Elephant] BT build failed for {agent.Name} (Rider={agent.RiderAgent?.Name ?? "null"})");
        return false;
    }

    public override void OnRemoveBehavior()
    {
        _elephantComponents.Clear();
        base.OnRemoveBehavior();
    }
}
