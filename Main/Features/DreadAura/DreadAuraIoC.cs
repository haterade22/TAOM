using DryIoc;

namespace TAOM.Features.DreadAura;

public static class DreadAuraIoC
{
    public static void RegisterDreadAuraFeature(IContainer container)
    {
        container.Register<IDreadAuraConfigProvider, DreadAuraConfigProvider>(Reuse.Singleton);
        container.Register<IDreadAuraSettingsProvider, DreadAuraSettingsProvider>(Reuse.Singleton);
        container.Register<IDreadRegistry, DreadRegistry>(Reuse.Singleton);
        container.Register<IDreadAuraService, DreadAuraService>(Reuse.Singleton);
    }
}
