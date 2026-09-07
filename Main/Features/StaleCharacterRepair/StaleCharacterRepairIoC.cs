using DryIoc;
using TAOM.Adapters;

namespace TAOM.Features.StaleCharacterRepair;

public static class StaleCharacterRepairIoC
{
    public static void RegisterStaleCharacterRepairFeature(IContainer container)
    {
        container.Register<IStaleCharacterAdapter, StaleCharacterAdapter>(Reuse.Singleton);
        container.Register<IStaleCharacterRepairService, StaleCharacterRepairService>(Reuse.Singleton);
    }
}
