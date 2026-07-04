using SandBox.View.Map;

namespace TAOM.Features.WarOfTheRingMomentum.UI;

public class MomentumUIService : IMomentumUIService
{
    public void AddMapMeter()
    {
        var mapScreen = MapScreen.Instance;
        if (mapScreen == null)
            return;
        if (mapScreen.GetMapView<MomentumIndicatorMapView>() != null)
            return;

        mapScreen.AddMapView<MomentumIndicatorMapView>();
    }

    public void RemoveMapMeter()
    {
        var mapScreen = MapScreen.Instance;
        var view = mapScreen?.GetMapView<MomentumIndicatorMapView>();
        if (view != null)
            mapScreen.RemoveMapView(view);
    }
}
