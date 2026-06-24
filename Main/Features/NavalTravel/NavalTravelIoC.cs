using DryIoc;

namespace TAOM.Features.NavalTravel;

public static class NavalTravelIoC
{
    public static void RegisterNavalTravelFeature(IContainer container)
    {
        container.Register<INavalTravelConfigProvider, NavalTravelConfigProvider>(Reuse.Singleton);
        container.Register<INavalTravelSettingsProvider, NavalTravelSettingsProvider>(Reuse.Singleton);
        container.Register<INavalTravelService, NavalTravelService>(Reuse.Singleton);
    }
}
