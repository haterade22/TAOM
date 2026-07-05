using DryIoc;
using TAOM.Features.ArmyTargeting.Diagnostics;

namespace TAOM.Features.ArmyTargeting;

public static class ArmyTargetingIoC
{
    public static void RegisterArmyTargetingFeature(IContainer container)
    {
        container.Register<IArmyTargetingConfigProvider, ArmyTargetingConfigProvider>(Reuse.Singleton);
        container.Register<IArmyTargetingSettingsProvider, ArmyTargetingSettingsProvider>(Reuse.Singleton);
        container.Register<IArmyTargetingService, ArmyTargetingService>(Reuse.Singleton);
        container.Register<ISiegeGatheringDiagnosticsService, SiegeGatheringDiagnosticsService>(Reuse.Singleton);
    }
}
