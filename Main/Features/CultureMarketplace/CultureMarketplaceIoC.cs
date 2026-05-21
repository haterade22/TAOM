using DryIoc;
using TAOM.Adapters;
using TAOM.Features.CultureMarketplace.Domain;

namespace TAOM.Features.CultureMarketplace;

public static class CultureMarketplaceIoC
{
    public static void RegisterCultureMarketplaceFeature(IContainer container)
    {
        container.RegisterInstance(MarketplaceTuning.Default);
        container.Register<IItemPoolAdapter, ItemPoolAdapter>(Reuse.Singleton);
        container.Register<ITownRosterAdapter, TownRosterAdapter>(Reuse.Singleton);
        container.Register<ICultureMarketplaceConfigProvider, CultureMarketplaceConfigProvider>(Reuse.Singleton);
        container.Register<ICultureItemPoolService, CultureItemPoolService>(Reuse.Singleton);
        container.Register<ICultureMarketplaceInjectionService, CultureMarketplaceInjectionService>(Reuse.Singleton);
        container.Register<ICultureMarketplaceMaintenanceService, CultureMarketplaceMaintenanceService>(Reuse.Singleton);
    }
}
