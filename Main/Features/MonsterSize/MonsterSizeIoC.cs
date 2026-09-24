using DryIoc;
using TAOM.Adapters;

namespace TAOM.Features.MonsterSize;

public static class MonsterSizeIoC
{
    public static void RegisterMonsterSizeFeature(IContainer container)
    {
        container.Register<IMonsterSizeCatalogAdapter, MonsterSizeCatalogAdapter>(Reuse.Singleton);
        container.Register<IMonsterSizeService, MonsterSizeService>(Reuse.Singleton);
    }
}
