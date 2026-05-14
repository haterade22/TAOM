using DryIoc;

namespace TAOM.Features.CulturalFeats;

/// <summary>
/// Registers the singleton <see cref="ICulturalFeatsService"/> used by the 16
/// <c>Taom*Model</c> overrides under <see cref="Models"/>. Phase 9b #144 / #176.
/// </summary>
public static class CulturalFeatsIoC
{
    public static void RegisterCulturalFeatsFeature(IContainer container)
    {
        container.Register<ICulturalFeatsService, CulturalFeatsService>(Reuse.Singleton);
    }
}
