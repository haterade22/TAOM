using DryIoc;

namespace TAOM.Features.Siege;

public static class SiegeDefenseIoC
{
    public static void RegisterSiegeDefenseFeature(IContainer container)
    {
        container.Register<ISiegeDefenseConfigProvider, SiegeDefenseConfigProvider>(Reuse.Singleton);
        container.Register<ISiegeDefenseSettingsProvider, SiegeDefenseSettingsProvider>(Reuse.Singleton);
        container.Register<ISiegeDefenseService, SiegeDefenseService>(Reuse.Singleton);
    }
}
