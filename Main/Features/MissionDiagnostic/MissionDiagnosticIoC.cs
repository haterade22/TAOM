using DryIoc;

namespace TAOM.Features.MissionDiagnostic;

public static class MissionDiagnosticIoC
{
    public static void RegisterMissionDiagnosticFeature(IContainer container)
    {
        container.Register<IMissionDiagnosticService, MissionDiagnosticService>(Reuse.Singleton);
    }
}
