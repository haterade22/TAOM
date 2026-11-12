using DryIoc;

namespace TAOM.Features.LordRetinue;

public static class LordRetinueIoC
{
    public static void RegisterLordRetinueFeature(IContainer container)
    {
        container.Register<ILordRetinueSettingsProvider, LordRetinueSettingsProvider>(Reuse.Singleton);
        container.Register<Hooks.LordRetinueWorld>(Reuse.Singleton);
        container.Register<LordRetinueService>(Reuse.Singleton);
    }
}
