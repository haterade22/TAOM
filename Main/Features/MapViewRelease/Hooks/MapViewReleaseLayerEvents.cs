// Adapted from yotthani's VanillaTuning (MIT, (c) 2026 yotthani), map-view-release.
using TaleWorlds.Engine.Screens;
using TaleWorlds.ScreenSystem;

namespace TAOM.Features.MapViewRelease.Hooks;

/// <summary>
/// The entry point of the map-view release (docs/features/map-view-release.md): a handler on the engine's public static
/// event <c>ScreenLayer.OnLayerActiveStateChanged</c>, which <c>HandleDeactivate</c> raises right after the layer's
/// <c>OnDeactivate()</c>, <c>IsActive = false</c> and the focus loss, and <c>HandleActivate</c> raises after <c>IsActive =
/// true</c> and <c>OnActivate()</c>. No Harmony patch. The handler returns at once for anything but a scene layer, then hands
/// a deactivated one to <see cref="IMapViewReleaseService.OnSceneLayerDeactivated"/> (the cover is counted) and an activated
/// one to <see cref="IMapViewReleaseService.OnSceneLayerActivated"/> (the release runs there), whose first cheap checks return
/// for every layer that is not the campaign map's own. It names no map type. Main thread, on layer activation changes (screen
/// changes, layers added or removed).
/// </summary>
internal static class MapViewReleaseLayerEvents
{
    private static bool _subscribed;

    /// <summary>
    /// Subscribes the handler, once per process. GameInit runs once per process, so the subscription is never removed: the
    /// engine's own screen manager keeps its subscription for the whole run too.
    /// </summary>
    internal static void SubscribeOnce()
    {
        if (_subscribed) return;
        _subscribed = true;
        ScreenLayer.OnLayerActiveStateChanged += OnLayerActiveStateChanged;
    }

    internal static void OnLayerActiveStateChanged(ScreenLayer layer)
    {
        if (layer == null || !(layer is SceneLayer)) return;

        try
        {
            var service = MapViewReleaseCalls.Service;
            if (layer.IsActive) service?.OnSceneLayerActivated(layer);
            else service?.OnSceneLayerDeactivated(layer);
        }
        catch { /* the service never throws; this guards the call itself */ }
    }
}
