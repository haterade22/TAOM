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
/// Mission boundary for the war-elephant. Attaches a per-agent <see cref="ElephantBehaviorTree"/> (via a
/// <c>BehaviorTreeAgentComponent</c>) to every elephant in the battle — the warg's pattern
/// (<see cref="TAOM.Features.Warg.WargMissionBehavior"/>) applied to the elephant. The tree drives the auto-trample
/// (a behavioral 1-for-1 of ADOD_Beasts's <c>OnTickAsAI</c>; since 2026-09-23 under a player rider too): a ridden elephant plays an attack animation
/// and deals a radial knockdown to enemies within <see cref="ElephantConfig.TrampleRadius"/>. The pure gate + damage
/// formula live in <see cref="IElephantAttackService"/> (unit-tested); the engine work lives in the SHARED BT leaf
/// nodes (<c>ElephantLikeEngageDecorator</c> + <c>ElephantLikeTrampleTask</c>). This behavior also instantiates the howdah
/// when the mahout builds (see <see cref="TryInstantiateHowdah"/>). The has-rider branch structure is the
/// foundation for richer creature AI — player-triggered trample, enrage/charge — in later phases.
/// </summary>
public class ElephantMissionBehavior : MissionLogic
{
    private readonly IElephantAttackService _service;
    private readonly IModLogger _logger;
    private readonly IHowdahDiagnosticsSettingsProvider _howdahDiagnostics;
    private readonly HashSet<string> _loggedErrors = new();
    // Per-mission howdah serial for the [Howdah#n] log tag (#627); agent indices recycle, so the tag is not an index.
    private int _howdahSerial;
    // The howdah crews (#627): queued from OnAgentBuild, spawned from OnMissionTick. See HowdahCrewSpawner.
    private readonly HowdahCrewSpawner _crew;
    // Attach/prune bookkeeping (shadow list, dedup, late-attach counting) — shared tracker,
    // see CreatureTreeTracker for the discipline notes.
    private readonly CreatureTreeTracker _tracker;
    private bool _initialized;
    private bool _treesAdded;

    public ElephantMissionBehavior()
    {
        _service = IoC.Resolve<IElephantAttackService>();
        _logger = IoC.Resolve<IModLogger>();
        _howdahDiagnostics = IoC.Resolve<IHowdahDiagnosticsSettingsProvider>();
        _crew = new HowdahCrewSpawner(_logger);
        _tracker = new CreatureTreeTracker("ElephantTree", "[Elephant]",
            a => _service.IsCreatureMonster(a.Monster?.StringId), _logger);
    }

    public override void OnAgentBuild(Agent agent, Banner banner)
    {
        base.OnAgentBuild(agent, banner);

        // Late-spawn BT attach: only after Initialize has registered "ElephantTree" (first OnMissionTick).
        // Elephants that built before that are caught by the first-tick scan in OnMissionTick.
        if (_treesAdded) _tracker.TryLateAttach(agent);

        // Howdah: when the mahout rider builds (human, mounted on an elephant, wearing a harness HowdahHarness.GetsPlatform accepts),
        // instantiate the howdah seat entity above the elephant's neck. Behavioural port of ADOD_Beasts's
        // mission-logic OnAgentBuild howdah branch. MountAgent is already built at this point
        // because the engine always builds the mount before the rider (horse-slot spawn order).
        TryInstantiateHowdah(agent);
    }

