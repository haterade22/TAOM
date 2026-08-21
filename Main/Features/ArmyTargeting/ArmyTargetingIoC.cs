using DryIoc;
using TAOM.Adapters;
using TAOM.Features.ArmyTargeting.Diagnostics;

namespace TAOM.Features.ArmyTargeting;

public static class ArmyTargetingIoC
{
    public static void RegisterArmyTargetingFeature(IContainer container)
    {
        container.Register<IArmyTargetingConfigProvider, ArmyTargetingConfigProvider>(Reuse.Singleton);
        container.Register<IArmyTargetingSettingsProvider, ArmyTargetingSettingsProvider>(Reuse.Singleton);
        container.Register<IArmyTargetingService, ArmyTargetingService>(Reuse.Singleton);
        // Singleton because it carries a day-scoped distance memo; a transient would rebuild the
        // per-faction fief list on every AI tick.
        container.Register<IMapReachAdapter, MapReachAdapter>(Reuse.Singleton);
        container.Register<ISiegeGatheringDiagnosticsService, SiegeGatheringDiagnosticsService>(Reuse.Singleton);
    }
}
