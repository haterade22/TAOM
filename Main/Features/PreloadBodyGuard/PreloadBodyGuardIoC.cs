using DryIoc;

namespace TAOM.Features.PreloadBodyGuard;

public static class PreloadBodyGuardIoC
{
    public static void RegisterPreloadBodyGuardFeature(IContainer container)
    {
        container.Register<IPreloadBodyGuardService, PreloadBodyGuardService>(Reuse.Singleton);
    }
}
