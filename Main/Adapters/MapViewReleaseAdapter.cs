// Adapted from yotthani's VanillaTuning (MIT, (c) 2026 yotthani), map-view-release.
using System.Runtime.CompilerServices;
using SandBox.View.Map;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Engine.Screens;

namespace TAOM.Adapters;

/// <summary>
/// Engine side of the map-view release (docs/features/map-view-release.md). The map screen is named in exactly one method,
/// <see cref="MapSceneLayerOrNull"/>, which is marked NoInlining and which only <see cref="IsMapSceneLayer"/> calls, only
/// after the service has seen a running campaign. Nothing else here, in the service or in the layer-event handler
/// resolves <c>MapScreen</c> (or runs its static members) when the code is compiled, so subscribing the handler at game
/// init touches no map type. <c>MapViewReleaseBindingTests</c> pins that property on the IL.
/// </summary>
public sealed class MapViewReleaseAdapter : IMapViewReleaseAdapter
{
    public bool IsCampaignRunning => Campaign.Current != null;

    public bool IsMapSceneLayer(object layer)
    {
        if (!(layer is SceneLayer sceneLayer)) return false;
        return ReferenceEquals(sceneLayer, MapSceneLayerOrNull());
    }

    public void ReleaseSceneView(object layer)
    {
        // The engine's own entry point (MapScreen.ClearGPUMemory calls it with true): exactly SceneView.ClearAll(false, remove_terrain).
        // It releases the view's GPU resources and marks the view not ready; the scene and the terrain stay.
        ((SceneLayer)layer).ClearRuntimeGPUMemory(remove_terrain: false);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static SceneLayer? MapSceneLayerOrNull() => MapScreen.Instance?.SceneLayer;
}
