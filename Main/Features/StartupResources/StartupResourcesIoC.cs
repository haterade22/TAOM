using DryIoc;
using TAOM.Adapters;

namespace TAOM.Features.StartupResources;

public static class StartupResourcesIoC
{
    public static void RegisterStartupResourcesFeature(IContainer container)
    {
        container.Register<IStartupHeroAdapter, StartupHeroAdapter>(Reuse.Singleton);
        container.Register<IGoldGiftAdapter, GoldGiftAdapter>(Reuse.Singleton);
        container.Register<IClanStartupAdapter, ClanStartupAdapter>(Reuse.Singleton);
        container.Register<IStartupResourcesConfigProvider, StartupResourcesConfigProvider>(Reuse.Singleton);
        container.Register<IStartupGoldService, StartupGoldService>(Reuse.Singleton);
        container.Register<IStartupInfluenceService, StartupInfluenceService>(Reuse.Singleton);
        container.Register<IPlayerStartupGoldService, PlayerStartupGoldService>(Reuse.Singleton);
    }
}
