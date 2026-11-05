// Adapted from yotthani's VanillaTuning (MIT, (c) 2026 yotthani), frame-buffer-guard and frame-buffer-watch.
namespace TAOM.Features.SkeletonBuffer;

/// <summary>The two MCM toggles of the skeleton buffer feature (CrashReport page, Master group).</summary>
public interface ISkeletonBufferSettingsProvider
{
    /// <summary>"Skeleton Buffer Guard". Read once, at the first main menu.</summary>
    bool GuardEnabled { get; }

    /// <summary>"Skeleton Buffer Watch". Read at the start of each mission.</summary>
    bool WatchEnabled { get; }
}
