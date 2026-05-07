using System;
using System.Linq;
using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.StartupResources;

public class PlayerStartupGoldService : IPlayerStartupGoldService
{
    private readonly IGoldGiftAdapter _goldGiftAdapter;
    private readonly IStartupResourcesConfigProvider _configProvider;
    private readonly IModLogger _logger;

    public PlayerStartupGoldService(
        IGoldGiftAdapter goldGiftAdapter,
        IStartupResourcesConfigProvider configProvider,
        IModLogger logger)
    {
        _goldGiftAdapter = goldGiftAdapter;
        _configProvider = configProvider;
        _logger = logger;
    }

    public void GrantPlayerStartupGold(string cultureId, string playerHeroId)
    {
        if (string.IsNullOrEmpty(cultureId))
        {
            _logger.LogWarning("PlayerStartupGoldService: cultureId is null or empty — skipping grant");
            return;
        }

        if (string.IsNullOrEmpty(playerHeroId))
        {
            _logger.LogWarning($"PlayerStartupGoldService: playerHeroId is null or empty for culture '{cultureId}' — skipping grant");
            return;
        }

        var config = _configProvider.LoadConfig();
        var entry = config.CultureEntries.FirstOrDefault(
            e => string.Equals(e.CultureId, cultureId, StringComparison.OrdinalIgnoreCase));

        if (entry == null)
        {
            _logger.LogWarning($"PlayerStartupGoldService: no startup-resources entry for culture '{cultureId}' — no gold granted");
            return;
        }

        if (entry.PlayerGold <= 0)
            return;

        _goldGiftAdapter.GiveGoldToHero(playerHeroId, entry.PlayerGold);
        _logger.LogInfo($"PlayerStartupGoldService: granted {entry.PlayerGold} gold to player ({cultureId})");
    }
}
