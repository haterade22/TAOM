using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.StartupResources.Config;

namespace TAOM.Features.StartupResources;

public class StartupGoldService : IStartupGoldService
{
    private readonly IStartupHeroAdapter _heroAdapter;
    private readonly IGoldGiftAdapter _goldGiftAdapter;
    private readonly IStartupResourcesConfigProvider _configProvider;
    private readonly IModLogger _logger;

    public StartupGoldService(
        IStartupHeroAdapter heroAdapter,
        IGoldGiftAdapter goldGiftAdapter,
        IStartupResourcesConfigProvider configProvider,
        IModLogger logger)
    {
        _heroAdapter = heroAdapter;
        _goldGiftAdapter = goldGiftAdapter;
        _configProvider = configProvider;
        _logger = logger;
    }

    public void DistributeStartupGold()
    {
        var config = _configProvider.LoadConfig();
        var lookup = BuildCultureLookup(config);
        var heroes = _heroAdapter.GetAliveLordHeroes();

        int totalGold = 0;
        int lordCount = 0;

        foreach (var hero in heroes)
        {
            if (hero.IsPlayerClan)
                continue;

            if (lookup.TryGetValue(hero.CultureId.ToLowerInvariant(), out var entry) && entry.Gold > 0)
            {
                _goldGiftAdapter.GiveGoldToHero(hero.HeroId, entry.Gold);
                totalGold += entry.Gold;
                lordCount++;
            }
        }

        _logger.LogInfo($"StartupGold: distributed {totalGold} gold to {lordCount} lords");
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
