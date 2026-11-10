using DryIoc;
using TAOM.Adapters;

namespace TAOM.Features.Arachnophobia;

public static class ArachnophobiaIoC
{
    public static void RegisterArachnophobiaFeature(IContainer container)
    {
        container.Register<ITroopMountCatalogAdapter, TroopMountCatalogAdapter>(Reuse.Singleton);
        container.Register<IArachnophobiaService, ArachnophobiaService>(Reuse.Singleton);
    }
}
