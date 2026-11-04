namespace TAOM.Adapters;

/// <summary>No client thumbnail cache (a dedicated server, or a client where TAOM.Client did not load): nothing is cleared.</summary>
public sealed class NullThumbnailCacheAdapter : IThumbnailCacheAdapter
{
    public bool TryClearAll() => false;
}
