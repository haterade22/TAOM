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

    public int RemapRemovedIcons()
    {
        var changed = 0;
        foreach (var kingdom in _kingdomAdapter.GetAllKingdoms())
        {
            var code = BannerIconRemap.Remap(kingdom.BannerCode, RemovedBannerIcons.Replacement);
            if (code is null)
                continue;
            _kingdomAdapter.SetBanner(kingdom.StringId, code);
            _kingdomAdapter.InvalidateVisuals(kingdom.StringId);
            changed++;
        }
        foreach (var clan in _clanAdapter.GetAllClans())
        {
            var code = BannerIconRemap.Remap(clan.BannerCode, RemovedBannerIcons.Replacement);
            if (code is null)
                continue;
            _clanAdapter.SetBanner(clan.StringId, code);
            _clanAdapter.InvalidateVisuals(clan.StringId);
            changed++;
        }
        if (changed > 0)
            _logger.LogInfo($"BannerInjection: {changed} banner(s) named a removed icon; each now shows the nearest icon of its old group (#1285).");
        return changed;
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
