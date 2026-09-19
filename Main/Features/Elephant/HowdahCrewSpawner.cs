using System;
using System.Collections.Generic;
using BehaviorTreeWrapper;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace TAOM.Features.Elephant;

/// <summary>
/// Spawns a howdah's crew (#627): harad archers on the crew frames of a <see cref="TaomHowdahMachine"/>, one
/// <see cref="HowdahCrewAgentOrigin"/> each, seated by <see cref="TaomHowdahStandingPoint"/>. Boundary code over raw
/// Agent and the engine's spawn calls, game-tested (ADR-008), not a service, like CreatureTreeTracker;
/// <see cref="ElephantMissionBehavior"/> owns one per mission. A spawn is queued from OnAgentBuild and runs from the
/// next OnMissionTick, because a SpawnAgent nested in the engine's SpawnAgent loop over behaviors re-enters that loop
/// mid-dispatch (#595).
/// </summary>
internal sealed class HowdahCrewSpawner
{
    // Re-enabled 2026-09-19 (#627, Mike) for the howdah harness only (HowdahHarness.CarriesCrew). Parked 2026-06-10 as
    // a confirmed slide source: the crew overlapped the elephant's capsule. The rebuilt floor's underside now sits
    // 0.33 m above that capsule, and the [Howdah#n] status line's carriedV measures any slide. static readonly, not
    // const, like TaomHowdahMachine.BoneTrackingEnabled: flip it false to park the crew again.
    internal static readonly bool CrewSpawnEnabled = true;

    private readonly IModLogger _logger;
    private readonly DeferredCallbackQueue _queue = new();
    private readonly HashSet<string> _loggedErrors = new();

    public HowdahCrewSpawner(IModLogger logger)
    {
        _logger = logger;
    }

    /// <summary>Queue a crew for this howdah; it spawns on the next <see cref="Drain"/>.</summary>
    public void Queue(TaomHowdahMachine machine, Agent mahout, Formation? crewFormation) =>
        _queue.Enqueue(() => SpawnQueued(machine, mahout, crewFormation));

    /// <summary>From OnMissionTick, outside the engine's SpawnAgent loop.</summary>
    public void Drain()
    {
        if (_queue.Count > 0) _queue.Drain();
    }

    /// <summary>Mission end: drop anything still queued.</summary>
    public void Clear()
    {
        _queue.Clear();
        _loggedErrors.Clear();
    }

