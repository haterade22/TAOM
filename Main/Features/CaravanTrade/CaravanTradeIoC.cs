using DryIoc;

namespace TAOM.Features.CaravanTrade;

public static class CaravanTradeIoC
{
    public static void RegisterCaravanTradeFeature(IContainer container)
    {
        container.Register<ICaravanTradeConfigProvider, CaravanTradeConfigProvider>(Reuse.Singleton);
        container.Register<ICaravanTradeSettingsProvider, CaravanTradeSettingsProvider>(Reuse.Singleton);
        container.Register<ICaravanTradeService, CaravanTradeService>(Reuse.Singleton);

        // Per-caravan visit memory — MUST be Singleton (shared state: the behavior writes visits,
        // the GetTradeScoreForTown hook reads the recency penalty).
        container.Register<ICaravanVisitMemory, CaravanVisitMemory>(Reuse.Singleton);
        container.Register<CaravanVisitMemoryBehavior>(Reuse.Singleton);
    }
}
