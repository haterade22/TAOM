using DryIoc;
using TAOM.Adapters;

namespace TAOM.Features.BannerColorPersistence;

public static class BannerColorPersistenceIoC
{
    public static void RegisterBannerColorPersistenceFeature(IContainer container)
    {
        container.Register<IBannerColorConfigProvider, BannerColorConfigProvider>(Reuse.Singleton);
        container.Register<IBannerColorService, BannerColorService>(Reuse.Singleton);
        container.Register<IBannerHeroAdapter, BannerHeroAdapter>(Reuse.Singleton);
    }
}
