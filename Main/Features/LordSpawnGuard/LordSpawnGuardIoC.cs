using DryIoc;
using TAOM.Adapters;

namespace TAOM.Features.LordSpawnGuard;

public static class LordSpawnGuardIoC
{
    public static void RegisterLordSpawnGuardFeature(IContainer container)
    {
        container.Register<ILordSpawnGuardAdapter, LordSpawnGuardAdapter>(Reuse.Singleton);
        container.Register<ILordSpawnGuardService, LordSpawnGuardService>(Reuse.Singleton);
    }
}
