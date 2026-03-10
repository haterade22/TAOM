using DryIoc;

namespace TAOM.Features.TroopProgression;

public static class TroopProgressionIoC
{
    public static void RegisterTroopProgressionFeature(IContainer container)
    {
        container.Register<ITroopCostService, TroopCostService>(Reuse.Singleton);
        container.Register<IVolunteerTierService, VolunteerTierService>(Reuse.Singleton);
    }
}
