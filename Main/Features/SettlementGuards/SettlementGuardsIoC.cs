using DryIoc;

namespace TAOM.Features.SettlementGuards;

public static class SettlementGuardsIoC
{
    public static void RegisterSettlementGuardsFeature(IContainer container)
    {
        container.Register<ISettlementGuardConfigProvider, SettlementGuardConfigProvider>(Reuse.Singleton);
        container.Register<ISettlementGuardService, SettlementGuardService>(Reuse.Singleton);
    }
}
