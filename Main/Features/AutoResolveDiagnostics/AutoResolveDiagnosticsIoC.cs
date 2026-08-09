using DryIoc;
using TAOM.Adapters;

namespace TAOM.Features.AutoResolveDiagnostics;

public static class AutoResolveDiagnosticsIoC
{
    /// <summary>
    /// Every type registered here must have EXACTLY ONE public constructor — DryIoc throws
    /// UnableToSelectSinglePublicConstructorFromMultiple at Register time, inside OnSubModuleLoad,
    /// which is a CTD before the main menu. Pinned by AutoResolveDiagnosticsWiringTests.
    /// </summary>
    public static void RegisterAutoResolveDiagnosticsFeature(IContainer container)
    {
        container.Register<IAutoResolveDiagnosticsSettingsProvider,
            AutoResolveDiagnosticsSettingsProvider>(Reuse.Singleton);
        container.Register<IMapEventBattleLogAdapter, MapEventBattleLogAdapter>(Reuse.Singleton);
        container.Register<ITroopCensusAdapter, TroopCensusAdapter>(Reuse.Singleton);
        container.Register<IAutoResolveLogWriter, AutoResolveLogWriter>(Reuse.Singleton);

        // The behavior itself is registered so SubModule.cs can resolve it.
        container.Register<AutoResolveDiagnosticsBehavior>(Reuse.Singleton);
    }
}
