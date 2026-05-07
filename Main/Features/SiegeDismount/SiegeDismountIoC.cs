using DryIoc;
using TAOM.Adapters;

namespace TAOM.Features.SiegeDismount;

public static class SiegeDismountIoC
{
    public static void RegisterSiegeDismountFeature(IContainer container)
    {
        container.Register<ISiegeDismountSettingsProvider, SiegeDismountSettingsProvider>(Reuse.Singleton);
        container.Register<IPlayerMountAdapter, PlayerMountAdapter>(Reuse.Singleton);
        container.Register<IPartyMountInventoryAdapter, PartyMountInventoryAdapter>(Reuse.Singleton);
        container.Register<ISiegeDismountService, SiegeDismountService>(Reuse.Singleton);
    }
}
