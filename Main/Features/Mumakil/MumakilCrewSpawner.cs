using System;
using System.Collections.Generic;
using BehaviorTreeWrapper;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.Elephant;
using TaleWorlds.Engine;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace TAOM.Features.Mumakil;

/// <summary>
/// Puts the Mûmakil's crew on its war tower (#627 phase 2): one archer per crew frame of a
/// <see cref="TaomMumakilPlatform"/>, each with its own <see cref="MumakilCrewAgentOrigin"/>. A clone of the
/// howdah's <see cref="TAOM.Features.Elephant.HowdahCrewSpawner"/>; boundary code over raw Agent and the engine's
/// spawn calls, game-tested (ADR-008), owned one-per-mission by <see cref="MumakilMissionBehavior"/>.
///
/// A spawn is QUEUED from OnAgentBuild and runs from the next OnMissionTick, because a SpawnAgent nested inside the
/// engine's own SpawnAgent loop over behaviours re-enters that loop mid-dispatch (#595).
/// </summary>
internal sealed class MumakilCrewSpawner
{
    /// <summary>Flip false to park the crew again; static readonly, not const, so the branch survives (#627).</summary>
    internal static readonly bool CrewSpawnEnabled = true;

    private readonly IModLogger _logger;
    private readonly DeferredCallbackQueue _queue = new();
    private readonly HashSet<string> _loggedErrors = new();

    // Per-mission platform serial for the [Mumakil#n] log tag; agent indices recycle, so the tag is not an index.
    private int _platformSerial;

    public MumakilCrewSpawner(IModLogger logger) => _logger = logger;

