using DryIoc;

namespace TAOM.Features.MountDespawn;

public static class MountDespawnIoC
{
    public static void RegisterMountDespawnFeature(IContainer container)
    {
        container.Register<IMountDespawnSettingsProvider, MountDespawnSettingsProvider>(Reuse.Singleton);
        container.Register<IDeadMountDespawnService, DeadMountDespawnService>(Reuse.Singleton);
    }
}
