using DryIoc;

namespace TAOM.Features.CoopInterop;

public static class CoopInteropIoC
{
    public static void RegisterCoopInteropFeature(IContainer container)
    {
        container.Register<ISaveDefinerCollisionDetector, SaveDefinerCollisionDetector>(Reuse.Singleton);
        container.Register<ICoopPresenceProvider, CoopPresenceProvider>(Reuse.Singleton);
        // Session/role probe. Singleton because the reflection BINDING is cached; the probe itself
        // still re-reads Coop's state on every call, which it must — see ICoopSessionProvider.
        container.Register<ICoopSessionProvider, CoopSessionProvider>(Reuse.Singleton);
    }
}
