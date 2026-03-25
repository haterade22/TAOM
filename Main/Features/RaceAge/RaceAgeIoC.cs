using DryIoc;
using TAOM.Adapters;

namespace TAOM.Features.RaceAge;

public static class RaceAgeIoC
{
    public static void RegisterRaceAgeFeature(IContainer container)
    {
        container.Register<IHeroAgeAdapter, HeroAgeAdapter>(Reuse.Singleton);
        container.Register<IRaceAgeConfigProvider, RaceAgeConfigProvider>(Reuse.Singleton);
        container.Register<IRaceAgeService, RaceAgeService>(Reuse.Singleton);
    }
}
