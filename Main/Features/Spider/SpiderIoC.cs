using DryIoc;

namespace TAOM.Features.Spider;

public static class SpiderIoC
{
    public static void RegisterSpiderFeature(IContainer container)
    {
        // Singleton: stateless pure-decision + boundary service (elephant parity).
        container.Register<ISpiderAttackService, SpiderAttackService>(Reuse.Singleton);
    }
}
