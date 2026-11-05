// Adapted from yotthani's VanillaTuning (MIT, (c) 2026 yotthani), map-view-release.
using TAOM.Features.BattleLoadDiagnostics;

namespace TAOM.Features.MapViewRelease;

/// <summary>
/// Reads the Battle Load Diagnostics MCM page through a cached reference (the contract of
/// <c>NameplateCullSettingsProvider</c>: lazy, read through, never snapshotted). Falls back to the shipped defaults (on,
/// every 20th cover) while MCM has no instance.
/// </summary>
public sealed class MapViewReleaseSettingsProvider : IMapViewReleaseSettingsProvider
{
    private BattleLoadDiagnosticsSettings? _settings;

    public MapViewReleaseSettingsProvider()
    {
    }

    internal MapViewReleaseSettingsProvider(BattleLoadDiagnosticsSettings settings) => _settings = settings;

    private BattleLoadDiagnosticsSettings? Settings => _settings ??= BattleLoadDiagnosticsSettings.Instance;

    public bool ReleaseEnabled => Settings?.ReleaseMapViewMemory ?? true;

    public int ReleaseInterval =>
        MapViewReleaseCounter.ClampInterval(Settings?.MapViewReleaseInterval ?? MapViewReleaseCounter.DefaultInterval);
}
