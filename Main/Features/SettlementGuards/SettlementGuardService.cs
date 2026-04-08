using System.Collections.Generic;
using TAOM.Core.Logging;
using TAOM.Features.SettlementGuards.Domain;
using TAOM.Features.TroopProgression;

namespace TAOM.Features.SettlementGuards;

public class SettlementGuardService : ISettlementGuardService
{
    private readonly ISettlementGuardConfigProvider _config;
    private readonly IRandomProvider _random;
    private readonly IModLogger _logger;

    public SettlementGuardService(
        ISettlementGuardConfigProvider config,
        IRandomProvider random,
        IModLogger logger)
    {
        _config = config;
        _random = random;
        _logger = logger;
    }

    public string ResolveGuardTroopId(SettlementGuardContext context, string spawnPointTag)
    {
        var pool = _config.GetBySettlementId(context.SettlementId)
                ?? _config.GetByClanId(context.OwnerClanId)
                ?? _config.GetByCultureId(context.CultureId);

        if (pool == null || pool.Guards.Count == 0)
            return null;

        var candidates = FilterBySpawnPoint(pool.Guards, spawnPointTag);
        if (candidates.Count == 0)
            candidates = pool.Guards;

        var troopId = PickWeighted(candidates);
        _logger.LogDebug($"SettlementGuard: settlement={context.SettlementId} clan={context.OwnerClanId} " +
                         $"culture={context.CultureId} spawnPoint={spawnPointTag} → {troopId}");
        return troopId;
    }

    public string ResolveSpearItemId(string cultureId)
    {
        return _config.GetSpearItemId(cultureId);
    }

    private static IReadOnlyList<GuardEntry> FilterBySpawnPoint(IReadOnlyList<GuardEntry> guards, string spawnPointTag)
    {
        if (string.IsNullOrEmpty(spawnPointTag))
            return guards;

        var filtered = new List<GuardEntry>();
        for (int i = 0; i < guards.Count; i++)
        {
            var entry = guards[i];
            if (entry.SpawnPoints.Count == 0)
            {
                filtered.Add(entry);
                continue;
            }

            for (int j = 0; j < entry.SpawnPoints.Count; j++)
            {
                if (entry.SpawnPoints[j] == spawnPointTag)
                {
                    filtered.Add(entry);
                    break;
                }
            }
        }

        return filtered;
    }

    private string PickWeighted(IReadOnlyList<GuardEntry> pool)
    {
        int totalWeight = 0;
        for (int i = 0; i < pool.Count; i++)
            totalWeight += pool[i].Weight;

        if (totalWeight <= 0)
            return pool[0].TroopId;

        int roll = _random.Next(totalWeight);

        int cumulative = 0;
        for (int i = 0; i < pool.Count; i++)
        {
            cumulative += pool[i].Weight;
            if (roll < cumulative)
                return pool[i].TroopId;
        }

        return pool[pool.Count - 1].TroopId;
    }
}
