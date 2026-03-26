using System;
using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.StartupResources.Config;

namespace TAOM.Features.StartupResources;

public class StartupInfluenceService : IStartupInfluenceService
{
    private readonly IClanStartupAdapter _clanAdapter;
    private readonly IStartupResourcesConfigProvider _configProvider;
    private readonly IModLogger _logger;

    public StartupInfluenceService(
        IClanStartupAdapter clanAdapter,
        IStartupResourcesConfigProvider configProvider,
        IModLogger logger)
    {
        _clanAdapter = clanAdapter;
        _configProvider = configProvider;
        _logger = logger;
    }

    public void DistributeStartupInfluence()
    {
        var config = _configProvider.LoadConfig();
        var lookup = BuildCultureLookup(config);
        var clans = _clanAdapter.GetEligibleClans();

        float totalInfluence = 0f;
        int clanCount = 0;

        foreach (var clan in clans)
        {
            if (lookup.TryGetValue(clan.CultureId.ToLowerInvariant(), out var entry) && entry.Influence > 0f)
            {
                _clanAdapter.AddInfluence(clan.ClanId, entry.Influence);
                totalInfluence += entry.Influence;
                clanCount++;
            }
        }

        _logger.LogInfo($"StartupInfluence: added {totalInfluence} influence to {clanCount} clans");
    }

    private static Dictionary<string, CultureResourceEntry> BuildCultureLookup(StartupResourcesConfig config)
    {
        var lookup = new Dictionary<string, CultureResourceEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in config.CultureEntries)
        {
            if (!string.IsNullOrEmpty(entry.CultureId))
                lookup[entry.CultureId.ToLowerInvariant()] = entry;
        }
        return lookup;
    }
}
