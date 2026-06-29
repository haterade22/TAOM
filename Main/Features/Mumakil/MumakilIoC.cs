using DryIoc;

namespace TAOM.Features.Mumakil;

public static class MumakilIoC
{
    public static void RegisterMumakilFeature(IContainer container)
    {
        // Pure, stateless decision service → Singleton (csharp-architecture.md).
        container.Register<IMumakilAttackService, MumakilAttackService>(Reuse.Singleton);
    }
}
