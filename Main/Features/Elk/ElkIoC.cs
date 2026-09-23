using DryIoc;

namespace TAOM.Features.Elk;

public static class ElkIoC
{
    public static void RegisterElkFeature(IContainer container)
    {
        // Pure, stateless decision service -> Singleton (csharp-architecture.md).
        container.Register<IElkAttackService, ElkAttackService>(Reuse.Singleton);
    }
}
