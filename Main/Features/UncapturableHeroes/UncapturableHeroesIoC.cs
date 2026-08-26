using DryIoc;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.UncapturableHeroes.Hooks;

namespace TAOM.Features.UncapturableHeroes;

public static class UncapturableHeroesIoC
{
    public static void RegisterUncapturableHeroesFeature(IContainer container)
    {
        container.Register<IHeroCaptivityAdapter, HeroCaptivityAdapter>(Reuse.Singleton);
        container.Register<IUncapturableHeroesConfigProvider, UncapturableHeroesConfigProvider>(Reuse.Singleton);
        container.Register<IUncapturableHeroesSettingsProvider, UncapturableHeroesSettingsProvider>(Reuse.Singleton);
        container.Register<IUncapturableRegistry, UncapturableRegistry>(Reuse.Singleton);
        container.Register<IUncapturableHeroService, UncapturableHeroService>(Reuse.Singleton);
    }

    /// <summary>
    /// Hands the two Harmony hooks their service. Called from the single eager-initialisation block
    /// at the end of IoC.Configure, never from the registration method above: an eager Resolve
    /// during registration materializes IEnumerable&lt;T&gt; injections and makes anything
    /// registered later invisible.
    /// </summary>
    internal static void InitializePatchStatics(IContainer container)
    {
        var service = container.Resolve<IUncapturableHeroService>();
        var logger = container.Resolve<IModLogger>();

        Hero_CanBecomePrisoner_Patch.Initialize(service, logger);
        TakePrisonerAction_Apply_Patch.Initialize(service, logger);
    }
}
