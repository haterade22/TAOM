// Adapted from yotthani's VanillaTuning (MIT, (c) 2026 yotthani), frame-buffer-guard and frame-buffer-watch.
namespace TAOM.Adapters;

/// <summary>The one engine fact the skeleton buffer watch needs besides memory: one on-screen warning.</summary>
public interface ISkeletonBufferEngineAdapter
{
    /// <summary>One localized line in the message log: the battle fills <paramref name="percent"/> percent of the engine's skeleton buffer and may freeze.</summary>
    void ShowWarning(int percent);
}
