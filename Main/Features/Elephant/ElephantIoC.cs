using DryIoc;

namespace TAOM.Features.Elephant;

public static class ElephantIoC
{
    public static void RegisterElephantFeature(IContainer container)
    {
        // Pure, stateless decision service → Singleton (csharp-architecture.md).
        container.Register<IElephantAttackService, ElephantAttackService>(Reuse.Singleton);
        // Reads TaomSettings live on every call, so one instance serves the whole process.
        container.Register<IHowdahDiagnosticsSettingsProvider, HowdahDiagnosticsSettingsProvider>(Reuse.Singleton);
    }
}
