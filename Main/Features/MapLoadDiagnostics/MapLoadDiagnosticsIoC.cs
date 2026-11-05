using DryIoc;

namespace TAOM.Features.MapLoadDiagnostics;

public static class MapLoadDiagnosticsIoC
{
    public static void RegisterMapLoadDiagnosticsFeature(IContainer container)
    {
        container.Register<IMapLoadHeartbeatService, MapLoadHeartbeatService>(Reuse.Singleton);
    }
}
