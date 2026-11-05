// Adapted from yotthani's VanillaTuning (MIT, (c) 2026 yotthani), map-view-release.
namespace TAOM.Features.MapViewRelease;

/// <summary>The service the layer-event handler reaches, handed over by the module's static initialisation.</summary>
internal static class MapViewReleaseCalls
{
    internal static IMapViewReleaseService? Service { get; private set; }

    internal static void Initialize(IMapViewReleaseService? service) => Service = service;
}
