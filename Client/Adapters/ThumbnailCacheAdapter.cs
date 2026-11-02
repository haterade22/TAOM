using TaleWorlds.MountAndBlade.View.Tableaus;
using TAOM.Adapters;

namespace TAOM.Client.Adapters;

/// <summary>
/// Clears every thumbnail cache through <see cref="ThumbnailCacheManager"/> (ADR-013); core reaches this through
/// <see cref="IThumbnailCacheAdapter"/>. Used by the race-framing tuner so an icon offset edit shows without a
/// restart.
/// </summary>
internal sealed class ThumbnailCacheAdapter : IThumbnailCacheAdapter
{
    // Deferred release (false), not ReleaseImmediately: the call clears every thumbnail cache (items,
    // portraits, banners), and the inventory or the map bar may still be showing those textures. Vanilla
    // releases immediately only from MapScreen.OnInitialize and ClearGPUMemory, when no such screen is open.
    public bool TryClearAll()
    {
        var manager = ThumbnailCacheManager.Current;
        if (manager == null)
            return false;

        manager.ForceClearAllCache(false);
        return true;
    }
}
