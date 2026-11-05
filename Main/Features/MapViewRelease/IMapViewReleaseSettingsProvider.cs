// Adapted from yotthani's VanillaTuning (MIT, (c) 2026 yotthani), map-view-release.
namespace TAOM.Features.MapViewRelease;

/// <summary>The two MCM settings of the map-view release (Battle Load Diagnostics page, group "Map Performance").</summary>
public interface IMapViewReleaseSettingsProvider
{
    /// <summary>"Release Map View Memory". Read at every scene-layer edge, which happens only on layer activation changes (screen changes, layers added or removed).</summary>
    bool ReleaseEnabled { get; }

    /// <summary>"Map View Release Interval", limited to 1 to 1000; 20 while MCM has no instance.</summary>
    int ReleaseInterval { get; }
}
