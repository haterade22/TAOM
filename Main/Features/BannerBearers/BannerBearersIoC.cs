using DryIoc;

namespace TAOM.Features.BannerBearers;

public static class BannerBearersIoC
{
    public static void RegisterBannerBearersFeature(IContainer container)
    {
        container.Register<IBannerBearerConfigProvider, BannerBearerConfigProvider>(Reuse.Singleton);
        container.Register<IBannerBearerService, BannerBearerService>(Reuse.Singleton);
    }
}
