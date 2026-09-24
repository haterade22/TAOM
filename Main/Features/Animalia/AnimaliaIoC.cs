using DryIoc;

namespace TAOM.Features.Animalia;

public static class AnimaliaIoC
{
    public static void RegisterAnimaliaFeature(IContainer container)
    {
        // Pure, stateless decision services -> Singleton (csharp-architecture.md), one per animal.
        container.Register<IAnimaliaElkAttackService, AnimaliaElkAttackService>(Reuse.Singleton);
        container.Register<IAnimaliaMooseAttackService, AnimaliaMooseAttackService>(Reuse.Singleton);
    }
}
