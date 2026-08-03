using DryIoc;

namespace TAOM.Features.SiegePropDiagnostics;

public static class SiegePropDiagnosticsIoC
{
    public static void RegisterSiegePropDiagnosticsFeature(IContainer container)
    {
        container.Register<ISiegePropDiagnosticsSettingsProvider, SiegePropDiagnosticsSettingsProvider>(Reuse.Singleton);
        container.Register<ISiegePropDiagnosticsService, SiegePropDiagnosticsService>(Reuse.Singleton);
    }
}
