using DryIoc;

namespace TAOM.Features.WarRam;

public static class WarRamIoC
{
    public static void RegisterWarRamFeature(IContainer container)
    {
        // Pure, stateless decision service -> Singleton (csharp-architecture.md).
        container.Register<IWarRamAttackService, WarRamAttackService>(Reuse.Singleton);
    }
}
