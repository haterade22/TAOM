using DryIoc;

namespace TAOM.Features.CaravanTrade;

public static class CaravanTradeIoC
{
    public static void RegisterCaravanTradeFeature(IContainer container)
    {
        container.Register<ICaravanTradeConfigProvider, CaravanTradeConfigProvider>(Reuse.Singleton);
        container.Register<ICaravanTradeSettingsProvider, CaravanTradeSettingsProvider>(Reuse.Singleton);
        container.Register<ICaravanTradeService, CaravanTradeService>(Reuse.Singleton);
    }
}
