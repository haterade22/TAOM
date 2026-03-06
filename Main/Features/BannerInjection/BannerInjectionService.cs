using TAOM.Adapters;
using TAOM.Core.Logging;
using TaleWorlds.CampaignSystem;

namespace TAOM.Features.BannerInjection;

public class BannerInjectionService : IBannerInjectionService
{
    private readonly IKingdomBannerAdapter _kingdomAdapter;
    private readonly IClanBannerAdapter _clanAdapter;
    private readonly IBannerExclusionService _exclusionService;
    private readonly IBannerConfigProvider _configProvider;
    private readonly IModLogger _logger;

    public BannerInjectionService(
        IKingdomBannerAdapter kingdomAdapter,
        IClanBannerAdapter clanAdapter,
        IBannerExclusionService exclusionService,
        IBannerConfigProvider configProvider,
        IModLogger logger)
    {
        _kingdomAdapter = kingdomAdapter;
        _clanAdapter = clanAdapter;
        _exclusionService = exclusionService;
        _configProvider = configProvider;
        _logger = logger;
    }

    public void InjectBanners()
    {
        var kingdomKeys = _configProvider.GetKingdomBannerKeys();
        var clanKeys = _configProvider.GetClanBannerKeys();

        var kingdomCount = InjectKingdomBanners(kingdomKeys);
        var clanCount = InjectClanBanners(clanKeys);

        _logger.LogInfo(
            $"BannerInjection: Injected {kingdomCount} kingdom(s), {clanCount} clan(s).");
    }

    public void SyncData(IDataStore dataStore)
    {
        _exclusionService.SyncData(dataStore);
    }

    private int InjectKingdomBanners(System.Collections.Generic.Dictionary<string, string> kingdomKeys)
    {
        var injected = 0;
        var kingdoms = _kingdomAdapter.GetAllKingdoms();

        foreach (var kingdom in kingdoms)
        {
            if (!kingdomKeys.TryGetValue(kingdom.StringId, out var expectedKey))
                continue;

            if (_exclusionService.IsPlayerModified(kingdom.StringId))
                continue;

            if (kingdom.BannerCode == expectedKey)
                continue;

            _kingdomAdapter.SetBanner(kingdom.StringId, expectedKey);
            _kingdomAdapter.InvalidateVisuals(kingdom.StringId);
            injected++;
        }

        return injected;
    }

    private int InjectClanBanners(System.Collections.Generic.Dictionary<string, string> clanKeys)
    {
        var injected = 0;
        var clans = _clanAdapter.GetAllClans();

        foreach (var clan in clans)
        {
            if (clan.IsRulingClan)
                continue;

            if (!clanKeys.TryGetValue(clan.StringId, out var expectedKey))
                continue;

            if (_exclusionService.IsPlayerModified(clan.StringId))
                continue;

            if (clan.BannerCode == expectedKey)
                continue;

            _clanAdapter.SetBanner(clan.StringId, expectedKey);
            _clanAdapter.InvalidateVisuals(clan.StringId);
            injected++;
        }

        return injected;
    }
}
