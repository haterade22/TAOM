// Adapted from yotthani's VanillaTuning (MIT, (c) 2026 yotthani), map-view-release.
namespace TAOM.Features.MapViewRelease;

/// <summary>What the layer-event handler asks whenever any scene layer activates or deactivates (docs/features/map-view-release.md). No member throws.</summary>
public interface IMapViewReleaseService
{
    /// <summary>
    /// A scene layer was deactivated. Does nothing unless the toggle is on, a campaign is running and the layer is the
    /// campaign map's own; then counts the cover and, on every Nth, remembers the layer as due for a release. Nothing is
    /// released yet: the engine raises this edge before the covering screen opens, while the map is still being paused.
    /// </summary>
    void OnSceneLayerDeactivated(object layer);

    /// <summary>
    /// A scene layer was activated. Returns at once while no release is due. When the due map layer comes back, releases
    /// its view (if the toggle is still on and a campaign still runs), before the map screen's own ready check.
    /// </summary>
    void OnSceneLayerActivated(object layer);

    /// <summary>Writes the install line. Called once, from the module's GameInit step.</summary>
    void LogInstall();
}
