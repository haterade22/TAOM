using DryIoc;

namespace TAOM.Features.SettlementFood;

public static class SettlementFoodIoC
{
    public static void RegisterSettlementFoodFeature(IContainer container)
    {
        container.Register<ISettlementFoodConfigProvider, SettlementFoodConfigProvider>(Reuse.Singleton);
        container.Register<ISettlementFoodService, SettlementFoodService>(Reuse.Singleton);
    }
}
