using DryIoc;
using TAOM.Adapters;

namespace TAOM.Features.BattleLoadDiagnostics;

public static class BattleLoadDiagnosticsIoC
{
    public static void RegisterBattleLoadDiagnosticsFeature(IContainer container)
    {
        container.Register<IBattleLoadDiagnosticsSettingsProvider, BattleLoadDiagnosticsSettingsProvider>(Reuse.Singleton);
        container.Register<IEquipmentDumpFormatter, EquipmentDumpFormatter>(Reuse.Singleton);
        container.Register<IBattleLoadDiagnosticsService, BattleLoadDiagnosticsService>(Reuse.Singleton);
        container.Register<IEquipmentSnapshotAdapter, EquipmentSnapshotAdapter>(Reuse.Singleton);
        container.Register<IBattleLoadStallMarker, BattleLoadStallMarker>(Reuse.Singleton);
        container.Register<BattleLoadStallWatchdog>(Reuse.Singleton);
        container.Register<ExitStallSampler>(Reuse.Singleton);
    }
}
