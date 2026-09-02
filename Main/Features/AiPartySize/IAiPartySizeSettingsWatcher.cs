using MCM.Abstractions.Base;

namespace TAOM.Features.AiPartySize;

/// <summary>
/// Keeps the engine's per-party size-limit cache honest after an MCM change. See
/// <see cref="AiPartySizeSettingsWatcher"/> for why the cache needs busting at all.
/// </summary>
public interface IAiPartySizeSettingsWatcher
{
    void EnsureSubscribed(BaseSettings? settings);
}
