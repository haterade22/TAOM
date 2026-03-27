using DryIoc;
using TAOM.Features.AdvancedCombat.Services;

namespace TAOM.Features.AdvancedCombat;

public static class AdvancedCombatIoC
{
    public static void RegisterAdvancedCombatFeature(IContainer container)
    {
        container.Register<IBoneCollisionService, BoneCollisionService>(Reuse.Singleton);
        container.Register<ISpatialGridDebugService, SpatialGridDebugService>(Reuse.Singleton);
    }
}
