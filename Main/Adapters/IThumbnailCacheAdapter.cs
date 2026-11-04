namespace TAOM.Adapters;

/// <summary>
/// Drops the engine's thumbnail caches so every icon renders again on its next request (ADR-013). The cache
/// manager is a View type, client-only, so the implementation lives in TAOM.Client; core
/// NullThumbnailCacheAdapter clears nothing.
/// </summary>
public interface IThumbnailCacheAdapter
{
    /// <summary>True when the caches were cleared. Textures are released deferred, not immediately.</summary>
    bool TryClearAll();
}
