using DryIoc;

namespace TAOM.Features.BanditManagement;

public static class BanditManagementIoC
{
    public static void RegisterBanditManagementFeature(IContainer container)
    {
        container.Register<IBanditScalingConfigProvider, BanditScalingConfigProvider>(Reuse.Singleton);
        container.Register<IBanditScalingSettingsProvider, BanditScalingSettingsProvider>(Reuse.Singleton);
        container.Register<IBanditScalingService, BanditScalingService>(Reuse.Singleton);
        container.Register<IHideoutDescriptionService, HideoutDescriptionService>(Reuse.Singleton);
    }
}
