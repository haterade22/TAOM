using DryIoc;

namespace TAOM.Features.BlowDiagnostics;

public static class BlowDiagnosticsIoC
{
    public static void RegisterBlowDiagnosticsFeature(IContainer container)
    {
        container.Register<IBlowDiagnosticsSettingsProvider, BlowDiagnosticsSettingsProvider>(Reuse.Singleton);
        container.Register<IBlowDiagnosticService, BlowDiagnosticService>(Reuse.Singleton);
    }
}
