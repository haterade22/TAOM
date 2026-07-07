using DryIoc;

namespace TAOM.Features.SaveLoadDiagnostics;

public static class SaveLoadDiagnosticsIoC
{
    public static void RegisterSaveLoadDiagnosticsFeature(IContainer container)
    {
        container.Register<ISaveLoadDiagnosticsService, SaveLoadDiagnosticsService>(Reuse.Singleton);
    }
}
