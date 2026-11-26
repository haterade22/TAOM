using DryIoc;
using TAOM.Features.FactionPower.Hooks;

namespace TAOM.Features.FactionPower;

public static class FactionPowerIoC
{
    public static void RegisterFactionPowerFeature(IContainer container)
    {
        container.Register<IFactionPowerCatalog, FactionPowerCatalogProvider>(Reuse.Singleton);
        container.Register<FactionPowerStore>(Reuse.Singleton);
        container.RegisterMapping<IFactionPowerSettings, FactionPowerStore>();
        container.Register<FactionPowerService>(Reuse.Singleton);
        container.Register<FactionPowerEditor>(Reuse.Singleton);
        container.Register<IFactionPowerPartyHook, FactionPowerPartyHook>(Reuse.Transient);
    }
}
