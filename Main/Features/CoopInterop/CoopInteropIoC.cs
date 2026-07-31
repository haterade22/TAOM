using DryIoc;

namespace TAOM.Features.CoopInterop;

public static class CoopInteropIoC
{
    public static void RegisterCoopInteropFeature(IContainer container)
    {
        container.Register<ISaveDefinerCollisionDetector, SaveDefinerCollisionDetector>(Reuse.Singleton);
    }
}
