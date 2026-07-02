using DryIoc;

namespace TAOM.Features.SettlementEconomy;

public static class SettlementEconomyIoC
{
    public static void RegisterSettlementEconomyFeature(IContainer container)
    {
        container.Register<ISettlementEconomyConfigProvider, SettlementEconomyConfigProvider>(Reuse.Singleton);
        container.Register<ISettlementEconomyService, SettlementEconomyService>(Reuse.Singleton);
    }
}
