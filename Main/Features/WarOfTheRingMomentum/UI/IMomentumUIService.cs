namespace TAOM.Features.WarOfTheRingMomentum.UI;

/// <summary>
/// Adds/removes the on-map momentum meter. Never caches the MapView across sessions —
/// MapScreen tears views down on save-load/exit; lookups go through GetMapView each call.
/// </summary>
public interface IMomentumUIService
{
    void AddMapMeter();
    void RemoveMapMeter();
}