    // The elephant and the mahout were live when the spawn was queued a tick ago; a recycled or dead handle must not
    // get a crew, and the crew's origins report to the mahout's, so it must have one.
    private void SpawnQueued(TaomHowdahMachine machine, Agent mahout, Formation? crewFormation)
    {
        try
        {
            Mission mission = Mission.Current;
            if (mission == null || mission.MissionEnded) return;
            Agent elephant = machine.elephantAgent;
            if (elephant == null || !elephant.IsActive() || !AgentSlotIdentity.IsCurrentOccupant(elephant))
            {
                _logger.LogInfo($"{machine.LogTag} crew not spawned: the elephant is gone");
                return;
            }
            if (!mahout.IsActive() || !AgentSlotIdentity.IsCurrentOccupant(mahout) || mahout.Team == null)
            {
                _logger.LogInfo($"{machine.LogTag} crew not spawned: the mahout is gone");
                return;
            }
            // No origin is not an option either: BattleObserverMissionLogic reads agent.Origin.BattleCombatant
            // unguarded on every build.
            if (mahout.Origin == null)
            {
                _logger.LogWarning($"{machine.LogTag} crew not spawned: the mahout has no origin to report to");
                return;
            }
            Spawn(machine, mahout, crewFormation);
        }
        catch (Exception ex)
        {
            if (_loggedErrors.Add($"HowdahCrew:{ex.GetType().Name}"))
                _logger.LogError($"{machine.LogTag} crew spawn failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    // Vanilla UsableMachine detachment cannot path to a moving target: archers walk toward the last-known seat
    // position but the elephant moves away. So the crew are spawned straight onto their seats.
    private void Spawn(TaomHowdahMachine machine, Agent mahout, Formation? crewFormation)
    {
        var crewChar = MBObjectManager.Instance.GetObject<CharacterObject>(ElephantConfig.HowdahCrewCharacterId);
        if (crewChar == null)
        {
            _logger.LogError($"{machine.LogTag} crew character '{ElephantConfig.HowdahCrewCharacterId}' not found.");
            return;
        }

        int seatIndex = 0;
        _logger.LogInfo($"{machine.LogTag} crew spawn: {machine.StandingPoints.Count} total StandingPoint(s) in prefab");
        foreach (StandingPoint sp in machine.StandingPoints)
        {
            _logger.LogInfo($"{machine.LogTag}   StandingPoint[{seatIndex}]: type={sp.GetType().Name} disabled={sp.IsDisabled} occupied={sp.MovingAgent != null}");
            seatIndex++;
        }

        int spawned = 0;
        foreach (StandingPoint sp in machine.StandingPoints)
        {
            if (!(sp is TaomHowdahStandingPoint seat) || seat.IsDisabled || seat.MovingAgent != null)
                continue;

            // Spawn on the seat's own crew frame: it stands on the floor top (3.15 m, 0.8 m behind the elephant's
            // origin) and faces outward. A 5 cm lift keeps the feet off the floor body on the first frame; the seat's
            // TeleportToPosition holds the agent on the frame every tick after.
            MatrixFrame frame = seat.GameEntity.GetGlobalFrame();
            var spawnPos = frame.origin + new Vec3(0f, 0f, 0.05f);
            _logger.LogInfo($"{machine.LogTag}   Spawning crew #{spawned} at pos=({spawnPos.x:F2},{spawnPos.y:F2},{spawnPos.z:F2})");

            // One origin per archer (#627, delta review F1): sharing the mahout's booked the first crew casualty as the
            // elephant rider's. The banner and clothing colours are the mahout's, as vanilla gives every troop its
            // side's (Mission.GetAgentBuildDataToSpawnTroop); NoHorses keeps an archer on the deck if its roster ever
            // gains a mount.
            var buildData = new AgentBuildData(crewChar)
                .Team(mahout.Team)
                .InitialPosition(spawnPos)
                .InitialDirection(frame.rotation.f.AsVec2.Normalized())
                .NoHorses(true)
                .Banner(mahout.Origin.Banner)
                .ClothingColor1(mahout.ClothingColor1)
                .ClothingColor2(mahout.ClothingColor2)
                .TroopOrigin(new HowdahCrewAgentOrigin(mahout.Origin, crewChar, mahout.Origin.Seed + 1 + spawned));

            // Build the crew into the mahout's pre-reassignment formation (Cavalry since 2026-06-29). The seat's OnUse
            // clears it while they are seated and restores it on release; TeleportToPosition holds their position.
            if (crewFormation != null)
                buildData = buildData.Formation(crewFormation);

            Agent crewAgent = Mission.Current.SpawnAgent(buildData);
            if (crewAgent == null)
            {
                _logger.LogError($"{machine.LogTag} crew SpawnAgent returned null for seat {spawned}.");
                continue;
            }

            _logger.LogInfo($"{machine.LogTag}   Crew #{spawned} agent built: name={crewAgent.Name} isActive={crewAgent.IsActive()} hasRanged={crewAgent.HasRangedWeapon(false)}");
            // Managed-only seating: OnUse registers the agent in our seat (AddMovingAgent + lock flags)
            // WITHOUT triggering native AIUseGameObjectEnable. UseGameObject would cause the native
            // pathfinder to continuously route agents toward the elevated seat (unreachable via navmesh),
            // producing a climbing loop. TeleportToPosition in OnTick handles visual elevation instead.
            seat.OnUse(crewAgent, 0);
            // Vanilla wields every battle troop's initial weapons at spawn (Mission.SpawnTroop); without it the
            // archer may stand with nothing in hand.
            crewAgent.WieldInitialWeapons();
            spawned++;
        }

        if (spawned == 0)
            _logger.LogWarning($"{machine.LogTag} no available seats for crew spawn.");
        else
            _logger.LogInfo($"{machine.LogTag} crew force-spawned: {spawned} archer(s)");
    }
}
