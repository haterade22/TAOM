using SandBox.View.Map;
using TAOM.Adapters;
using TAOM.Features.CheatMenu.UI;

namespace TAOM.Client.Adapters;

/// <summary>
/// Attaches the cheat menu's map view when the current map screen lacks it (ADR-013), as RealmBordersMapViewAdapter does.
/// The screen is rebuilt on every load and does not exist at session launch, so the campaign behavior asks from its tick.
/// </summary>
internal sealed class CheatMenuMapViewAdapter : ICheatMenuMapViewAdapter
{
    public void EnsureAttached()
    {
        var mapScreen = MapScreen.Instance;
        if (mapScreen == null || mapScreen.GetMapView<CheatMenuMapView>() != null)
            return;
        mapScreen.AddMapView<CheatMenuMapView>();
    }
}