    /// <summary>
    /// Gives a mumakil rider its war tower and queues the crew. It lives here rather than in the mission behaviour
    /// so the entry point stays a delegation (ADR-002) and the platform and crew have one owner. Unlike the
    /// elephant's howdah there is no harness to check: the tower is an AdditionalMesh on the Horse item, so every
    /// mumakil has one.
    /// </summary>
    public void TryBuildPlatform(Agent rider)
    {
        if (rider?.MountAgent == null) return;
        try
        {
            GameEntity entity = GameEntity.Instantiate(Mission.Current.Scene, MumakilConfig.PlatformPrefabName, true);
            if (entity == null)
            {
                // Deduped: this runs per rider build, and a missing Armory prefab is one fact, not one per beast.
                if (_loggedErrors.Add("MumakilPlatform:missing"))
                    _logger.LogError(
                    $"[Mumakil] platform prefab '{MumakilConfig.PlatformPrefabName}' is not loaded: it ships in " +
                    "LOTRLOME_Armory/Prefabs, and an Armory reinstall or an old package drops it. No crew.");
                return;
            }
            entity.SetVisibilityExcludeParents(true);

            var platform = entity.GetFirstScriptOfType<TaomMumakilPlatform>();
            if (platform == null)
            {
                _logger.LogError("[Mumakil] TaomMumakilPlatform script not found on the instantiated prefab.");
                return;
            }

            platform.LogTag = $"[Mumakil#{++_platformSerial}]";
            platform.mumakilAgent = rider.MountAgent;
            // Placed and scaled BEFORE the crew spawn, so the seats report real world positions rather than the
            // world origin when the spawner reads them a tick later.
            platform.RepositionToMount();

            if (CrewSpawnEnabled) Queue(platform, rider);
        }
        catch (Exception ex)
        {
            if (_loggedErrors.Add($"MumakilPlatform:{ex.GetType().Name}"))
                _logger.LogError($"[Mumakil] platform build failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>Queue a crew for this platform; it spawns on the next <see cref="Drain"/>.</summary>
    public void Queue(TaomMumakilPlatform platform, Agent rider) =>
        _queue.Enqueue(() => SpawnQueued(platform, rider));

    /// <summary>From OnMissionTick, outside the engine's SpawnAgent loop.</summary>
    public void Drain() => _queue.Drain();

    /// <summary>Mission end: drop anything still queued.</summary>
    public void Clear()
    {
        _queue.Clear();
        _loggedErrors.Clear();
        _platformSerial = 0;
    }

    private void SpawnQueued(TaomMumakilPlatform platform, Agent rider)
    {
        try
        {
            Mission mission = Mission.Current;
            if (mission == null || mission.MissionEnded) return;

            Agent mount = platform.mumakilAgent;
            if (mount == null || !mount.IsActive() || !AgentSlotIdentity.IsCurrentOccupant(mount))
            {
                _logger.LogInfo($"{platform.LogTag} crew not spawned: the mumakil is gone");
                return;
            }
            if (!rider.IsActive() || !AgentSlotIdentity.IsCurrentOccupant(rider) || rider.Team == null)
            {
                _logger.LogInfo($"{platform.LogTag} crew not spawned: the rider is gone");
                return;
            }
            // BattleObserverMissionLogic reads agent.Origin.BattleCombatant unguarded on every build, so the crew
            // need an origin to forward to.
            if (rider.Origin == null)
            {
                _logger.LogWarning($"{platform.LogTag} crew not spawned: the rider has no origin to report to");
                return;
            }
            Spawn(platform, rider, mission);
        }
        catch (Exception ex)
        {
            if (_loggedErrors.Add($"MumakilCrew:{ex.GetType().Name}"))
                _logger.LogError($"{platform.LogTag} crew spawn failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void Spawn(TaomMumakilPlatform platform, Agent rider, Mission mission)
    {
        // BasicCharacterObject, never the sealed CharacterObject: GetObject<T> takes an exact-type path for a sealed
        // T, and Custom Battle registers NPCCharacter as BasicCharacterObject while the campaign registers
        // CharacterObject, so the sealed type finds nothing in the mode the smoke runs in (#627).
        var crewChar = MBObjectManager.Instance.GetObject<BasicCharacterObject>(MumakilConfig.CrewCharacterId);
        if (crewChar == null)
        {
            _logger.LogError($"{platform.LogTag} crew character '{MumakilConfig.CrewCharacterId}' not found.");
            return;
        }

        // The formation vanilla would give this troop, not the rider's: the crew are Ranged, the rider is Cavalry,
        // so a released archer rejoins the archers. The seat KEEPS this formation, because a null one pins
        // Agent.MissileRangeAdjusted at 0 and the archer never shoots.
        Formation crewFormation = rider.Team.GetFormation(mission.GetAgentTroopClass(rider.Team.Side, crewChar));

        int spawned = 0;
        int seatIndex = -1;
        foreach (StandingPoint sp in platform.StandingPoints)
        {
            seatIndex++;
            if (!(sp is TaomMumakilStandingPoint seat) || seat.IsDisabled || seat.MovingAgent != null)
                continue;

            MatrixFrame frame = seat.GameEntity.GetGlobalFrame();
            // A small lift keeps the feet off the deck body on the first frame; the seat holds them after that.
            Vec3 spawnPos = frame.origin + new Vec3(0f, 0f, 0.05f);
            if (!HowdahSeatMotion.IsPlaceable(spawnPos.x, spawnPos.y, spawnPos.z))
            {
                _logger.LogWarning($"{platform.LogTag} seat {seatIndex} has no usable world position; skipped");
                continue;
            }

            var buildData = new AgentBuildData(crewChar)
                .Team(rider.Team)
                .InitialPosition(spawnPos)
                .InitialDirection(frame.rotation.f.AsVec2.Normalized())
                .NoHorses(true)
                .Banner(rider.Origin.Banner)
                .ClothingColor1(rider.ClothingColor1)
                .ClothingColor2(rider.ClothingColor2)
                .TroopOrigin(new MumakilCrewAgentOrigin(rider.Origin, crewChar, rider.Origin.Seed + 1 + spawned));
            if (crewFormation != null)
                buildData = buildData.Formation(crewFormation);

            Agent crew = mission.SpawnAgent(buildData);
            if (crew == null)
            {
                _logger.LogError($"{platform.LogTag} SpawnAgent returned null for seat {seatIndex}.");
                continue;
            }

            // Managed-only seating: OnUse registers the archer without the native AIUseGameObjectEnable, which would
            // send the pathfinder climbing after a seat the navmesh cannot reach.
            seat.OnUse(crew, 0);
            // Vanilla wields every battle troop's weapons at spawn; without it an archer can stand empty-handed.
            crew.WieldInitialWeapons();
            spawned++;
        }

        if (spawned == 0)
            _logger.LogWarning($"{platform.LogTag} no available seats for crew spawn.");
        else
            _logger.LogInfo($"{platform.LogTag} crew spawned: {spawned} archer(s) across the decks");
    }
}
