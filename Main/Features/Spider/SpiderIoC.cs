using DryIoc;

namespace TAOM.Features.Spider;

public static class SpiderIoC
{
    public static void RegisterSpiderFeature(IContainer container)
    {
        container.Register<ISpiderAttackService, SpiderAttackService>(Reuse.Transient);
        container.Register<ISpiderSpawnerService, SpiderSpawnerService>(Reuse.Singleton);
    }
}
