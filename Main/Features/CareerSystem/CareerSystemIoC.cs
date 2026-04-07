using DryIoc;
using TAOM.Adapters;
using TAOM.Features.CareerSystem.Mutations;

namespace TAOM.Features.CareerSystem;

public static class CareerSystemIoC
{
    public static void RegisterCareerSystemFeature(IContainer container)
    {
        // Phase 1: Data persistence
        container.Register<ICareerDataService, CareerDataService>(Reuse.Singleton);

        // Phase 2: Config, registry, passives, mutations
        container.Register<ICareerConfigProvider, CareerConfigProvider>(Reuse.Singleton);
        container.Register<ICareerRegistry, CareerRegistry>(Reuse.Singleton);
        container.Register<ICareerPassiveService, CareerPassiveService>(Reuse.Singleton);
        container.Register<IMutationCalculatorRegistry, MutationCalculatorRegistry>(Reuse.Singleton);

        // Phase 3: Campaign integration
        container.Register<ICareerCreationHandler, CareerCreationHandler>(Reuse.Singleton);
        container.Register<ICareerSwitchService, CareerSwitchService>(Reuse.Singleton);

        // Phase 4: Abilities and mutations
        container.Register<Abilities.ICareerAbilityService, Abilities.CareerAbilityService>(Reuse.Singleton);
        container.Register<IMutationService, MutationService>(Reuse.Singleton);

        // Phase 5: GameModel support
        container.Register<ICareerHeroAdapterFactory, CareerHeroAdapterFactory>(Reuse.Singleton);
    }

    public static void InitializeCalculators(IMutationCalculatorRegistry registry)
    {
        BuiltInCalculators.RegisterAll(registry);
    }
}
