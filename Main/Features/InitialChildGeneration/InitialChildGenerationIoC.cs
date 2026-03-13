using DryIoc;
using TAOM.Adapters;

namespace TAOM.Features.InitialChildGeneration;

public static class InitialChildGenerationIoC
{
    public static void RegisterInitialChildGenerationFeature(IContainer container)
    {
        container.Register<IClanPopulationAdapter, ClanPopulationAdapter>(Reuse.Singleton);
        container.Register<IChildCreatorAdapter, ChildCreatorAdapter>(Reuse.Singleton);
        container.Register<IInitialChildGenerationConfigProvider, InitialChildGenerationConfigProvider>(Reuse.Singleton);
        container.Register<IRandomSource, SystemRandomSource>(Reuse.Singleton);
        container.Register<IInitialChildGenerationService, InitialChildGenerationService>(Reuse.Singleton);
    }
}
