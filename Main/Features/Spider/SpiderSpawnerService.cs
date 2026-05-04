using System;
using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace TAOM.Features.Spider;

/// <summary>
/// Spawns spider agents directly via Mission.SpawnAgent + AgentBuildData.Monster() —
/// bypasses the troop/NPCCharacter system since Bannerlord cannot resolve non-humanoid races.
///
/// The character anchor (BasicCharacterObject) and Monster lookup are pluggable via
/// constructor delegates so tests can stub them. The default delegate uses Mission.Current.SpawnAgent
/// which requires a live engine — production-only path.
/// </summary>
public class SpiderSpawnerService : ISpiderSpawnerService
{
    public const string SpiderMonsterId = "spider";
    public const string SpiderCharacterId = "taom_spider_creature";

    private readonly IObjectManagerAdapter _objectManager;
    private readonly IMissionAdapterFactory _adapterFactory;
    private readonly IModLogger _logger;
    private readonly Func<string, Monster> _monsterLookup;
    private readonly Func<AgentBuildData, Agent> _spawnDelegate;

    public SpiderSpawnerService(
        IObjectManagerAdapter objectManager,
        IMissionAdapterFactory adapterFactory,
        IModLogger logger)
        : this(objectManager, adapterFactory, logger,
              monsterLookup: id => MBObjectManager.Instance?.GetObject<Monster>(id),
              spawnDelegate: data => Mission.Current?.SpawnAgent(data))
    {
    }

    /// <summary>Test seam — allows stubbing Monster lookup and SpawnAgent.</summary>
    internal SpiderSpawnerService(
        IObjectManagerAdapter objectManager,
        IMissionAdapterFactory adapterFactory,
        IModLogger logger,
        Func<string, Monster> monsterLookup,
        Func<AgentBuildData, Agent> spawnDelegate)
    {
        _objectManager = objectManager;
        _adapterFactory = adapterFactory;
        _logger = logger;
        _monsterLookup = monsterLookup;
        _spawnDelegate = spawnDelegate;
    }

    public IReadOnlyList<IAgentAdapter> SpawnSpiders(Team team, Vec3 referencePosition, int count, float radius)
    {
        var spawned = new List<IAgentAdapter>();
        if (count <= 0) return spawned;

        BasicCharacterObject character = _objectManager?.GetBasicCharacter(SpiderCharacterId);
        if (character == null)
        {
            _logger.LogError($"[Spider] Cannot spawn — anchor character '{SpiderCharacterId}' not found in MBObjectManager. " +
                             $"Verify it is defined in TAOM/Main/_Module/ModuleData/characters/.");
            return spawned;
        }

        Monster spiderMonster = _monsterLookup(SpiderMonsterId);
        if (spiderMonster == null)
        {
            _logger.LogError($"[Spider] Cannot spawn — Monster '{SpiderMonsterId}' not found. " +
                             $"Verify LOTRLOME_Armory loads before TAOM (DependedModule order).");
            return spawned;
        }

        var random = new Random();
        for (int i = 0; i < count; i++)
        {
            try
            {
                Vec3 spawnPos = ComputeSpawnPosition(referencePosition, radius, random);
                Vec2 facing = (referencePosition.AsVec2 - spawnPos.AsVec2).Normalized();

                var build = new AgentBuildData(character)
                    .Monster(spiderMonster)
                    .Team(team)
                    .InitialPosition(in spawnPos)
                    .InitialDirection(in facing)
                    .Controller(AgentControllerType.AI)
                    .NoHorses(true)
                    .NoWeapons(true);

                Agent agent = _spawnDelegate(build);
                if (agent != null)
                {
                    spawned.Add(_adapterFactory.GetAgentAdapter(agent));
                }
                else
                {
                    _logger.LogWarning($"[Spider] SpawnAgent returned null for spider #{i + 1}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"[Spider] Failed to spawn spider #{i + 1}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        _logger.LogInfo($"[Spider] Spawned {spawned.Count}/{count} spiders on team");
        return spawned;
    }

    internal static Vec3 ComputeSpawnPosition(Vec3 reference, float radius, Random random)
    {
        // Random angle, random radius within [0.5*radius, radius] to avoid clustering at center.
        double angle = random.NextDouble() * 2.0 * Math.PI;
        float r = (float)(radius * (0.5 + random.NextDouble() * 0.5));
        float dx = (float)(r * Math.Cos(angle));
        float dy = (float)(r * Math.Sin(angle));
        return new Vec3(reference.x + dx, reference.y + dy, reference.z, reference.w);
    }
}