    private void TryInstantiateHowdah(Agent agent)
    {
        if (agent == null || !agent.IsHuman || agent.MountAgent == null) return;
        if (!_service.IsCreatureMonster(agent.MountAgent.Monster?.StringId)) return;

        // Diagnostics (#627): one line per elephant rider saying why it did or did not get a howdah. Only elephant
        // riders get this far, a handful per battle, so no dedupe is needed.
        bool diagnostics = _howdahDiagnostics?.IsEnabled == true;
        try
        {
            // The harness the mount actually wears (#627, delta review F4): SpawnEquipment is the roster the engine rolled
            // for this agent, set before OnAgentBuild, where Character.Equipment is only the troop's first roster.
            Equipment equipment = agent.SpawnEquipment;
            if (equipment == null)
            {
                if (diagnostics)
                    _logger.LogInfo($"[Howdah] rider {agent.Name} on elephant index {agent.MountAgent.Index}: no equipment, no howdah");
                return;
            }
            var harness = equipment[EquipmentIndex.HorseHarness];
            string harnessId = harness.Item?.StringId;
            if (!HowdahHarness.GetsPlatform(harnessId))
            {
                if (diagnostics)
                    _logger.LogInfo(
                        $"[Howdah] rider {agent.Name} on elephant index {agent.MountAgent.Index} wears harness " +
                        $"'{harnessId ?? "none"}': no howdah (the triggers are {ElephantConfig.HowdahHarnessStringId} " +
                        $"and {ElephantConfig.HarnessStringId})");
                return;
            }

            GameEntity howdah = GameEntity.Instantiate(Mission.Current.Scene, ElephantConfig.HowdahPrefabName, true);
            if (howdah == null)
            {
                _logger.LogError(
                    $"[Elephant] Howdah: prefab '{ElephantConfig.HowdahPrefabName}' not found in any module's Prefabs folder " +
                    "(it ships in LOTRLOME_Armory/Prefabs; an Armory reinstall or an old package drops it).");
                return;
            }
            howdah.SetVisibilityExcludeParents(true);
            var machine = howdah.GetFirstScriptOfType<TaomHowdahMachine>();
            if (machine == null)
            {
                _logger.LogError("[Elephant] Howdah: TaomHowdahMachine script not found on instantiated prefab.");
                return;
            }
            machine.LogTag = $"[Howdah#{++_howdahSerial}]";
            machine.elephantAgent = agent.MountAgent;
            machine.elephantRider = agent;
            // Position the howdah at the elephant's back BEFORE spawning crew so that
            // seat GlobalPositions are valid world coordinates, not world origin (0,0,0).
            machine.RepositionToElephant();

            // Move the mahout to Cavalry so the elephant charges instead of circling at skirmish range. The rider's
            // default group has been Cavalry since 2026-06-29 (troops_harad.xml), so today this is a no-op; the crew
            // take the formation vanilla would give an archer (HowdahCrewSpawner), not the mahout's.
            Formation? previousFormation = agent.Formation;
            if (agent.Team != null)
            {
                var cavalryFormation = agent.Team.GetFormation(FormationClass.Cavalry);
                if (cavalryFormation != null && agent.Formation != cavalryFormation)
                {
                    agent.Formation = cavalryFormation;
                    _logger.LogInfo($"[Elephant] Mahout reassigned to Cavalry formation (was {previousFormation?.FormationIndex})");
                }
            }

            _logger.LogInfo(
                $"[Elephant] Howdah instantiated for rider={agent.Name} as {machine.LogTag}: prefab={ElephantConfig.HowdahPrefabName} " +
                $"elephant={agent.MountAgent.Name} (index {agent.MountAgent.Index}) harness={harnessId} side={agent.Team?.Side}");
            // Crew (#627): howdah harness only, and never spawned from here; this runs inside Mission.SpawnAgent's
            // loop over behaviors (#595). HowdahCrewSpawner queues it for the next OnMissionTick.
            if (HowdahCrewSpawner.CrewSpawnEnabled && HowdahHarness.CarriesCrew(harnessId))
            {
                _crew.Queue(machine, agent);
                if (diagnostics)
                    _logger.LogInfo($"{machine.LogTag} crew queued for the next mission tick");
            }
            else if (diagnostics)
            {
                _logger.LogInfo(HowdahCrewSpawner.CrewSpawnEnabled
                    ? $"{machine.LogTag} no crew: harness {harnessId} carries none (only {ElephantConfig.HowdahHarnessStringId} does)"
                    : $"{machine.LogTag} no crew: crew spawn is disabled");
            }
        }
        catch (Exception ex)
        {
            string key = $"Howdah:{ex.GetType().Name}";
            if (_loggedErrors.Add(key))
                _logger.LogError($"[Elephant] Howdah instantiation failed: {ex.GetType().Name}: {ex.Message}");
        }
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
        if (ElephantCombat.Profile.AnyUnresolved())
            _logger.LogError(
                "[Elephant] One or more attack actions resolved to act_none — LOTRLOME action_types drift? " +
                $"Expected {ElephantConfig.TrampleActionName}/{ElephantConfig.SideAttackLeftActionName}/" +
                $"{ElephantConfig.SideAttackRightActionName}. Attacks will not animate correctly.");

        _logger.LogInfo("[Elephant] Initialized");
        LogHowdahConfig();
    }

