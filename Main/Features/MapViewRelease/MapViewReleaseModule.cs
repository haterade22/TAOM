// Adapted from yotthani's VanillaTuning (MIT, (c) 2026 yotthani), map-view-release.
using DryIoc;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Features.MapViewRelease.Hooks;

namespace TAOM.Features.MapViewRelease;

/// <summary>
/// Map-view release (docs/features/map-view-release.md): lets the campaign map's scene view give back the render targets a
/// covered map leaves behind, on every Nth cover. No Harmony patch: at GameInit, which comes after the main menu, the module
/// subscribes once to the engine's <c>ScreenLayer.OnLayerActiveStateChanged</c> event. The upstream mod moved its install to
/// the first map frame because an install at game start broke the load-game preview; TAOM does not know why. What TAOM does
/// instead: the handler touches no engine member for any layer but a scene layer (the first checks return), and
/// the map screen is reached only through the adapter, behind a campaign check, so compiling the handler resolves no map type.
/// </summary>
internal sealed class MapViewReleaseModule : TaomFeatureModule
{
    public override string Id => "MapViewRelease";

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<IMapViewReleaseAdapter, MapViewReleaseAdapter>(Reuse.Singleton);
        registrator.Register<IMapViewReleaseSettingsProvider, MapViewReleaseSettingsProvider>(Reuse.Singleton);
        registrator.Register<IMapViewReleaseService, MapViewReleaseService>(Reuse.Singleton);
    }

    public override void InitializeStatics(IResolver resolver) =>
        MapViewReleaseCalls.Initialize(resolver.Resolve<IMapViewReleaseService>());

    public override void OnPhase(ApplyPhase phase, IResolver resolver)
    {
        if (phase != ApplyPhase.GameInit) return;

        resolver.Resolve<IMapViewReleaseService>().LogInstall();
        MapViewReleaseLayerEvents.SubscribeOnce();
    }
}
