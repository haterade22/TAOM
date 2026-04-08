using DryIoc;
using TAOM.Adapters;

namespace TAOM.Features.NamedCompanions;

public static class NamedCompanionIoC
{
    public static void RegisterNamedCompanionsFeature(IContainer container)
    {
        container.Register<INamedCompanionConfigProvider, NamedCompanionConfigProvider>(Reuse.Singleton);
        container.Register<INamedCompanionAdapter, NamedCompanionAdapter>(Reuse.Singleton);
        container.Register<INamedCompanionService, NamedCompanionService>(Reuse.Singleton);
    }
}