    // Once per mission (#627). A missing prefab is logged whatever the toggle says: it means no elephant in this
    // install can get a howdah (an Armory reinstall or an Armory package older than the TAOM build drops it).
    private void LogHowdahConfig()
    {
        bool prefabLoaded = GameEntity.PrefabExists(ElephantConfig.HowdahPrefabName);
        if (!prefabLoaded)
            _logger.LogWarning(
                $"[Howdah] prefab '{ElephantConfig.HowdahPrefabName}' is not loaded from any module's Prefabs folder: no elephant " +
                "will get a howdah. It ships in LOTRLOME_Armory/Prefabs.");
        if (_howdahDiagnostics?.IsEnabled != true) return;
        _logger.LogInfo(
            $"[Howdah] config: prefab={ElephantConfig.HowdahPrefabName} loaded={prefabLoaded} " +
            $"triggers={ElephantConfig.HowdahHarnessStringId}(crew),{ElephantConfig.HarnessStringId}(no crew) " +
            $"heightAboveGround={HowdahDiagnostics.Format(ElephantConfig.HowdahHeightAboveGround, 2)} " +
            $"boneTracking={TaomHowdahMachine.BoneTrackingEnabled} crewSpawn={(HowdahCrewSpawner.CrewSpawnEnabled ? "on" : "off")} " +
            $"statusEvery={HowdahDiagnostics.Format(ElephantConfig.HowdahStatusPeriodSeconds, 0)}s");
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
                _logger.LogInfo($"[Elephant] Attached behavior trees to {count} elephant(s)");
            }

            // Prune dead elephants from the shadow list. Agent.Tick auto-ticks each BT component (v1.4.5) —
            // there is no manual tick here; we only drop dead elephants so the list doesn't grow unbounded.
            _tracker.PruneDead();

            // Howdah crews queued from OnAgentBuild (#627): spawned here, outside the engine's SpawnAgent loop.
            _crew.Drain();
        }
        catch (Exception ex)
        {
            string key = $"{ex.GetType().Name}:{ex.TargetSite?.Name}";
            if (_loggedErrors.Add(key))
                _logger.LogError($"[Elephant] OnMissionTick error: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
        }
    }

    // Counts howdah crew shots for the diagnostics line. The native engine raises this for every missile fired
    // ([MBCallback] Mission.OnAgentShootMissile), and vanilla's archery training counts shots through the same hook.
    // A crew archer carries its own HowdahCrewAgentOrigin, so the origin identifies it and holds its count: no seat
    // list is searched from a callback whose thread is the engine's business.
    public override void OnAgentShootMissile(Agent shooterAgent, EquipmentIndex weaponIndex, Vec3 position, Vec3 velocity,
        Mat3 orientation, bool hasRigidBody, int forcedMissileIndex)
    {
        if (shooterAgent?.Origin is HowdahCrewAgentOrigin crew) crew.RecordShot();
    }

    public override void OnRemoveBehavior()
    {
        if (_treesAdded)
            _logger.LogInfo($"[Elephant] Mission end: {_tracker.LateAttachCount} tree(s) late-attached, {_tracker.AliveCount} elephant(s) alive at end");
        _tracker.Clear();
        // Clear error dedup so a fresh mission can re-log genuinely new occurrences (spider/mumakil parity).
        _loggedErrors.Clear();
        _howdahSerial = 0;
        _crew.Clear();
        base.OnRemoveBehavior();
    }
}
