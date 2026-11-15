using TaleWorlds.CampaignSystem;

namespace TAOM.Features.BannerInjection;

public interface IBannerInjectionService
{
    void InjectBanners();

    /// <summary>
    /// Replaces each icon id the v2.0.40 repack removed, in every kingdom and clan banner, with the nearest-id icon
    /// its BannerIconGroup still holds (#1285, RemovedBannerIcons). A player's own banner is included. Returns the
    /// count of banners changed.
    /// </summary>
    int RemapRemovedIcons();
    void SyncData(IDataStore dataStore);
}
