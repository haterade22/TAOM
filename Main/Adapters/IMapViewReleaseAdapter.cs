// Adapted from yotthani's VanillaTuning (MIT, (c) 2026 yotthani), map-view-release.
namespace TAOM.Adapters;

/// <summary>
/// The engine side of the map-view release: whether a campaign is running, whether a scene layer is the campaign map's own,
/// and the release itself. Every member runs on the main thread, from the layer activation events.
/// </summary>
public interface IMapViewReleaseAdapter
{
    /// <summary><c>Campaign.Current != null</c>. Nothing about the map screen is touched until this is true.</summary>
    bool IsCampaignRunning { get; }

    /// <summary>True when <paramref name="layer"/> is the very <c>SceneLayer</c> of <c>MapScreen.Instance</c>, compared by reference. Call only while <see cref="IsCampaignRunning"/>.</summary>
    bool IsMapSceneLayer(object layer);

    /// <summary><c>SceneLayer.ClearRuntimeGPUMemory(false)</c>: releases the view's GPU resources and marks it not ready, keeps the scene and the terrain.</summary>
    void ReleaseSceneView(object layer);
}
