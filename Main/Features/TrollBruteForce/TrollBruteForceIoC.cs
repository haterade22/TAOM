using DryIoc;

namespace TAOM.Features.TrollBruteForce;

public static class TrollBruteForceIoC
{
    public static void RegisterTrollBruteForceFeature(IContainer container)
    {
        // Pure, stateless decision service -> Singleton (csharp-architecture.md).
        container.Register<ITrollBruteForceService, TrollBruteForceService>(Reuse.Singleton);
    }
}
