// Adapted from yotthani's VanillaTuning (MIT, (c) 2026 yotthani), frame-buffer-guard and frame-buffer-watch.
using TAOM.Features.CrashReport;

namespace TAOM.Features.SkeletonBuffer;

/// <summary>
/// Reads the CrashReport MCM page. Both toggles fall back to the shipped default (on) when MCM has no instance yet: the
/// guard's byte checks still decide whether anything is written, so a missing setting cannot make an install unsafe.
/// </summary>
public sealed class SkeletonBufferSettingsProvider : ISkeletonBufferSettingsProvider
{
    public bool GuardEnabled => CrashReportSettings.Instance?.SkeletonBufferGuard ?? true;

    public bool WatchEnabled => CrashReportSettings.Instance?.SkeletonBufferWatch ?? true;
}
